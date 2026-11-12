using System.Security.Cryptography;
using System.Threading.RateLimiting;
using Microsoft.EntityFrameworkCore;
using PresidentsMap;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDb>(o =>
    o.UseSqlite(builder.Configuration.GetConnectionString("Default") ?? "Data Source=presidents.db"));
builder.Services.AddSingleton<CountryCatalog>();
builder.Services.AddSingleton<CardRenderer>();
builder.Services.AddSingleton<IPaymentGateway, DemoPaymentGateway>();
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy("buy", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
    scope.ServiceProvider.GetRequiredService<AppDb>().Database.EnsureCreated();

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseRateLimiter();

// Who holds which country on a given day, and what that day costs.
app.MapGet("/api/days/{date}", async (string date, AppDb db) =>
{
    if (!DateOnly.TryParseExact(date, "yyyy-MM-dd", out var day)) return Results.BadRequest();
    var gifts = await db.Gifts.Where(g => g.Date == day)
        .Select(g => new { country = g.Country, to = g.To, from = g.From }).ToListAsync();
    return Results.Ok(new { date = day, special = Pricing.SpecialDay(day), multiplier = Pricing.Multiplier(day), gifts });
});

app.MapGet("/api/summary", async (AppDb db) => Results.Ok(new
{
    count = await db.Gifts.CountAsync(),
    total = await db.Gifts.SumAsync(g => (int?)g.Price) ?? 0,
    recent = await db.Gifts.OrderByDescending(g => g.CreatedAt).Take(8)
        .Select(g => new { country = g.Country, date = g.Date, to = g.To, price = g.Price }).ToListAsync(),
}));

// The certificate id is the only key to the personal message, so it is never listed publicly.
app.MapGet("/api/gifts/{id}", async (string id, AppDb db) =>
    await db.Gifts.FindAsync(id) is { } g ? Results.Ok(GiftDto.Of(g)) : Results.NotFound());

app.MapPost("/api/gifts", async (GiftRequest req, AppDb db, CountryCatalog catalog, IPaymentGateway payments, CancellationToken ct) =>
{
    var country = catalog.Find(req.Country);
    if (country is null) return Problem("ასეთი ქვეყანა რუკაზე არ არის.");
    if (!DateOnly.TryParseExact(req.Date, "yyyy-MM-dd", out var day)) return Problem("თარიღი არასწორია.");

    // "Today" differs by up to 14 hours around the world; accept any date that is still today somewhere.
    var earliest = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-12));
    if (day < earliest) return Problem("წარსული თარიღის ჩუქება არ შეიძლება.");
    if (day > earliest.AddYears(5)) return Problem("თარიღი ძალიან შორსაა. აირჩიე უახლოესი 5 წლიდან.");

    var to = (req.To ?? "").Trim();
    var from = (req.From ?? "").Trim();
    var message = (req.Message ?? "").Trim();
    if (to.Length < 2 || to.Length > Limits.Name) return Problem("ჩაწერე, ვის ჩუქნი (2-დან 40 სიმბოლომდე).");
    if (from.Length > Limits.Name) return Problem("შენი სახელი ძალიან გრძელია.");
    if (message.Length > Limits.Message) return Problem("მილოცვა ძალიან გრძელია (მაქსიმუმ 140 სიმბოლო).");

    const string takenText = "ეს დღე უკვე დაკავებულია. აირჩიე სხვა თარიღი ან სხვა ქვეყანა.";
    if (await db.Gifts.AnyAsync(g => g.Country == country.Id && g.Date == day, ct))
        return Problem(takenText, StatusCodes.Status409Conflict);

    var price = Pricing.Price(country, day);
    var payment = await payments.ChargeAsync(price, $"{country.Id} {day:yyyy-MM-dd}", ct);
    if (!payment.Ok) return Problem("გადახდა ვერ შესრულდა.", StatusCodes.Status402PaymentRequired);

    var gift = new Gift
    {
        Id = NewId(), Country = country.Id, Date = day, To = to, From = from, Message = message,
        Price = price, PaymentRef = payment.Reference, CreatedAt = DateTime.UtcNow,
    };
    db.Gifts.Add(gift);
    try
    {
        await db.SaveChangesAsync(ct);
    }
    catch (DbUpdateException)
    {
        // Someone else took the same day between the check and the insert.
        await payments.RefundAsync(payment.Reference, ct);
        return Problem(takenText, StatusCodes.Status409Conflict);
    }
    return Results.Created($"/cert/{gift.Id}", GiftDto.Of(gift));
}).RequireRateLimiting("buy");

app.MapGet("/cert/{id}", async (string id, HttpRequest http, AppDb db, CountryCatalog catalog) =>
{
    if (await db.Gifts.FindAsync(id) is not { } g) return Results.NotFound();
    var html = CertificatePage.Render(g, catalog.Find(g.Country)?.Name ?? g.Country, $"{http.Scheme}://{http.Host}");
    return Results.Content(html, "text/html; charset=utf-8");
});

app.MapGet("/cert/{id}/card.png", async (string id, HttpResponse res, AppDb db, CountryCatalog catalog, CardRenderer cards) =>
{
    if (await db.Gifts.FindAsync(id) is not { } g) return Results.NotFound();
    res.Headers.CacheControl = "public, max-age=86400";
    return Results.File(cards.Render(g, catalog.Find(g.Country)?.Name ?? g.Country), "image/png");
});

app.Run();

static IResult Problem(string text, int status = StatusCodes.Status400BadRequest) =>
    Results.Json(new { error = text }, statusCode: status);

static string NewId()
{
    const string alphabet = "abcdefghjkmnpqrstuvwxyz23456789";
    return string.Create(12, 0, (span, _) =>
    {
        for (var i = 0; i < span.Length; i++) span[i] = alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
    });
}

record GiftRequest(string? Country, string? Date, string? To, string? From, string? Message);

record GiftDto(string Id, string Country, DateOnly Date, string To, string From, string Message, int Price, string Number)
{
    public static GiftDto Of(Gift g) => new(g.Id, g.Country, g.Date, g.To, g.From, g.Message, g.Price, g.Number);
}

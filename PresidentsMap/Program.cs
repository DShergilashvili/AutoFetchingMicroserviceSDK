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
    total = (await db.Gifts.SumAsync(g => (int?)(g.Price + g.CabinetPaid)) ?? 0)
          + (await db.Appointments.SumAsync(a => (int?)a.TotalPaid) ?? 0),
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

// ---- cabinet: whoever has the certificate link can fill a post ----

app.MapGet("/api/gifts/{id}/cabinet", async (string id, AppDb db) =>
    await db.Gifts.FindAsync(id) is { } g ? Results.Ok(await CabinetView(db, g)) : Results.NotFound());

app.MapPost("/api/gifts/{id}/cabinet", async (string id, AppointRequest req, AppDb db, IPaymentGateway payments, CancellationToken ct) =>
{
    if (await db.Gifts.FindAsync([id], ct) is not { } g) return Results.NotFound();
    var post = Cabinet.Find(req.Post);
    if (post is null) return Problem("ასეთი თანამდებობა არ არსებობს.");
    var name = (req.Name ?? "").Trim();
    if (name.Length < 2 || name.Length > Limits.Name) return Problem("ჩაწერე სახელი (2-დან 40 სიმბოლომდე).");

    const string changedText = "სანამ იხდიდი, ეს სკამი სხვამ დაიკავა და ფასი შეიცვალა. ნახე ახალი ფასი.";
    var seat = await db.Appointments.FirstOrDefaultAsync(a => a.GiftId == id && a.Post == post.Key, ct);

    // Empty seat: a plain appointment. Occupied seat: a coup, which always costs money.
    var price = seat is null ? (g.CabinetPrepaid ? 0 : post.Price) : Cabinet.CoupPrice(seat.Worth);
    if (req.Price != price) return Problem(changedText, StatusCodes.Status409Conflict);

    var reference = "prepaid";
    if (price > 0)
    {
        var payment = await payments.ChargeAsync(price, $"{g.Number} {post.Key}", ct);
        if (!payment.Ok) return Problem("გადახდა ვერ შესრულდა.", StatusCodes.Status402PaymentRequired);
        reference = payment.Reference;
    }

    if (seat is null)
    {
        db.Appointments.Add(new Appointment
        {
            GiftId = id, Post = post.Key, Name = name, Price = price, Worth = post.Price, TotalPaid = price,
            PaymentRef = reference, CreatedAt = DateTime.UtcNow,
        });
    }
    else
    {
        seat.Name = name;
        seat.Price = price;
        seat.Worth = price;
        seat.TotalPaid += price;
        seat.Coups++;
        seat.PaymentRef = reference;
        seat.CreatedAt = DateTime.UtcNow;
    }
    try
    {
        await db.SaveChangesAsync(ct);
    }
    catch (DbUpdateException)
    {
        // Someone else got the seat between the read and the write.
        if (price > 0) await payments.RefundAsync(reference, ct);
        return Problem(changedText, StatusCodes.Status409Conflict);
    }
    return Results.Ok(await CabinetView(db, g));
}).RequireRateLimiting("buy");

app.MapPost("/api/gifts/{id}/cabinet/bundle", async (string id, AppDb db, IPaymentGateway payments, CancellationToken ct) =>
{
    if (await db.Gifts.FindAsync([id], ct) is not { } g) return Results.NotFound();
    if (g.CabinetPrepaid) return Problem("მთავრობა უკვე გადახდილია.", StatusCodes.Status409Conflict);
    var filled = await db.Appointments.Where(a => a.GiftId == id).Select(a => a.Post).ToListAsync(ct);
    var empty = Cabinet.Posts.Where(p => !filled.Contains(p.Key)).ToList();
    if (empty.Count == 0) return Problem("ყველა თანამდებობა უკვე დაკავებულია.", StatusCodes.Status409Conflict);

    var price = Cabinet.BundlePrice(empty);
    var payment = await payments.ChargeAsync(price, $"{g.Number} cabinet", ct);
    if (!payment.Ok) return Problem("გადახდა ვერ შესრულდა.", StatusCodes.Status402PaymentRequired);

    g.CabinetPrepaid = true;
    g.CabinetPaid = price;
    await db.SaveChangesAsync(ct);
    return Results.Ok(await CabinetView(db, g));
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

static async Task<object> CabinetView(AppDb db, Gift g)
{
    var seats = await db.Appointments.Where(a => a.GiftId == g.Id).ToDictionaryAsync(a => a.Post);
    var empty = Cabinet.Posts.Where(p => !seats.ContainsKey(p.Key)).ToList();
    return new
    {
        prepaid = g.CabinetPrepaid,
        bundlePrice = Cabinet.BundlePrice(empty),
        fullPrice = empty.Sum(p => p.Price),
        // price = what the next action on this seat costs: appointing if empty, a coup if occupied.
        posts = Cabinet.Posts.Select(p =>
        {
            var seat = seats.GetValueOrDefault(p.Key);
            return new
            {
                key = p.Key, title = p.Title, name = seat?.Name, coups = seat?.Coups ?? 0,
                price = seat is null ? (g.CabinetPrepaid ? 0 : p.Price) : Cabinet.CoupPrice(seat.Worth),
            };
        }),
    };
}

static string NewId()
{
    const string alphabet = "abcdefghjkmnpqrstuvwxyz23456789";
    return string.Create(12, 0, (span, _) =>
    {
        for (var i = 0; i < span.Length; i++) span[i] = alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
    });
}

record AppointRequest(string? Post, string? Name, int? Price);

record GiftRequest(string? Country, string? Date, string? To, string? From, string? Message);

record GiftDto(string Id, string Country, DateOnly Date, string To, string From, string Message, int Price, string Number)
{
    public static GiftDto Of(Gift g) => new(g.Id, g.Country, g.Date, g.To, g.From, g.Message, g.Price, g.Number);
}

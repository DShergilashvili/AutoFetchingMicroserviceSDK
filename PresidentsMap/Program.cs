using System.Security.Cryptography;
using System.Threading.RateLimiting;
using Microsoft.EntityFrameworkCore;
using PresidentsMap;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDb>(o =>
    o.UseSqlite(builder.Configuration.GetConnectionString("Default") ?? "Data Source=presidents.db"));
builder.Services.AddSingleton<CountryCatalog>();
builder.Services.AddSingleton<CardRenderer>();
// PayPal turns on when both keys are configured; without them every payment is a simulation.
if (builder.Configuration["PayPal:ClientId"] is { Length: > 0 } && builder.Configuration["PayPal:Secret"] is { Length: > 0 })
    builder.Services.AddHttpClient<IPaymentGateway, PayPalGateway>(c => c.Timeout = TimeSpan.FromSeconds(20));
else
    builder.Services.AddSingleton<IPaymentGateway, DemoPaymentGateway>();
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy("buy", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 30, Window = TimeSpan.FromMinutes(1) }));
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
    scope.ServiceProvider.GetRequiredService<AppDb>().Database.EnsureCreated();

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseRateLimiter();

app.MapGet("/api/config", (IPaymentGateway payments) => Results.Ok(new { paypalClientId = payments.PublicClientId }));

// Who holds which country on a given day, and what that day costs.
app.MapGet("/api/days/{date}", async (string date, HttpRequest http, AppDb db) =>
{
    if (!DateOnly.TryParseExact(date, "yyyy-MM-dd", out var day)) return Results.BadRequest();
    var gifts = (await db.Gifts.Where(g => g.Date == day).ToListAsync())
        .Select(g => new { country = g.Country, to = g.To, from = g.From, coups = g.Coups, coupPrice = Pricing.CoupPrice(g.Price), immune = g.Immune });
    return Results.Ok(new { date = day, special = Pricing.SpecialDay(day) is { } name ? Loc.T(Loc.Of(http), name) : null, multiplier = Pricing.Multiplier(day), gifts });
});

app.MapGet("/api/summary", async (AppDb db) => Results.Ok(new
{
    count = await db.Gifts.CountAsync(),
    total = (await db.Gifts.SumAsync(g => (int?)(g.TotalPaid + g.CabinetPaid)) ?? 0)
          + (await db.Appointments.SumAsync(a => (int?)a.TotalPaid) ?? 0),
    recent = await db.Gifts.OrderByDescending(g => g.CreatedAt).Take(8)
        .Select(g => new { country = g.Country, date = g.Date, to = g.To, price = g.Price, coups = g.Coups }).ToListAsync(),
}));

// The certificate id is the only key to the personal message, so it is never listed publicly.
app.MapGet("/api/gifts/{id}", async (string id, AppDb db) =>
    await db.Gifts.FindAsync(id) is { } g ? Results.Ok(GiftDto.Of(g)) : Results.NotFound());

app.MapPost("/api/gifts", async (GiftRequest req, HttpRequest http, AppDb db, CountryCatalog catalog, IPaymentGateway payments, CancellationToken ct) =>
{
    var country = catalog.Find(req.Country);
    if (country is null) return Problem(http, "ასეთი ქვეყანა რუკაზე არ არის.");
    if (!DateOnly.TryParseExact(req.Date, "yyyy-MM-dd", out var day)) return Problem(http, "თარიღი არასწორია.");

    // "Today" differs by up to 14 hours around the world; accept any date that is still today somewhere.
    var earliest = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-12));
    if (day < earliest) return Problem(http, "წარსული თარიღის ჩუქება არ შეიძლება.");
    if (day > earliest.AddYears(5)) return Problem(http, "თარიღი ძალიან შორსაა. აირჩიე უახლოესი 5 წლიდან.");

    var to = (req.To ?? "").Trim();
    var from = (req.From ?? "").Trim();
    var message = (req.Message ?? "").Trim();
    if (to.Length < 2 || to.Length > Limits.Name) return Problem(http, "ჩაწერე, ვის ჩუქნი (2-დან 40 სიმბოლომდე).");
    if (from.Length > Limits.Name) return Problem(http, "შენი სახელი ძალიან გრძელია.");
    if (message.Length > Limits.Message) return Problem(http, "მილოცვა ძალიან გრძელია (მაქსიმუმ 140 სიმბოლო).");

    const string changedText = "სანამ იხდიდი, ეს დღე სხვამ დაიკავა და ფასი შეიცვალა. ნახე ახალი ფასი.";
    var held = await db.Gifts.FirstOrDefaultAsync(g => g.Country == country.Id && g.Date == day, ct);

    // Free day: a plain gift. Held day: a coup against the sitting president, for 1.5x what they paid.
    if (held is { Immune: true }) return Problem(http, "ეს პრეზიდენტი გადატრიალებისგან დაცულია.", StatusCodes.Status409Conflict);
    var price = held is null ? Pricing.Price(country, day) : Pricing.CoupPrice(held.Price);
    var immune = req.Immune == true;
    var total = price + (immune ? Pricing.ImmunityPrice(price) : 0);
    if (req.Price != total) return Problem(http, changedText, StatusCodes.Status409Conflict);

    var payment = await payments.PayAsync(total, $"Presidents Map {country.Id} {day:yyyy-MM-dd}", ApprovedOrder(http), ct);
    if (payment.PendingOrderId is not null) return AwaitApproval(payment);
    if (!payment.Ok) return Problem(http, "გადახდა ვერ შესრულდა.", StatusCodes.Status402PaymentRequired);

    var gift = held ?? new Gift { Id = NewId(), Country = country.Id, Date = day };
    if (held is null) db.Gifts.Add(gift); else gift.Coups++;
    gift.To = to;
    gift.From = from;
    gift.Message = message;
    gift.Lang = Loc.Norm(req.Lang);
    gift.Price = price;
    gift.Immune = immune;
    gift.TotalPaid += total;
    gift.PaymentRef = payment.Reference;
    gift.CreatedAt = DateTime.UtcNow;
    try
    {
        await db.SaveChangesAsync(ct);
    }
    catch (DbUpdateException)
    {
        // Someone else got this day between the read and the write.
        await payments.RefundAsync(payment.Reference, ct);
        return Problem(http, changedText, StatusCodes.Status409Conflict);
    }
    return Results.Created($"/cert/{gift.Id}", GiftDto.Of(gift));
}).RequireRateLimiting("buy");

// Immunity bought later, from the certificate page.
app.MapPost("/api/gifts/{id}/immunity", async (string id, ImmunityRequest req, HttpRequest http, AppDb db, IPaymentGateway payments, CancellationToken ct) =>
{
    if (await db.Gifts.FindAsync([id], ct) is not { } g) return Results.NotFound();
    if (g.Immune) return Problem(http, "ეს პრეზიდენტი უკვე დაცულია.", StatusCodes.Status409Conflict);
    const string changedText = "სანამ იხდიდი, პრეზიდენტი შეიცვალა. გადატვირთე გვერდი.";
    var price = Pricing.ImmunityPrice(g.Price);
    if (req.Price != price) return Problem(http, changedText, StatusCodes.Status409Conflict);

    var payment = await payments.PayAsync(price, $"Presidents Map {g.Number} immunity", ApprovedOrder(http), ct);
    if (payment.PendingOrderId is not null) return AwaitApproval(payment);
    if (!payment.Ok) return Problem(http, "გადახდა ვერ შესრულდა.", StatusCodes.Status402PaymentRequired);

    // Only protect the president the buyer saw: a coup in between must not inherit the shield.
    var coups = g.Coups;
    var won = await db.Gifts.Where(x => x.Id == id && !x.Immune && x.Coups == coups)
        .ExecuteUpdateAsync(u => u.SetProperty(x => x.Immune, true).SetProperty(x => x.TotalPaid, x => x.TotalPaid + price), ct);
    if (won == 0)
    {
        await payments.RefundAsync(payment.Reference, ct);
        return Problem(http, changedText, StatusCodes.Status409Conflict);
    }
    await db.Entry(g).ReloadAsync(ct);
    return Results.Ok(GiftDto.Of(g));
}).RequireRateLimiting("buy");

// ---- cabinet: whoever has the certificate link can fill a post ----

app.MapGet("/api/gifts/{id}/cabinet", async (string id, HttpRequest http, AppDb db) =>
    await db.Gifts.FindAsync(id) is { } g ? Results.Ok(await CabinetView(db, g, Loc.Of(http))) : Results.NotFound());

app.MapPost("/api/gifts/{id}/cabinet", async (string id, AppointRequest req, HttpRequest http, AppDb db, IPaymentGateway payments, CancellationToken ct) =>
{
    if (await db.Gifts.FindAsync([id], ct) is not { } g) return Results.NotFound();
    var post = Cabinet.Find(req.Post);
    if (post is null) return Problem(http, "ასეთი თანამდებობა არ არსებობს.");
    var name = (req.Name ?? "").Trim();
    if (name.Length < 2 || name.Length > Limits.Name) return Problem(http, "ჩაწერე სახელი (2-დან 40 სიმბოლომდე).");

    const string changedText = "სანამ იხდიდი, ეს სკამი სხვამ დაიკავა და ფასი შეიცვალა. ნახე ახალი ფასი.";
    var seat = await db.Appointments.FirstOrDefaultAsync(a => a.GiftId == id && a.Post == post.Key, ct);

    // Empty seat: a plain appointment. Occupied seat: a coup, which always costs money.
    var price = seat is null ? (g.CabinetPrepaid ? 0 : post.Price) : Pricing.CoupPrice(seat.Worth);
    if (req.Price != price) return Problem(http, changedText, StatusCodes.Status409Conflict);

    var reference = "prepaid";
    if (price > 0)
    {
        var payment = await payments.PayAsync(price, $"Presidents Map {g.Number} {post.Key}", ApprovedOrder(http), ct);
        if (payment.PendingOrderId is not null) return AwaitApproval(payment);
        if (!payment.Ok) return Problem(http, "გადახდა ვერ შესრულდა.", StatusCodes.Status402PaymentRequired);
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
        return Problem(http, changedText, StatusCodes.Status409Conflict);
    }
    return Results.Ok(await CabinetView(db, g, Loc.Of(http)));
}).RequireRateLimiting("buy");

app.MapPost("/api/gifts/{id}/cabinet/bundle", async (string id, HttpRequest http, AppDb db, IPaymentGateway payments, CancellationToken ct) =>
{
    if (await db.Gifts.FindAsync([id], ct) is not { } g) return Results.NotFound();
    if (g.CabinetPrepaid) return Problem(http, "მთავრობა უკვე გადახდილია.", StatusCodes.Status409Conflict);
    var filled = await db.Appointments.Where(a => a.GiftId == id).Select(a => a.Post).ToListAsync(ct);
    var empty = Cabinet.Posts.Where(p => !filled.Contains(p.Key)).ToList();
    if (empty.Count == 0) return Problem(http, "ყველა თანამდებობა უკვე დაკავებულია.", StatusCodes.Status409Conflict);

    var price = Cabinet.BundlePrice(empty);
    var payment = await payments.PayAsync(price, $"Presidents Map {g.Number} cabinet", ApprovedOrder(http), ct);
    if (payment.PendingOrderId is not null) return AwaitApproval(payment);
    if (!payment.Ok) return Problem(http, "გადახდა ვერ შესრულდა.", StatusCodes.Status402PaymentRequired);

    // Two people may pay for the same cabinet at once; only the first update wins, the other is refunded.
    var won = await db.Gifts.Where(x => x.Id == id && !x.CabinetPrepaid)
        .ExecuteUpdateAsync(u => u.SetProperty(x => x.CabinetPrepaid, true).SetProperty(x => x.CabinetPaid, price), ct);
    if (won == 0)
    {
        await payments.RefundAsync(payment.Reference, ct);
        return Problem(http, "მთავრობა უკვე გადახდილია.", StatusCodes.Status409Conflict);
    }
    await db.Entry(g).ReloadAsync(ct);
    return Results.Ok(await CabinetView(db, g, Loc.Of(http)));
}).RequireRateLimiting("buy");

// A certificate is shown in the language it was bought in; ?lang= overrides that.
app.MapGet("/cert/{id}", async (string id, string? lang, HttpRequest http, AppDb db, CountryCatalog catalog) =>
{
    if (await db.Gifts.FindAsync(id) is not { } g) return Results.NotFound();
    var l = lang is null ? g.Lang : Loc.Norm(lang);
    var html = CertificatePage.Render(g, catalog.Find(g.Country)?.Name(l) ?? g.Country, $"{http.Scheme}://{http.Host}", l);
    return Results.Content(html, "text/html; charset=utf-8");
});

app.MapGet("/cert/{id}/card.png", async (string id, string? lang, HttpResponse res, AppDb db, CountryCatalog catalog, CardRenderer cards) =>
{
    if (await db.Gifts.FindAsync(id) is not { } g) return Results.NotFound();
    var l = lang is null ? g.Lang : Loc.Norm(lang);
    res.Headers.CacheControl = "public, max-age=86400";
    return Results.File(cards.Render(g, catalog.Find(g.Country)?.Name(l) ?? g.Country, l), "image/png");
});

app.Run();

static IResult Problem(HttpRequest http, string text, int status = StatusCodes.Status400BadRequest) =>
    Results.Json(new { error = Loc.T(Loc.Of(http), text) }, statusCode: status);

// The buyer's browser sends the same purchase twice: first bare, then with the order it approved.
static string? ApprovedOrder(HttpRequest http) => http.Headers["X-Payment-Order"].FirstOrDefault() is { Length: > 0 } id ? id : null;

static IResult AwaitApproval(Payment payment) =>
    Results.Json(new { paymentOrderId = payment.PendingOrderId }, statusCode: StatusCodes.Status202Accepted);

static async Task<object> CabinetView(AppDb db, Gift g, string lang)
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
                key = p.Key, title = Loc.T(lang, p.Title), name = seat?.Name, coups = seat?.Coups ?? 0,
                price = seat is null ? (g.CabinetPrepaid ? 0 : p.Price) : Pricing.CoupPrice(seat.Worth),
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

record GiftRequest(string? Country, string? Date, string? To, string? From, string? Message, string? Lang, int? Price, bool? Immune);

record ImmunityRequest(int? Price);

record GiftDto(string Id, string Country, DateOnly Date, string To, string From, string Message, int Price, string Number, int Coups, bool Immune)
{
    public static GiftDto Of(Gift g) => new(g.Id, g.Country, g.Date, g.To, g.From, g.Message, g.Price, g.Number, g.Coups, g.Immune);
}

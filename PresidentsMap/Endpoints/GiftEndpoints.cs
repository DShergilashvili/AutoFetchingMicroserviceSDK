using Microsoft.EntityFrameworkCore;
using static PresidentsMap.Endpoints.Api;

namespace PresidentsMap.Endpoints;

public static class GiftEndpoints
{
    private record GiftRequest(string? Country, string? Date, string? To, string? From, string? Message, string? Lang,
        int? Price, bool? Immune, string? Title, string? Email, string? RecipientEmail);

    private record ImmunityRequest(int? Price);

    public static void MapGifts(this WebApplication app)
    {
        app.MapGet("/api/config", (IPaymentGateway payments) => Results.Ok(new
        {
            paypalClientId = payments.PublicClientId,
            titlePrice = Titles.UpgradePrice,
        }));

        // Who holds which country on a given day, and what that day costs.
        app.MapGet("/api/days/{date}", async (string date, HttpRequest http, AppDb db) =>
        {
            if (!DateOnly.TryParseExact(date, "yyyy-MM-dd", out var day)) return Results.BadRequest();
            var gifts = (await db.Gifts.Where(g => g.Date == day).ToListAsync()).Select(g => new
            {
                country = g.Country, to = g.To, from = g.From, title = g.Title, coups = g.Coups,
                coupPrice = Pricing.CoupPrice(g.Price), immune = g.Immune,
            });
            var special = Pricing.SpecialDay(day) is { } name ? Loc.T(Loc.Of(http), name) : null;
            return Results.Ok(new { date = day, special, multiplier = Pricing.Multiplier(day), gifts });
        });

        app.MapGet("/api/summary", async (AppDb db) => Results.Ok(new
        {
            count = await db.Gifts.CountAsync(),
            total = (await db.Gifts.SumAsync(g => (int?)(g.TotalPaid + g.CabinetPaid)) ?? 0)
                  + (await db.Appointments.SumAsync(a => (int?)a.TotalPaid) ?? 0),
            recent = await db.Gifts.OrderByDescending(g => g.CreatedAt).Take(8)
                .Select(g => new { country = g.Country, date = g.Date, to = g.To, price = g.Price, coups = g.Coups }).ToListAsync(),
            contested = await db.Gifts.Where(g => g.Coups > 0).OrderByDescending(g => g.Coups).ThenByDescending(g => g.Price).Take(5)
                .Select(g => new { country = g.Country, date = g.Date, to = g.To, price = g.Price, coups = g.Coups }).ToListAsync(),
        }));

        // The certificate id is the only key to the personal message, so it is never listed publicly.
        app.MapGet("/api/gifts/{id}", async (string id, AppDb db) =>
            await db.Gifts.FindAsync(id) is { } g ? Results.Ok(GiftDto.Of(g)) : Results.NotFound());

        app.MapPost("/api/gifts", Buy).RequireRateLimiting(BuyLimit);
        app.MapPost("/api/gifts/{id}/immunity", BuyImmunity).RequireRateLimiting(BuyLimit);
    }

    private static async Task<IResult> Buy(GiftRequest req, HttpRequest http, AppDb db, CountryCatalog catalog,
        IPaymentGateway payments, Mailer mailer, IConfiguration config, CancellationToken ct)
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
        var title = req.Title ?? Titles.Default;
        if (to.Length < 2 || to.Length > Limits.Name) return Problem(http, "ჩაწერე, ვის ჩუქნი (2-დან 40 სიმბოლომდე).");
        if (from.Length > Limits.Name) return Problem(http, "შენი სახელი ძალიან გრძელია.");
        if (message.Length > Limits.Message) return Problem(http, "მილოცვა ძალიან გრძელია (მაქსიმუმ 140 სიმბოლო).");
        if (!Titles.Exists(title)) return Problem(http, "ასეთი ტიტული არ არსებობს.");
        if (!TryEmail(req.Email, out var buyerEmail) || !TryEmail(req.RecipientEmail, out var recipientEmail))
            return Problem(http, "ელფოსტის მისამართი არასწორია.");

        const string changedText = "სანამ იხდიდი, ეს დღე სხვამ დაიკავა და ფასი შეიცვალა. ნახე ახალი ფასი.";
        var held = await db.Gifts.FirstOrDefaultAsync(g => g.Country == country.Id && g.Date == day, ct);

        // Free day: a plain gift. Held day: a coup against the sitting ruler, for 1.5x what they paid.
        if (held is { Immune: true }) return Problem(http, "ეს პრეზიდენტი გადატრიალებისგან დაცულია.", StatusCodes.Status409Conflict);
        var price = held is null ? Pricing.Price(country, day) : Pricing.CoupPrice(held.Price);
        var immune = req.Immune == true;
        var total = price + (immune ? Pricing.ImmunityPrice(price) : 0) + Titles.Price(title);
        if (req.Price != total) return Problem(http, changedText, StatusCodes.Status409Conflict);

        var payment = await payments.PayAsync(total, $"Presidents Map {country.Id} {day:yyyy-MM-dd}", ApprovedOrder(http), ct);
        if (payment.PendingOrderId is not null) return AwaitApproval(payment);
        if (!payment.Ok) return Problem(http, "გადახდა ვერ შესრულდა.", StatusCodes.Status402PaymentRequired);

        // Whoever is being overthrown hears about it, in their own language.
        var deposed = held is { BuyerEmail.Length: > 0 } ? new Deposed(held.BuyerEmail, held.To, held.Lang, held.Origin) : null;

        var gift = held ?? new Gift { Id = NewId(), Country = country.Id, Date = day };
        if (held is null) db.Gifts.Add(gift); else gift.Coups++;
        gift.To = to;
        gift.From = from;
        gift.Message = message;
        gift.Title = title;
        gift.Lang = Loc.Norm(req.Lang);
        gift.BuyerEmail = buyerEmail;
        gift.RecipientEmail = recipientEmail;
        gift.Delivered = false;
        gift.Origin = Origin(http, config);
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

        mailer.Receipt(gift, country);
        if (deposed is not null) mailer.Overthrown(deposed, gift, country);
        return Results.Created($"/cert/{gift.Id}", GiftDto.Of(gift));
    }

    /// <summary>Immunity bought later, from the certificate page.</summary>
    private static async Task<IResult> BuyImmunity(string id, ImmunityRequest req, HttpRequest http, AppDb db,
        IPaymentGateway payments, CancellationToken ct)
    {
        if (await db.Gifts.FindAsync([id], ct) is not { } g) return Results.NotFound();
        if (g.Immune) return Problem(http, "ეს პრეზიდენტი უკვე დაცულია.", StatusCodes.Status409Conflict);
        const string changedText = "სანამ იხდიდი, პრეზიდენტი შეიცვალა. გადატვირთე გვერდი.";
        var price = Pricing.ImmunityPrice(g.Price);
        if (req.Price != price) return Problem(http, changedText, StatusCodes.Status409Conflict);

        var payment = await payments.PayAsync(price, $"Presidents Map {g.Number} immunity", ApprovedOrder(http), ct);
        if (payment.PendingOrderId is not null) return AwaitApproval(payment);
        if (!payment.Ok) return Problem(http, "გადახდა ვერ შესრულდა.", StatusCodes.Status402PaymentRequired);

        // Only protect the ruler the buyer saw: a coup in between must not inherit the shield.
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
    }
}

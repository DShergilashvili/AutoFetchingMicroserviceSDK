using Microsoft.EntityFrameworkCore;
using static PresidentsMap.Endpoints.Api;

namespace PresidentsMap.Endpoints;

/// <summary>The cabinet of one country-day. Whoever has the certificate link can fill or take a seat.</summary>
public static class CabinetEndpoints
{
    private record AppointRequest(string? Post, string? Name, int? Price, string? Title);

    public static void MapCabinet(this WebApplication app)
    {
        app.MapGet("/api/gifts/{id}/cabinet", async (string id, HttpRequest http, AppDb db) =>
            await db.Gifts.FindAsync(id) is { } g ? Results.Ok(await View(db, g, Loc.Of(http))) : Results.NotFound());

        app.MapPost("/api/gifts/{id}/cabinet", Appoint).RequireRateLimiting(BuyLimit);
        app.MapPost("/api/gifts/{id}/cabinet/bundle", BuyBundle).RequireRateLimiting(BuyLimit);
    }

    private static async Task<IResult> Appoint(string id, AppointRequest req, HttpRequest http, AppDb db,
        IPaymentGateway payments, CancellationToken ct)
    {
        if (await db.Gifts.FindAsync([id], ct) is not { } g) return Results.NotFound();
        var name = (req.Name ?? "").Trim();
        if (name.Length < 2 || name.Length > Limits.Name) return Problem(http, "ჩაწერე სახელი (2-დან 40 სიმბოლომდე).");

        const string changedText = "სანამ იხდიდი, ეს სკამი სხვამ დაიკავა და ფასი შეიცვალა. ნახე ახალი ფასი.";
        var key = req.Post ?? "";
        var customTitle = "";
        int basePrice;

        if (key == "custom")
        {
            // A ministry the buyer invents. It takes the next free custom slot.
            customTitle = (req.Title ?? "").Trim();
            if (customTitle.Length < 2 || customTitle.Length > Limits.PostTitle) return Problem(http, "ჩაწერე სამინისტროს სახელი (2-დან 30 სიმბოლომდე).");
            var used = await db.Appointments.Where(a => a.GiftId == id && a.Post.StartsWith(Cabinet.CustomPrefix)).Select(a => a.Post).ToListAsync(ct);
            if (used.Count >= Cabinet.CustomLimit) return Problem(http, "საკუთარი სამინისტროების ლიმიტი ამოიწურა.", StatusCodes.Status409Conflict);
            key = Enumerable.Range(1, Cabinet.CustomLimit).Select(i => Cabinet.CustomPrefix + i).First(k => !used.Contains(k));
            basePrice = Cabinet.CustomPrice;
        }
        else if (Cabinet.Find(key) is { } post)
        {
            basePrice = post.Price;
        }
        else if (key.StartsWith(Cabinet.CustomPrefix) && await db.Appointments.AnyAsync(a => a.GiftId == id && a.Post == key, ct))
        {
            basePrice = Cabinet.CustomPrice;   // a coup against an existing custom ministry
        }
        else
        {
            return Problem(http, "ასეთი თანამდებობა არ არსებობს.");
        }

        var seat = await db.Appointments.FirstOrDefaultAsync(a => a.GiftId == id && a.Post == key, ct);
        var isCustom = key.StartsWith(Cabinet.CustomPrefix);

        // Empty seat: a plain appointment. Occupied seat: a coup, which always costs money.
        var price = seat is not null ? Pricing.CoupPrice(seat.Worth) : g.CabinetPrepaid && !isCustom ? 0 : basePrice;
        if (req.Price != price) return Problem(http, changedText, StatusCodes.Status409Conflict);

        var reference = "prepaid";
        if (price > 0)
        {
            var payment = await payments.PayAsync(price, $"Presidents Map {g.Number} {key}", ApprovedOrder(http), ct);
            if (payment.PendingOrderId is not null) return AwaitApproval(payment);
            if (!payment.Ok) return Problem(http, "გადახდა ვერ შესრულდა.", StatusCodes.Status402PaymentRequired);
            reference = payment.Reference;
        }

        if (seat is null)
        {
            db.Appointments.Add(new Appointment
            {
                GiftId = id, Post = key, CustomTitle = customTitle, Name = name, Price = price, Worth = basePrice,
                TotalPaid = price, PaymentRef = reference, CreatedAt = DateTime.UtcNow,
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
        return Results.Ok(await View(db, g, Loc.Of(http)));
    }

    private static async Task<IResult> BuyBundle(string id, HttpRequest http, AppDb db, IPaymentGateway payments, CancellationToken ct)
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
        return Results.Ok(await View(db, g, Loc.Of(http)));
    }

    private static async Task<object> View(AppDb db, Gift g, string lang)
    {
        var seats = await db.Appointments.Where(a => a.GiftId == g.Id).ToDictionaryAsync(a => a.Post);
        var empty = Cabinet.Posts.Where(p => !seats.ContainsKey(p.Key)).ToList();
        var custom = seats.Values.Where(a => a.Post.StartsWith(Cabinet.CustomPrefix)).OrderBy(a => a.Post).ToList();

        // price = what the next action on this seat costs: appointing if empty, a coup if occupied.
        var standard = Cabinet.Posts.Select(p =>
        {
            var seat = seats.GetValueOrDefault(p.Key);
            return new
            {
                key = p.Key, title = Loc.T(lang, p.Title), name = seat?.Name, coups = seat?.Coups ?? 0,
                price = seat is null ? (g.CabinetPrepaid ? 0 : p.Price) : Pricing.CoupPrice(seat.Worth),
            };
        });
        var invented = custom.Select(a => new
        {
            key = a.Post, title = a.CustomTitle, name = (string?)a.Name, coups = a.Coups, price = Pricing.CoupPrice(a.Worth),
        });
        return new
        {
            prepaid = g.CabinetPrepaid,
            bundlePrice = Cabinet.BundlePrice(empty),
            fullPrice = empty.Sum(p => p.Price),
            posts = standard.Concat(invented),
            customPrice = Cabinet.CustomPrice,
            customLeft = Cabinet.CustomLimit - custom.Count,
        };
    }
}

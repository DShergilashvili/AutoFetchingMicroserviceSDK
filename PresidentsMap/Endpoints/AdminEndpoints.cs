using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace PresidentsMap.Endpoints;

/// <summary>
/// Moderation for the site owner. Every call must carry the key from the Admin:Key setting
/// in the X-Admin-Key header; with no key configured the whole area answers 404.
/// </summary>
public static class AdminEndpoints
{
    private record TestEmailRequest(string? To);

    public static void MapAdmin(this WebApplication app)
    {
        var admin = app.MapGroup("/api/admin").AddEndpointFilter(async (ctx, next) =>
        {
            var expected = ctx.HttpContext.RequestServices.GetRequiredService<IConfiguration>()["Admin:Key"];
            if (string.IsNullOrEmpty(expected)) return Results.NotFound();
            var given = ctx.HttpContext.Request.Headers["X-Admin-Key"].FirstOrDefault() ?? "";
            var same = CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(given), Encoding.UTF8.GetBytes(expected));
            return same ? await next(ctx) : Results.Unauthorized();
        });

        // What is switched on, so the owner can see at a glance whether the site takes real money and sends real mail.
        admin.MapGet("/status", (IConfiguration config, IPaymentGateway payments, IEmailSender sender) => Results.Ok(new
        {
            payments = payments is not PayPalGateway ? "demo" : config.GetValue("PayPal:Sandbox", true) ? "paypal-sandbox" : "paypal-live",
            email = sender is not LogEmailSender,
            siteUrl = config["Site:Url"] ?? "",
        }));

        // Sends one message right now and reports the mail server's answer, to check the mail settings.
        admin.MapPost("/test-email", async (TestEmailRequest req, IEmailSender sender, CancellationToken ct) =>
        {
            if (sender is LogEmailSender) return Results.Json(new { error = "ფოსტის სერვერი არ არის მითითებული (SMTP_HOST)." }, statusCode: StatusCodes.Status409Conflict);
            if (!Api.TryEmail(req.To, out var to) || to.Length == 0) return Results.Json(new { error = "ელფოსტის მისამართი არასწორია." }, statusCode: StatusCodes.Status400BadRequest);
            try
            {
                await sender.SendAsync(new Email(to, "Presidents Map: test", "ეს სატესტო წერილია. ფოსტის პარამეტრები მუშაობს.\n\nThis is a test message. The mail settings work."), ct);
                return Results.NoContent();
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                return Results.Json(new { error = e.Message }, statusCode: StatusCodes.Status502BadGateway);
            }
        });

        admin.MapGet("/gifts", async (AppDb db) =>
        {
            var gifts = await db.Gifts.OrderByDescending(g => g.CreatedAt).Take(200).ToListAsync();
            var ids = gifts.Select(g => g.Id).ToList();
            var seats = await db.Appointments.Where(a => ids.Contains(a.GiftId)).ToListAsync();
            return Results.Ok(gifts.Select(g => new
            {
                g.Id, g.Country, g.Date, g.To, g.From, g.Message, g.Title, g.Price, g.TotalPaid, g.CabinetPaid, g.Coups, g.Immune,
                g.BuyerEmail, g.RecipientEmail, g.PaymentRef, g.CreatedAt,
                seats = seats.Where(a => a.GiftId == g.Id).OrderBy(a => a.Post)
                    .Select(a => new { a.Id, a.Post, a.CustomTitle, a.Name, a.TotalPaid, a.Coups, a.PaymentRef }),
            }));
        });

        // Removing frees the day or the seat again. Money is not touched: refund in the payment provider if needed.
        admin.MapDelete("/gifts/{id}", async (string id, AppDb db) =>
        {
            await db.Appointments.Where(a => a.GiftId == id).ExecuteDeleteAsync();
            return await db.Gifts.Where(g => g.Id == id).ExecuteDeleteAsync() == 1 ? Results.NoContent() : Results.NotFound();
        });

        admin.MapDelete("/seats/{seatId:int}", async (int seatId, AppDb db) =>
            await db.Appointments.Where(a => a.Id == seatId).ExecuteDeleteAsync() == 1 ? Results.NoContent() : Results.NotFound());

        admin.MapPost("/gifts/{id}/clear-message", async (string id, AppDb db) =>
            await db.Gifts.Where(g => g.Id == id).ExecuteUpdateAsync(u => u.SetProperty(g => g.Message, "")) == 1
                ? Results.NoContent() : Results.NotFound());
    }
}

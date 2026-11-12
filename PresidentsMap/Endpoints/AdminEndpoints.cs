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

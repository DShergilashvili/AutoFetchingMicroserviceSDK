using Microsoft.EntityFrameworkCore;
using static PresidentsMap.Endpoints.Api;

namespace PresidentsMap.Endpoints;

public static class PassportEndpoints
{
    private record PassportRequest(string? Email, string? Lang);

    public static void MapPassport(this WebApplication app)
    {
        // Always answers the same way, so nobody can use it to learn whether an address has bought anything.
        app.MapPost("/api/passport", async (PassportRequest req, HttpRequest http, AppDb db, Signer signer, Mailer mailer, IConfiguration config) =>
        {
            if (!TryEmail(req.Email, out var email) || email.Length == 0) return Problem(http, "ელფოსტის მისამართი არასწორია.");
            email = email.ToLowerInvariant();
            if (await db.Reigns.AnyAsync(r => r.BuyerEmail == email || r.RecipientEmail == email))
                mailer.Passport(email, signer.Link(Origin(http, config), email, Loc.Norm(req.Lang)), Loc.Norm(req.Lang));
            return Results.NoContent();
        }).RequireRateLimiting(BuyLimit);

        app.MapGet("/passport", async (string? e, string? t, string? lang, AppDb db, CountryCatalog catalog, Signer signer) =>
        {
            if (string.IsNullOrEmpty(e) || !signer.Verify(e, t)) return Results.NotFound();
            return Results.Content(await PassportPage.RenderAsync(db, catalog, e, Loc.Norm(lang)), "text/html; charset=utf-8");
        });
    }
}

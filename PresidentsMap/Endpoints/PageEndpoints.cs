namespace PresidentsMap.Endpoints;

/// <summary>Server-rendered pages: the certificate and its share image.</summary>
public static class PageEndpoints
{
    public static void MapPages(this WebApplication app)
    {
        app.MapGet("/healthz", () => Results.Ok(new { ok = true }));

        // A certificate is shown in the language it was bought in; ?lang= overrides that.
        app.MapGet("/cert/{id}", async (string id, string? lang, HttpRequest http, AppDb db, CountryCatalog catalog, IConfiguration config) =>
        {
            if (await db.Gifts.FindAsync(id) is not { } g) return Results.NotFound();
            var l = lang is null ? g.Lang : Loc.Norm(lang);
            var html = CertificatePage.Render(g, catalog.Find(g.Country)?.Name(l) ?? g.Country, Api.Origin(http, config), l);
            return Results.Content(html, "text/html; charset=utf-8");
        });

        app.MapGet("/cert/{id}/card.png", async (string id, string? lang, HttpResponse res, AppDb db, CountryCatalog catalog, CardRenderer cards) =>
        {
            if (await db.Gifts.FindAsync(id) is not { } g) return Results.NotFound();
            var l = lang is null ? g.Lang : Loc.Norm(lang);
            // Short cache: a coup or a new title changes the picture.
            res.Headers.CacheControl = "public, max-age=300";
            return Results.File(cards.Render(g, catalog.Find(g.Country)?.Name(l) ?? g.Country, l), "image/png");
        });
    }
}

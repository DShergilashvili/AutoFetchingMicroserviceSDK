using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace PresidentsMap;

/// <summary>
/// Signs passport links. A passport is opened only by a link sent to the email address itself,
/// so knowing someone's address is not enough to see what they bought.
/// </summary>
public class Signer
{
    private byte[] _secret = [];

    /// <summary>Uses Site:Secret when configured; otherwise a random secret created once and kept in the database.</summary>
    public async Task InitAsync(AppDb db, IConfiguration config)
    {
        var secret = config["Site:Secret"];
        if (string.IsNullOrEmpty(secret))
        {
            var row = await db.Settings.FindAsync("secret");
            if (row is null)
            {
                row = new Setting { Key = "secret", Value = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)) };
                db.Settings.Add(row);
                await db.SaveChangesAsync();
            }
            secret = row.Value;
        }
        _secret = Encoding.UTF8.GetBytes(secret);
    }

    public string Token(string email) =>
        Convert.ToHexString(HMACSHA256.HashData(_secret, Encoding.UTF8.GetBytes(email.Trim().ToLowerInvariant())))[..32].ToLowerInvariant();

    public bool Verify(string email, string? token) =>
        token is not null && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(Token(email)), Encoding.UTF8.GetBytes(token));

    public string Link(string origin, string email, string lang) =>
        $"{origin}/passport?e={Uri.EscapeDataString(email.Trim().ToLowerInvariant())}&t={Token(email)}&lang={lang}";
}

/// <summary>One line of a passport: a reign and what has happened to it since.</summary>
public record PassportEntry(Reign Reign, Gift? Current);

public static class PassportPage
{
    public static async Task<string> RenderAsync(AppDb db, CountryCatalog catalog, string email, string lang)
    {
        email = email.Trim().ToLowerInvariant();
        var reigns = await db.Reigns.Where(r => r.RecipientEmail == email || r.BuyerEmail == email).OrderByDescending(r => r.Date).ToListAsync();
        var ids = reigns.Select(r => r.GiftId).Distinct().ToList();
        var gifts = await db.Gifts.Where(g => ids.Contains(g.Id)).ToDictionaryAsync(g => g.Id);

        string E(string s) => WebUtility.HtmlEncode(s);
        string T(string ka) => Loc.T(lang, ka);
        var ruled = reigns.Where(r => r.RecipientEmail == email).ToList();
        var gifted = reigns.Where(r => r.BuyerEmail == email && r.RecipientEmail != email).ToList();
        var countries = reigns.Select(r => r.Country).Distinct().Count();

        string Row(Reign r)
        {
            var name = catalog.Find(r.Country)?.Name(lang) ?? r.Country;
            var now = gifts.GetValueOrDefault(r.GiftId);
            var what = $"<div class=\"t\"><span>{E(name)}</span><small>{E(Loc.Date(lang, r.Date))} · {E(r.To)} · {E(Titles.Name(r.Title, lang))}</small></div>";
            if (now is null) return $"<li>{what}</li>";
            if (r.EndedAt is null) return $"<li>{what}<a class=\"btn ghost\" href=\"/cert/{E(now.Id)}\">{E(T("სერტიფიკატი"))}</a></li>";
            // Overthrown: offer to take it back, unless the usurper bought immunity.
            var back = now.Immune
                ? $"<small>{E(T("ჩამოგდებულია. ახალი მმართველი დაცულია."))}</small>"
                : $"<a class=\"btn coup\" href=\"/?c={E(r.Country)}&amp;d={r.Date:yyyy-MM-dd}\">{E(T("დაიბრუნე"))} · ${Pricing.CoupPrice(now.Price)}</a>";
            return $"<li>{what}{back}</li>";
        }

        string Section(string heading, List<Reign> list) => list.Count == 0 ? "" :
            $"<section class=\"cabinet\"><h2>{E(heading)}</h2><ul class=\"posts\">{string.Concat(list.Select(Row))}</ul></section>";

        var empty = reigns.Count == 0 ? $"<p class=\"empty\">{E(T("ამ მისამართით ტიტულები ჯერ არ არის. ყიდვისას ჩაწერე ელფოსტა და აქ გამოჩნდება."))}</p>" : "";

        return $$"""
        <!doctype html>
        <html lang="{{lang}}" data-lang="{{lang}}">
        <head>
        <meta charset="utf-8">
        <meta name="viewport" content="width=device-width,initial-scale=1">
        <meta name="robots" content="noindex">
        <title>{{E(T("პასპორტი"))}} · {{E(T("პრეზიდენტების რუკა"))}}</title>
        <link rel="stylesheet" href="https://fonts.googleapis.com/css2?family=Noto+Sans+Georgian:wght@400;600&family=Noto+Serif+Georgian:wght@700&display=swap">
        <link rel="stylesheet" href="/app.css">
        </head>
        <body>
        <main class="certpage">
          <div class="cert">
            <div class="k">{{E(T("პასპორტი"))}}</div>
            <div class="who">{{countries}}</div>
            <div class="land">{{E(T("ქვეყანა ამ პასპორტში"))}}</div>
            <div class="no">{{E(email)}}</div>
          </div>
          {{empty}}
          {{Section(T("შენ მართავდი"), ruled)}}
          {{Section(T("შენ აჩუქე"), gifted)}}
          <a class="btn" href="/">{{E(T("დაამატე ახალი ქვეყანა"))}}</a>
        </main>
        </body>
        </html>
        """;
    }
}

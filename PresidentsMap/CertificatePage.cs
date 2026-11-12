using System.Net;

namespace PresidentsMap;

/// <summary>Server-rendered certificate page. Crawlers read the og: tags here to build the share card.</summary>
public static class CertificatePage
{
    public static string Render(Gift g, string countryName, string baseUrl, string lang)
    {
        string E(string s) => WebUtility.HtmlEncode(s);
        string T(string ka) => Loc.T(lang, ka);

        // The plain link shows the gift's own language; a switched view keeps its choice in the link.
        var query = lang == g.Lang ? "" : "?lang=" + lang;
        var url = $"{baseUrl}/cert/{g.Id}{query}";
        var image = $"{baseUrl}/cert/{g.Id}/card.png{query}";
        var other = lang == "en" ? "ka" : "en";
        var date = Loc.Date(lang, g.Date);
        var giver = T("გამჩუქებელი") + (lang == "en" ? " " : ": ") + g.From;

        var royal = g.Title != Titles.Default;
        var heading = royal ? T("საპატიო ტიტულის სერტიფიკატი") : T("საპატიო პრეზიდენტის სერტიფიკატი");
        var title = $"{g.To} · {(royal ? Titles.Name(g.Title, lang) : T("საპატიო პრეზიდენტი"))}";
        var desc = $"{T("ქვეყანა")}: {countryName}. {T("მმართველობის დღე")}: {date}.";
        var share = Uri.EscapeDataString(string.Format(T("საჩუქარი: {0} ერთი დღით პრეზიდენტია. ქვეყანა: {1}, დღე: {2}."), g.To, countryName, date));
        var u = Uri.EscapeDataString(url);

        return $$"""
        <!doctype html>
        <html lang="{{lang}}" data-lang="{{lang}}">
        <head>
        <meta charset="utf-8">
        <meta name="viewport" content="width=device-width,initial-scale=1">
        <title>{{E(title)}}</title>
        <meta name="description" content="{{E(desc)}}">
        <meta property="og:type" content="website">
        <meta property="og:site_name" content="{{E(T("პრეზიდენტების რუკა"))}}">
        <meta property="og:title" content="{{E(title)}}">
        <meta property="og:description" content="{{E(desc)}}">
        <meta property="og:url" content="{{E(url)}}">
        <meta property="og:image" content="{{E(image)}}">
        <meta property="og:image:width" content="1200">
        <meta property="og:image:height" content="630">
        <meta name="twitter:card" content="summary_large_image">
        <link rel="stylesheet" href="https://fonts.googleapis.com/css2?family=Noto+Sans+Georgian:wght@400;600&family=Noto+Serif+Georgian:wght@700&display=swap">
        <link rel="stylesheet" href="/app.css">
        </head>
        <body>
        <main class="certpage">
          <a class="langlink" href="/cert/{{E(g.Id)}}?lang={{other}}">{{(other == "en" ? "English" : "ქართული")}}</a>
          <div class="cert">
            <div class="k">{{E(heading)}}</div>
            <div class="no">№ {{E(g.Number)}}</div>
            <div class="who">{{E(g.To)}}</div>
            {{(royal ? $"<div class=\"land\">{E(T("ტიტული"))}: {E(Titles.Name(g.Title, lang))}</div>" : "")}}
            <div class="land">{{E(T("ქვეყანა"))}}: {{E(countryName)}}</div>
            <div>{{E(T("მმართველობის დღე"))}}: {{E(date)}}</div>
            {{(g.Message.Length > 0 ? $"<div class=\"motto\">{(lang == "en" ? "“" : "„")}{E(g.Message)}{(lang == "en" ? "”" : "“")}</div>" : "")}}
            {{(g.From.Length > 0 ? $"<div>{E(giver)}</div>" : "")}}
            {{(g.Coups > 0 ? $"<div class=\"no\">{E(T("გადატრიალება"))} №{g.Coups}</div>" : "")}}
            {{(g.Immune ? $"<div class=\"no\">{E(T("დაცულია გადატრიალებისგან"))}</div>" : "")}}
            <div class="foot">{{E(T("სიმბოლური ტიტული. არ იძლევა არანაირ იურიდიულ ან პოლიტიკურ უფლებას."))}}</div>
          </div>
          <div class="row">
            <button class="btn ghost" type="button" onclick="window.print()">{{E(T("სერტიფიკატის ბეჭდვა"))}}</button>
            <a class="btn ghost" target="_blank" rel="noopener noreferrer" href="https://www.facebook.com/sharer/sharer.php?u={{u}}">Facebook</a>
            <a class="btn ghost" target="_blank" rel="noopener noreferrer" href="https://twitter.com/intent/tweet?text={{share}}&amp;url={{u}}">X</a>
            <a class="btn ghost" target="_blank" rel="noopener noreferrer" href="https://wa.me/?text={{share}}%20{{u}}">WhatsApp</a>
            <a class="btn ghost" target="_blank" rel="noopener noreferrer" href="https://t.me/share/url?url={{u}}&amp;text={{share}}">Telegram</a>
          </div>
          {{(g.Immune ? "" : $"<div class=\"bundle\" id=\"immunity\" data-price=\"{Pricing.ImmunityPrice(g.Price)}\"><div><strong>{E(T("იმუნიტეტი"))} · ${Pricing.ImmunityPrice(g.Price)}</strong><div class=\"small\">{E(T("დაიცავი პრეზიდენტი: ამ დღეს მას ვეღარავინ ჩამოაგდებს."))}</div></div><span id=\"immunity-act\"></span></div><a class=\"btn coup\" href=\"/?c={E(g.Country)}&amp;d={g.Date:yyyy-MM-dd}\">{E(T("წაართვი პრეზიდენტობა"))} · ${Pricing.CoupPrice(g.Price)}</a>")}}
          <section class="cabinet" id="cabinet" data-id="{{E(g.Id)}}" aria-live="polite"></section>
          <a class="btn ghost" href="/">{{E(T("აჩუქე შენც ქვეყანა"))}}</a>
          <footer class="legal"><a href="/legal.html#terms">{{E(T("წესები"))}}</a><a href="/legal.html#privacy">{{E(T("კონფიდენციალურობა"))}}</a><a href="/legal.html#refunds">{{E(T("თანხის დაბრუნება"))}}</a></footer>
        </main>
        <script src="/i18n.js"></script>
        <script src="/pay.js"></script>
        <script src="/cert.js"></script>
        </body>
        </html>
        """;
    }
}

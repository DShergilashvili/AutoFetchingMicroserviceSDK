using System.Net;

namespace PresidentsMap;

/// <summary>Server-rendered certificate page. Crawlers read the og: tags here to build the share card.</summary>
public static class CertificatePage
{
    public static string Render(Gift g, string countryName, string baseUrl)
    {
        string E(string s) => WebUtility.HtmlEncode(s);
        var url = $"{baseUrl}/cert/{g.Id}";
        var title = $"{g.To} · საპატიო პრეზიდენტი";
        var desc = $"ქვეყანა: {countryName}. მმართველობის დღე: {Ka.Date(g.Date)}.";
        var share = Uri.EscapeDataString($"საჩუქარი: {g.To} ერთი დღით პრეზიდენტია. ქვეყანა: {countryName}, დღე: {Ka.Date(g.Date)}.");
        var u = Uri.EscapeDataString(url);

        return $$"""
        <!doctype html>
        <html lang="ka">
        <head>
        <meta charset="utf-8">
        <meta name="viewport" content="width=device-width,initial-scale=1">
        <title>{{E(title)}}</title>
        <meta name="description" content="{{E(desc)}}">
        <meta property="og:type" content="website">
        <meta property="og:title" content="{{E(title)}}">
        <meta property="og:description" content="{{E(desc)}}">
        <meta property="og:url" content="{{E(url)}}">
        <meta property="og:image" content="{{E(url)}}/card.png">
        <meta property="og:image:width" content="1200">
        <meta property="og:image:height" content="630">
        <meta name="twitter:card" content="summary_large_image">
        <link rel="stylesheet" href="https://fonts.googleapis.com/css2?family=Noto+Sans+Georgian:wght@400;600&family=Noto+Serif+Georgian:wght@700&display=swap">
        <link rel="stylesheet" href="/app.css">
        </head>
        <body>
        <main class="certpage">
          <div class="cert">
            <div class="k">საპატიო პრეზიდენტის სერტიფიკატი</div>
            <div class="no">№ {{E(g.Number)}}</div>
            <div class="who">{{E(g.To)}}</div>
            <div class="land">ქვეყანა: {{E(countryName)}}</div>
            <div>მმართველობის დღე: {{E(Ka.Date(g.Date))}}</div>
            {{(g.Message.Length > 0 ? $"<div class=\"motto\">„{E(g.Message)}“</div>" : "")}}
            {{(g.From.Length > 0 ? $"<div>გამჩუქებელი: {E(g.From)}</div>" : "")}}
            <div class="foot">სიმბოლური ტიტული. არ იძლევა არანაირ იურიდიულ ან პოლიტიკურ უფლებას.</div>
          </div>
          <div class="row">
            <a class="btn ghost" target="_blank" rel="noopener noreferrer" href="https://www.facebook.com/sharer/sharer.php?u={{u}}">Facebook</a>
            <a class="btn ghost" target="_blank" rel="noopener noreferrer" href="https://twitter.com/intent/tweet?text={{share}}&amp;url={{u}}">X</a>
            <a class="btn ghost" target="_blank" rel="noopener noreferrer" href="https://wa.me/?text={{share}}%20{{u}}">WhatsApp</a>
            <a class="btn ghost" target="_blank" rel="noopener noreferrer" href="https://t.me/share/url?url={{u}}&amp;text={{share}}">Telegram</a>
          </div>
          <a class="btn" href="/">აჩუქე შენც ქვეყანა</a>
        </main>
        </body>
        </html>
        """;
    }
}

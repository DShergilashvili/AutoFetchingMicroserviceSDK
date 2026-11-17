using System.Net;
using System.Text.RegularExpressions;
using static PresidentsMap.Tests.Http;

namespace PresidentsMap.Tests;

/// <summary>Decrees ($3), diplomatic notes ($2) and the passport.</summary>
public class ExtrasTests(SiteFactory site) : IClassFixture<SiteFactory>
{
    private readonly HttpClient _http = site.CreateClient();

    [Fact]
    public async Task A_decree_at_checkout_adds_three_dollars_and_shows_on_the_certificate()
    {
        var (cheap, _) = await _http.PostAsync("/api/gifts", new { country = "GE", date = Day(300), to = "Sopho", decree = "All coffee is free today", price = 10 });
        var (status, gift) = await _http.PostAsync("/api/gifts", new { country = "GE", date = Day(300), to = "Sopho", decree = "All coffee is free today", price = 13 });

        Assert.Equal(409, cheap);
        Assert.Equal(201, status);
        Assert.Contains("All coffee is free today", await _http.GetStringAsync("/cert/" + gift.GetProperty("id").GetString()));
    }

    [Fact]
    public async Task A_decree_can_be_issued_later_changed_for_money_and_is_wiped_by_a_coup()
    {
        var (_, gift) = await _http.PostAsync("/api/gifts", new { country = "GE", date = Day(301), to = "Sopho", price = 10 });
        var id = gift.GetProperty("id").GetString();

        var (first, _) = await _http.PostAsync($"/api/gifts/{id}/decree", new { text = "Mondays are cancelled", price = 3 });
        var (second, changed) = await _http.PostAsync($"/api/gifts/{id}/decree", new { text = "Naps are mandatory", price = 3 });
        var (tooShort, _) = await _http.PostAsync($"/api/gifts/{id}/decree", new { text = "a", price = 3 });
        var (_, afterCoup) = await _http.PostAsync("/api/gifts", new { country = "GE", date = Day(301), to = "Alex", price = 15 });

        Assert.Equal(200, first);
        Assert.Equal(200, second);
        Assert.Equal("Naps are mandatory", changed.GetProperty("decree").GetString());
        Assert.Equal(400, tooShort);
        Assert.Equal("", afterCoup.GetProperty("decree").GetString());
    }

    [Fact]
    public async Task A_note_costs_two_dollars_is_listed_and_the_owner_is_emailed()
    {
        var (_, gift) = await _http.PostAsync("/api/gifts", new { country = "GE", date = Day(302), to = "Sopho", email = "noted@example.com", lang = "en", price = 10 });
        var id = gift.GetProperty("id").GetString();

        var (wrong, _) = await _http.PostAsync($"/api/gifts/{id}/notes", new { from = "President of Italy", text = "Pizza summit on Friday?", price = 1 });
        var (status, notes) = await _http.PostAsync($"/api/gifts/{id}/notes", new { from = "President of Italy", text = "Pizza summit on Friday?", price = 2 });

        Assert.Equal(409, wrong);
        Assert.Equal(200, status);
        Assert.Equal("Pizza summit on Friday?", notes[0].GetProperty("text").GetString());
        for (var i = 0; i < 100 && !site.Sent.Any(m => m.Subject.Contains("diplomatic note")); i++) await Task.Delay(50);
        Assert.Contains(site.Sent, m => m.To == "noted@example.com" && m.Body.Contains("Pizza summit on Friday?"));
    }

    [Fact]
    public async Task The_passport_opens_only_with_the_emailed_link_and_offers_to_take_back_a_lost_country()
    {
        await _http.PostAsync("/api/gifts", new { country = "GE", date = Day(303), to = "Sopho", email = "collector@example.com", recipientEmail = "collector@example.com", lang = "en", price = 10 });
        await _http.PostAsync("/api/gifts", new { country = "GE", date = Day(304), to = "Sopho", email = "collector@example.com", recipientEmail = "collector@example.com", lang = "en", price = 10 });
        await _http.PostAsync("/api/gifts", new { country = "GE", date = Day(304), to = "Alex", price = 15 });   // the second one is overthrown

        var (asked, _) = await _http.PostAsync("/api/passport", new { email = "collector@example.com", lang = "en" });
        var (unknown, _) = await _http.PostAsync("/api/passport", new { email = "nobody@example.com", lang = "en" });
        Assert.Equal(204, asked);
        Assert.Equal(204, unknown);   // same answer: no way to probe which addresses have bought

        for (var i = 0; i < 100 && !site.Sent.Any(m => m.Subject.Contains("passport")); i++) await Task.Delay(50);
        var mail = site.Sent.Single(m => m.Subject.Contains("passport"));
        Assert.Equal("collector@example.com", mail.To);
        var link = Regex.Match(mail.Body, @"https://example\.test(/passport\S+)").Groups[1].Value;

        var page = await _http.GetStringAsync(link);
        Assert.Contains("collector@example.com", page);
        Assert.Contains("Take it back", page);
        Assert.Contains("$23", page);

        Assert.Equal(HttpStatusCode.NotFound, (await _http.GetAsync("/passport?e=collector@example.com&t=00000000000000000000000000000000")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _http.GetAsync("/passport?e=collector@example.com")).StatusCode);
    }
}

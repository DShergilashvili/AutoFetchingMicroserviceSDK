using System.Net;
using static PresidentsMap.Tests.Http;

namespace PresidentsMap.Tests;

public class PurchaseTests(SiteFactory site) : IClassFixture<SiteFactory>
{
    private readonly HttpClient _http = site.CreateClient();

    // Georgia's base price is $10; a coup is 1.5x; immunity is half the day's price (min $5); a royal title is +$10.

    [Fact]
    public async Task A_free_day_is_bought_at_the_base_price()
    {
        var (status, gift) = await _http.PostAsync("/api/gifts", new { country = "GE", date = Day(1), to = "Sopho", price = 10 });

        Assert.Equal(201, status);
        Assert.Equal("Sopho", gift.GetProperty("to").GetString());
        var day = await _http.GetJsonAsync("/api/days/" + Day(1));
        Assert.Equal("Sopho", day.GetProperty("gifts")[0].GetProperty("to").GetString());
        Assert.Equal(15, day.GetProperty("gifts")[0].GetProperty("coupPrice").GetInt32());
    }

    [Fact]
    public async Task A_wrong_price_is_refused_and_nothing_is_sold()
    {
        var (status, _) = await _http.PostAsync("/api/gifts", new { country = "GE", date = Day(2), to = "Sopho", price = 1 });

        Assert.Equal(409, status);
        Assert.Equal(0, (await _http.GetJsonAsync("/api/days/" + Day(2))).GetProperty("gifts").GetArrayLength());
    }

    [Theory]
    [InlineData("ZZ", "Sopho", 400)]   // no such country
    [InlineData("GE", "S", 400)]       // name too short
    public async Task Bad_input_is_rejected(string country, string to, int expected)
    {
        var (status, _) = await _http.PostAsync("/api/gifts", new { country, date = Day(3), to, price = 10 });
        Assert.Equal(expected, status);
    }

    [Fact]
    public async Task A_past_date_is_rejected()
    {
        var (status, _) = await _http.PostAsync("/api/gifts", new { country = "GE", date = "2020-01-01", to = "Sopho", price = 10 });
        Assert.Equal(400, status);
    }

    [Fact]
    public async Task A_coup_costs_half_more_replaces_the_ruler_and_keeps_the_same_certificate()
    {
        var (_, first) = await _http.PostAsync("/api/gifts", new { country = "GE", date = Day(4), to = "Sopho", price = 10 });

        var (cheap, _) = await _http.PostAsync("/api/gifts", new { country = "GE", date = Day(4), to = "Alex", price = 10 });
        var (status, second) = await _http.PostAsync("/api/gifts", new { country = "GE", date = Day(4), to = "Alex", price = 15 });

        Assert.Equal(409, cheap);
        Assert.Equal(201, status);
        Assert.Equal(first.GetProperty("id").GetString(), second.GetProperty("id").GetString());
        Assert.Equal("Alex", second.GetProperty("to").GetString());
        Assert.Equal(1, second.GetProperty("coups").GetInt32());
    }

    [Fact]
    public async Task Immunity_adds_to_the_price_and_blocks_coups()
    {
        var (status, gift) = await _http.PostAsync("/api/gifts", new { country = "GE", date = Day(5), to = "Sopho", price = 15, immune = true });
        Assert.Equal(201, status);
        Assert.True(gift.GetProperty("immune").GetBoolean());

        var (coup, _) = await _http.PostAsync("/api/gifts", new { country = "GE", date = Day(5), to = "Alex", price = 15 });
        Assert.Equal(409, coup);
    }

    [Fact]
    public async Task Immunity_can_be_added_later_from_the_certificate()
    {
        var (_, gift) = await _http.PostAsync("/api/gifts", new { country = "GE", date = Day(6), to = "Sopho", price = 10 });
        var id = gift.GetProperty("id").GetString();

        var (status, _) = await _http.PostAsync($"/api/gifts/{id}/immunity", new { price = 5 });
        var (again, _) = await _http.PostAsync($"/api/gifts/{id}/immunity", new { price = 5 });

        Assert.Equal(200, status);
        Assert.Equal(409, again);
    }

    [Fact]
    public async Task A_royal_title_costs_ten_dollars_more()
    {
        var (plain, _) = await _http.PostAsync("/api/gifts", new { country = "GE", date = Day(7), to = "Sopho", title = "queen", price = 10 });
        var (status, gift) = await _http.PostAsync("/api/gifts", new { country = "GE", date = Day(7), to = "Sopho", title = "queen", price = 20 });

        Assert.Equal(409, plain);
        Assert.Equal(201, status);
        Assert.Equal("queen", gift.GetProperty("title").GetString());
    }

    [Fact]
    public async Task The_buyer_gets_a_receipt_and_later_an_alert_when_overthrown()
    {
        await _http.PostAsync("/api/gifts", new { country = "GE", date = Day(8), to = "Sopho", email = "buyer@example.com", lang = "en", price = 10 });
        await _http.PostAsync("/api/gifts", new { country = "GE", date = Day(8), to = "Alex", price = 15 });

        var mail = (await site.WaitForEmailsAsync(2)).Where(m => m.To == "buyer@example.com").ToList();
        Assert.Contains(mail, m => m.Subject.StartsWith("Your gift is ready") && m.Body.Contains("https://example.test/cert/"));
        Assert.Contains(mail, m => m.Subject.Contains("overthrown") && m.Body.Contains("$23") && m.Body.Contains("https://example.test/?c=GE"));
    }

    [Fact]
    public async Task An_invalid_email_is_rejected()
    {
        var (status, _) = await _http.PostAsync("/api/gifts", new { country = "GE", date = Day(9), to = "Sopho", email = "not an email", price = 10 });
        Assert.Equal(400, status);
    }

    [Fact]
    public async Task Emails_and_messages_never_appear_in_public_listings()
    {
        await _http.PostAsync("/api/gifts", new { country = "GE", date = Day(10), to = "Sopho", email = "secret@example.com", message = "private words", price = 10 });

        var listing = await _http.GetStringAsync("/api/days/" + Day(10)) + await _http.GetStringAsync("/api/summary");
        Assert.Contains("Sopho", listing);   // the gift itself is listed
        Assert.DoesNotContain("secret@example.com", listing);
        Assert.DoesNotContain("private words", listing);
    }

    [Fact]
    public async Task The_certificate_page_escapes_what_people_type()
    {
        var (_, gift) = await _http.PostAsync("/api/gifts", new { country = "GE", date = Day(11), to = "<script>alert(1)</script>", price = 10 });

        var html = await _http.GetStringAsync("/cert/" + gift.GetProperty("id").GetString());
        Assert.DoesNotContain("<script>alert(1)</script>", html);
        Assert.Contains("&lt;script&gt;", html);
    }

    [Fact]
    public async Task The_share_image_is_a_png()
    {
        var (_, gift) = await _http.PostAsync("/api/gifts", new { country = "GE", date = Day(12), to = "Sopho", price = 10 });

        var res = await _http.GetAsync($"/cert/{gift.GetProperty("id").GetString()}/card.png");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("image/png", res.Content.Headers.ContentType?.MediaType);
    }
}

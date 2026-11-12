using static PresidentsMap.Tests.Http;

namespace PresidentsMap.Tests;

public class CabinetTests(SiteFactory site) : IClassFixture<SiteFactory>
{
    private readonly HttpClient _http = site.CreateClient();

    private async Task<string> NewGiftAsync(int day)
    {
        var (_, gift) = await _http.PostAsync("/api/gifts", new { country = "GE", date = Day(100 + day), to = "Sopho", price = 10 });
        return gift.GetProperty("id").GetString()!;
    }

    [Fact]
    public async Task A_seat_is_appointed_and_then_taken_for_more()
    {
        var id = await NewGiftAsync(1);

        var (appointed, _) = await _http.PostAsync($"/api/gifts/{id}/cabinet", new { post = "pm", name = "George", price = 3 });
        var (stale, _) = await _http.PostAsync($"/api/gifts/{id}/cabinet", new { post = "pm", name = "Sandro", price = 3 });
        var (taken, view) = await _http.PostAsync($"/api/gifts/{id}/cabinet", new { post = "pm", name = "Sandro", price = 5 });

        Assert.Equal(200, appointed);
        Assert.Equal(409, stale);
        Assert.Equal(200, taken);
        var pm = view.GetProperty("posts").EnumerateArray().First(p => p.GetProperty("key").GetString() == "pm");
        Assert.Equal("Sandro", pm.GetProperty("name").GetString());
        Assert.Equal(8, pm.GetProperty("price").GetInt32());
    }

    [Fact]
    public async Task The_whole_cabinet_costs_thirty_percent_less_and_makes_empty_seats_free()
    {
        var id = await NewGiftAsync(2);

        var (status, view) = await _http.PostAsync($"/api/gifts/{id}/cabinet/bundle", new { });   // 9 posts worth $11 -> $8
        var (free, _) = await _http.PostAsync($"/api/gifts/{id}/cabinet", new { post = "love", name = "Mariam", price = 0 });
        var (twice, _) = await _http.PostAsync($"/api/gifts/{id}/cabinet/bundle", new { });

        Assert.Equal(200, status);
        Assert.True(view.GetProperty("prepaid").GetBoolean());
        Assert.Equal(200, free);
        Assert.Equal(409, twice);
        Assert.Equal(10 + 8, (await _http.GetJsonAsync("/api/summary")).GetProperty("total").GetInt32() - await OtherRevenueAsync(id));
    }

    [Fact]
    public async Task A_custom_ministry_costs_three_dollars_and_there_can_be_five()
    {
        var id = await NewGiftAsync(3);

        for (var i = 1; i <= 5; i++)
        {
            var (ok, _) = await _http.PostAsync($"/api/gifts/{id}/cabinet", new { post = "custom", title = $"Minister of Pizza {i}", name = "Luka", price = 3 });
            Assert.Equal(200, ok);
        }
        var (sixth, _) = await _http.PostAsync($"/api/gifts/{id}/cabinet", new { post = "custom", title = "One too many", name = "Luka", price = 3 });
        var (coup, view) = await _http.PostAsync($"/api/gifts/{id}/cabinet", new { post = "custom-1", name = "Nina", price = 5 });

        Assert.Equal(409, sixth);
        Assert.Equal(200, coup);
        Assert.Equal(0, view.GetProperty("customLeft").GetInt32());
        Assert.Contains(view.GetProperty("posts").EnumerateArray(), p => p.GetProperty("title").GetString() == "Minister of Pizza 1" && p.GetProperty("name").GetString() == "Nina");
    }

    [Fact]
    public async Task An_unknown_post_is_rejected()
    {
        var id = await NewGiftAsync(4);
        var (status, _) = await _http.PostAsync($"/api/gifts/{id}/cabinet", new { post = "custom-4", name = "Nobody", price = 5 });
        Assert.Equal(400, status);
    }

    /// <summary>Revenue from the other tests sharing this database, so each test can check only its own.</summary>
    private async Task<int> OtherRevenueAsync(string mine)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, "/api/admin/gifts");
        req.Headers.Add("X-Admin-Key", SiteFactory.AdminKey);
        var gifts = System.Text.Json.JsonDocument.Parse(await (await _http.SendAsync(req)).Content.ReadAsStringAsync()).RootElement;
        return gifts.EnumerateArray().Where(g => g.GetProperty("id").GetString() != mine).Sum(g =>
            g.GetProperty("totalPaid").GetInt32() + g.GetProperty("cabinetPaid").GetInt32()
            + g.GetProperty("seats").EnumerateArray().Sum(s => s.GetProperty("totalPaid").GetInt32()));
    }
}

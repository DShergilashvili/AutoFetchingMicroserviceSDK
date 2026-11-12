using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static PresidentsMap.Tests.Http;

namespace PresidentsMap.Tests;

public class AdminAndDeliveryTests(SiteFactory site) : IClassFixture<SiteFactory>
{
    private readonly HttpClient _http = site.CreateClient();

    private async Task<HttpResponseMessage> AdminAsync(HttpMethod method, string path, string? key)
    {
        using var req = new HttpRequestMessage(method, "/api/admin" + path);
        if (key is not null) req.Headers.Add("X-Admin-Key", key);
        return await _http.SendAsync(req);
    }

    [Fact]
    public async Task Admin_needs_the_right_key()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await AdminAsync(HttpMethod.Get, "/gifts", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await AdminAsync(HttpMethod.Get, "/gifts", "wrong")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await AdminAsync(HttpMethod.Get, "/gifts", SiteFactory.AdminKey)).StatusCode);
    }

    [Fact]
    public async Task Removing_a_gift_frees_the_day_again()
    {
        var (_, gift) = await _http.PostAsync("/api/gifts", new { country = "TR", date = Day(200), to = "Offensive", price = 20 });
        var id = gift.GetProperty("id").GetString();
        await _http.PostAsync($"/api/gifts/{id}/cabinet", new { post = "pm", name = "George", price = 3 });

        var removed = await AdminAsync(HttpMethod.Delete, "/gifts/" + id, SiteFactory.AdminKey);
        var (again, _) = await _http.PostAsync("/api/gifts", new { country = "TR", date = Day(200), to = "Sopho", price = 20 });

        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        Assert.Equal(201, again);
    }

    [Fact]
    public async Task The_recipient_gets_the_certificate_once_when_the_day_comes()
    {
        // The gift day cannot be bought in the past, so buy for today and let the delivery pass run.
        var today = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");
        var (status, _) = await _http.PostAsync("/api/gifts", new
        {
            country = "IS", date = today, to = "Sopho", from = "Davit", message = "Happy day", recipientEmail = "sopho@example.com", lang = "en", price = 10,
        });
        Assert.Equal(201, status);

        var worker = site.Services.GetServices<Microsoft.Extensions.Hosting.IHostedService>().OfType<MailWorker>().Single();
        await worker.DeliverOnceAsync(default);
        await worker.DeliverOnceAsync(default);
        await site.WaitForEmailsAsync(1);
        await Task.Delay(200);

        var mail = site.Sent.Where(m => m.To == "sopho@example.com").ToList();
        Assert.Single(mail);
        Assert.Contains("Davit made you President for a day", mail[0].Body);
        Assert.Contains("Happy day", mail[0].Body);

        using var scope = site.Services.CreateScope();
        Assert.True(await scope.ServiceProvider.GetRequiredService<AppDb>().Gifts.AnyAsync(g => g.Country == "IS" && g.Delivered));
    }
}

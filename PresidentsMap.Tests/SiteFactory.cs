using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace PresidentsMap.Tests;

/// <summary>The real site in memory, with its own throwaway database and a mailbox that only records.</summary>
public class SiteFactory : WebApplicationFactory<Program>
{
    private readonly string _db = Path.Combine(Path.GetTempPath(), $"presidents-test-{Guid.NewGuid():N}.db");
    public ConcurrentQueue<Email> Sent { get; } = new();
    public const string AdminKey = "test-admin-key";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration(c => c.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] = $"Data Source={_db}",
            ["Admin:Key"] = AdminKey,
            ["Site:Url"] = "https://example.test",
        }));
        builder.ConfigureServices(s =>
        {
            s.RemoveAll<IEmailSender>();
            s.AddSingleton<IEmailSender>(new RecordingSender(Sent));
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        foreach (var f in new[] { _db, _db + "-shm", _db + "-wal" }) if (File.Exists(f)) File.Delete(f);
    }

    private sealed class RecordingSender(ConcurrentQueue<Email> sent) : IEmailSender
    {
        public Task SendAsync(Email email, CancellationToken ct) { sent.Enqueue(email); return Task.CompletedTask; }
    }

    /// <summary>Emails are sent in the background; wait until the expected number has arrived.</summary>
    public async Task<List<Email>> WaitForEmailsAsync(int count)
    {
        for (var i = 0; i < 100 && Sent.Count < count; i++) await Task.Delay(50);
        return Sent.ToList();
    }
}

public static class Http
{
    /// <summary>A date safely in the future, different for each test so they never collide.</summary>
    public static string Day(int offset) => DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30 + offset).ToString("yyyy-MM-dd");

    public static async Task<(int Status, JsonElement Body)> PostAsync(this HttpClient http, string url, object body)
    {
        var res = await http.PostAsJsonAsync(url, body);
        var text = await res.Content.ReadAsStringAsync();
        return ((int)res.StatusCode, text.Length > 0 ? JsonDocument.Parse(text).RootElement : default);
    }

    public static async Task<JsonElement> GetJsonAsync(this HttpClient http, string url) =>
        JsonDocument.Parse(await http.GetStringAsync(url)).RootElement;
}

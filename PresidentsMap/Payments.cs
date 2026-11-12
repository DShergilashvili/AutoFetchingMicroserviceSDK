using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace PresidentsMap;

/// <summary>
/// Outcome of one payment step. <see cref="PendingOrderId"/> set means the buyer still has to
/// approve the payment in their browser; the same purchase request is then sent again with that id.
/// </summary>
public record Payment(bool Ok, string Reference, string? PendingOrderId = null)
{
    public static readonly Payment Failed = new(false, "");
}

/// <summary>The only place money moves. Every purchase goes through <see cref="PayAsync"/>.</summary>
public interface IPaymentGateway
{
    /// <summary>Given to the browser so it can show the provider's pay button; null means demo mode.</summary>
    string? PublicClientId { get; }

    /// <param name="approvedOrderId">Order the buyer approved in the browser, or null on the first call.</param>
    Task<Payment> PayAsync(int amountUsd, string description, string? approvedOrderId, CancellationToken ct);

    Task RefundAsync(string reference, CancellationToken ct);
}

/// <summary>Approves everything and charges nothing.</summary>
public class DemoPaymentGateway : IPaymentGateway
{
    public string? PublicClientId => null;

    public Task<Payment> PayAsync(int amountUsd, string description, string? approvedOrderId, CancellationToken ct) =>
        Task.FromResult(new Payment(true, "demo-" + Guid.NewGuid().ToString("N")[..12]));

    public Task RefundAsync(string reference, CancellationToken ct) => Task.CompletedTask;
}

/// <summary>
/// PayPal Orders v2. First call creates an order for the exact price and hands its id to the browser.
/// After the buyer approves it there, the second call checks the order is for today's price and
/// captures the money. An order can be captured only once, so one payment cannot buy two things.
/// </summary>
public partial class PayPalGateway(HttpClient http, IConfiguration config, ILogger<PayPalGateway> log) : IPaymentGateway
{
    private readonly string _clientId = config["PayPal:ClientId"] ?? "";
    private readonly string _secret = config["PayPal:Secret"] ?? "";
    private readonly string _baseUrl = (config["PayPal:BaseUrl"] is { Length: > 0 } custom ? custom
        : config.GetValue("PayPal:Sandbox", true) ? "https://api-m.sandbox.paypal.com" : "https://api-m.paypal.com").TrimEnd('/');

    private readonly SemaphoreSlim _tokenLock = new(1, 1);
    private string? _token;
    private DateTime _tokenExpires;

    public string? PublicClientId => _clientId;

    [GeneratedRegex("^[A-Za-z0-9-]{8,40}$")]
    private static partial Regex IdPattern();

    public async Task<Payment> PayAsync(int amountUsd, string description, string? approvedOrderId, CancellationToken ct)
    {
        var amount = $"{amountUsd}.00";
        try
        {
            if (approvedOrderId is null)
            {
                var created = await SendAsync(HttpMethod.Post, "/v2/checkout/orders", new
                {
                    intent = "CAPTURE",
                    purchase_units = new[] { new { description, amount = new { currency_code = "USD", value = amount } } },
                }, ct);
                return created?.GetProperty("id").GetString() is { } id ? new Payment(false, "", id) : Payment.Failed;
            }

            if (!IdPattern().IsMatch(approvedOrderId)) return Payment.Failed;

            // The price may have changed since the order was created (a coup in between): never capture a stale amount.
            var order = await SendAsync(HttpMethod.Get, $"/v2/checkout/orders/{approvedOrderId}", null, ct);
            var unit = order?.GetProperty("purchase_units")[0].GetProperty("amount");
            if (unit?.GetProperty("value").GetString() != amount || unit?.GetProperty("currency_code").GetString() != "USD")
                return Payment.Failed;

            var captured = await SendAsync(HttpMethod.Post, $"/v2/checkout/orders/{approvedOrderId}/capture", new { }, ct);
            if (captured?.GetProperty("status").GetString() != "COMPLETED") return Payment.Failed;
            var captureId = captured.Value.GetProperty("purchase_units")[0].GetProperty("payments").GetProperty("captures")[0].GetProperty("id").GetString();
            return captureId is null ? Payment.Failed : new Payment(true, captureId);
        }
        catch (Exception e) when (e is HttpRequestException or JsonException or KeyNotFoundException or InvalidOperationException or TaskCanceledException)
        {
            log.LogWarning(e, "PayPal call failed");
            return Payment.Failed;
        }
    }

    public async Task RefundAsync(string reference, CancellationToken ct)
    {
        try
        {
            if (await SendAsync(HttpMethod.Post, $"/v2/payments/captures/{reference}/refund", new { }, ct) is null)
                log.LogError("PayPal refund of capture {Capture} was rejected; refund it by hand", reference);
        }
        catch (Exception e) when (e is HttpRequestException or JsonException or TaskCanceledException)
        {
            log.LogError(e, "PayPal refund of capture {Capture} failed; refund it by hand", reference);
        }
    }

    /// <summary>Returns the response body, or null when PayPal answered with an error status.</summary>
    private async Task<JsonElement?> SendAsync(HttpMethod method, string path, object? body, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(method, _baseUrl + path);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await TokenAsync(ct));
        if (body is not null) req.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        using var res = await http.SendAsync(req, ct);
        if (!res.IsSuccessStatusCode)
        {
            log.LogWarning("PayPal {Method} {Path} answered {Status}", method, path, (int)res.StatusCode);
            return null;
        }
        return await res.Content.ReadFromJsonAsync<JsonElement>(ct);
    }

    private async Task<string> TokenAsync(CancellationToken ct)
    {
        await _tokenLock.WaitAsync(ct);
        try
        {
            if (_token is not null && DateTime.UtcNow < _tokenExpires) return _token;
            using var req = new HttpRequestMessage(HttpMethod.Post, _baseUrl + "/v1/oauth2/token");
            req.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_clientId}:{_secret}")));
            req.Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["grant_type"] = "client_credentials" });
            using var res = await http.SendAsync(req, ct);
            res.EnsureSuccessStatusCode();
            var json = await res.Content.ReadFromJsonAsync<JsonElement>(ct);
            _token = json.GetProperty("access_token").GetString()!;
            _tokenExpires = DateTime.UtcNow.AddSeconds(json.GetProperty("expires_in").GetInt32() - 60);
            return _token;
        }
        finally
        {
            _tokenLock.Release();
        }
    }
}

using System.Net.Mail;
using System.Security.Cryptography;

namespace PresidentsMap.Endpoints;

/// <summary>Small helpers shared by every endpoint file.</summary>
public static class Api
{
    public const string BuyLimit = "buy";

    public static IResult Problem(HttpRequest http, string text, int status = StatusCodes.Status400BadRequest) =>
        Results.Json(new { error = Loc.T(Loc.Of(http), text) }, statusCode: status);

    /// <summary>The buyer's browser sends the same purchase twice: first bare, then with the order it approved.</summary>
    public static string? ApprovedOrder(HttpRequest http) =>
        http.Headers["X-Payment-Order"].FirstOrDefault() is { Length: > 0 } id ? id : null;

    public static IResult AwaitApproval(Payment payment) =>
        Results.Json(new { paymentOrderId = payment.PendingOrderId }, statusCode: StatusCodes.Status202Accepted);

    /// <summary>Site address for links: the configured one, else the address this request came to.</summary>
    public static string Origin(HttpRequest http, IConfiguration config) =>
        config["Site:Url"] is { Length: > 0 } url ? url.TrimEnd('/') : $"{http.Scheme}://{http.Host}";

    /// <summary>Empty input is fine (email is optional); anything else must be one plain address.</summary>
    public static bool TryEmail(string? input, out string email)
    {
        email = (input ?? "").Trim();
        if (email.Length == 0) return true;
        return email.Length <= Limits.Email && !email.Contains(' ') && MailAddress.TryCreate(email, out var parsed) && parsed.Address == email;
    }

    public static string NewId()
    {
        const string alphabet = "abcdefghjkmnpqrstuvwxyz23456789";
        return string.Create(12, 0, (span, _) =>
        {
            for (var i = 0; i < span.Length; i++) span[i] = alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
        });
    }
}

public record GiftDto(string Id, string Country, DateOnly Date, string To, string From, string Message, int Price, string Number, int Coups, bool Immune, string Title)
{
    public static GiftDto Of(Gift g) => new(g.Id, g.Country, g.Date, g.To, g.From, g.Message, g.Price, g.Number, g.Coups, g.Immune, g.Title);
}

namespace PresidentsMap;

public record PaymentResult(bool Ok, string Reference);

/// <summary>
/// The only place money moves. Swap <see cref="DemoPaymentGateway"/> for a real
/// provider (bank gateway, Paddle, ...) without touching the rest of the app.
/// </summary>
public interface IPaymentGateway
{
    Task<PaymentResult> ChargeAsync(int amountUsd, string description, CancellationToken ct);
    Task RefundAsync(string reference, CancellationToken ct);
}

/// <summary>Approves everything and charges nothing.</summary>
public class DemoPaymentGateway : IPaymentGateway
{
    public Task<PaymentResult> ChargeAsync(int amountUsd, string description, CancellationToken ct) =>
        Task.FromResult(new PaymentResult(true, "demo-" + Guid.NewGuid().ToString("N")[..12]));

    public Task RefundAsync(string reference, CancellationToken ct) => Task.CompletedTask;
}

using ECommerceApp.Application.Payments.Models;

namespace ECommerceApp.Application.Payments;

/// <summary>
/// Charges a payment method behind an abstraction so the gateway (a
/// simulated one today, per Milestone 9.2 - no real payment processor
/// account exists in this environment) can be swapped for a real one later
/// without touching callers - the same reasoning IFileStorage/IEmailSender
/// already use for local/simulated stand-ins elsewhere in this app.
/// </summary>
public interface IPaymentGateway
{
    Task<ChargeResult> ChargeAsync(ChargeRequest request, CancellationToken cancellationToken = default);
}

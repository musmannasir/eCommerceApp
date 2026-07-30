namespace ECommerceApp.Domain.Payments;

/// <summary>
/// The outcome of a single (simulated) charge attempt - both values are
/// meaningfully produced by Milestone 9.2's synchronous gateway simulation,
/// unlike a real async processor there's no intermediate "Pending"/"Authorized"
/// state to model here.
/// </summary>
public enum PaymentStatus
{
    Succeeded,
    Failed,
}

namespace ECommerceApp.Domain.Payments;

/// <summary>
/// Deliberately just one value for now - Milestone 9.2's simulated gateway
/// only ever charges a card. A real second method (e.g. a wallet or
/// bank transfer) would add its own value here rather than this being
/// removed or restructured.
/// </summary>
public enum PaymentMethodType
{
    CreditCard,
}

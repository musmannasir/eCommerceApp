namespace ECommerceApp.Application.Payments.Models;

/// <summary>
/// The raw card input a customer types on the Review step. Never persisted
/// as-is - IPaymentGateway derives only a masked last-4 and brand from it,
/// matching real PCI-compliant practice even though this gateway is
/// simulated.
/// </summary>
public record ChargeRequest(
    string CardNumber, string CardholderName, int ExpiryMonth, int ExpiryYear, string Cvv, decimal Amount);

public record ChargeResult(bool Succeeded, string MaskedCardNumber, string CardBrand, string? DeclineReason);

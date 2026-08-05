using System.Text.RegularExpressions;
using ECommerceApp.Application.Common.Interfaces;
using ECommerceApp.Application.Payments;
using ECommerceApp.Application.Payments.Models;

namespace ECommerceApp.Infrastructure.Payments;

/// <summary>
/// No real payment processor account exists in this environment (the same
/// reason DevEmailSender writes emails to disk instead of sending them), so
/// this "charges" a card using the well-known, publicly documented Stripe
/// test-card numbers - a real, industry-standard convention for simulating
/// both outcomes, not something invented for this app. 4242 4242 4242 4242
/// always succeeds; 4000 0000 0000 0002 always declines. Any other
/// card number is validated for real (Luhn checksum, length, expiry, CVV
/// format) and - since there's no real processor behind it - simply
/// succeeds if it passes those checks, the same leniency a sandbox/test
/// mode gateway would offer. The real card number is never persisted -
/// only a masked last-4 and the detected brand are returned.
/// </summary>
public sealed class SimulatedPaymentGateway : IPaymentGateway
{
    private const string StripeTestDeclineCard = "4000000000000002";

    private readonly IClock _clock;

    public SimulatedPaymentGateway(IClock clock)
    {
        _clock = clock;
    }

    public Task<ChargeResult> ChargeAsync(ChargeRequest request, CancellationToken cancellationToken = default)
    {
        var digitsOnly = Regex.Replace(request.CardNumber ?? string.Empty, @"[\s-]", "");

        if (digitsOnly.Length is < 13 or > 19 || !digitsOnly.All(char.IsDigit) || !PassesLuhnCheck(digitsOnly))
        {
            return Task.FromResult(Decline(digitsOnly, "Invalid card number."));
        }

        if (string.IsNullOrWhiteSpace(request.Cvv) || request.Cvv.Length is < 3 or > 4 || !request.Cvv.All(char.IsDigit))
        {
            return Task.FromResult(Decline(digitsOnly, "Invalid security code."));
        }

        var expiry = new DateTime(request.ExpiryYear, request.ExpiryMonth, 1).AddMonths(1).AddDays(-1);
        if (expiry < _clock.UtcNow.Date)
        {
            return Task.FromResult(Decline(digitsOnly, "Card has expired."));
        }

        if (digitsOnly == StripeTestDeclineCard)
        {
            return Task.FromResult(Decline(digitsOnly, "Your card was declined."));
        }

        return Task.FromResult(new ChargeResult(true, Mask(digitsOnly), DetectBrand(digitsOnly), null));
    }

    /// <summary>
    /// Unlike ChargeAsync, there is no realistic decline scenario for
    /// reversing a charge that already succeeded, and no card-number-based
    /// test fixtures exist for refunds - a real processor could still reject
    /// one (e.g. funds already withdrawn), but simulating that has no
    /// meaningful test case to drive it, so this always succeeds.
    /// </summary>
    public Task<RefundResult> RefundAsync(RefundRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(new RefundResult(true, null));

    private static ChargeResult Decline(string digitsOnly, string reason) =>
        new(false, Mask(digitsOnly), DetectBrand(digitsOnly), reason);

    private static bool PassesLuhnCheck(string digitsOnly)
    {
        var sum = 0;
        var doubleDigit = false;
        for (var i = digitsOnly.Length - 1; i >= 0; i--)
        {
            var digit = digitsOnly[i] - '0';
            if (doubleDigit)
            {
                digit *= 2;
                if (digit > 9)
                {
                    digit -= 9;
                }
            }

            sum += digit;
            doubleDigit = !doubleDigit;
        }

        return sum % 10 == 0;
    }

    private static string Mask(string digitsOnly) =>
        digitsOnly.Length >= 4 ? $"**** **** **** {digitsOnly[^4..]}" : "**** **** **** ****";

    private static string DetectBrand(string digitsOnly)
    {
        if (digitsOnly.Length == 0)
        {
            return "Card";
        }

        return digitsOnly[0] switch
        {
            '4' => "Visa",
            '5' => "Mastercard",
            '3' => "American Express",
            '6' => "Discover",
            _ => "Card",
        };
    }
}

using ECommerceApp.Application.Payments.Models;
using ECommerceApp.Infrastructure.Payments;
using ECommerceApp.Infrastructure.Tests.TestSupport;
using FluentAssertions;

namespace ECommerceApp.Infrastructure.Tests.Payments;

public class SimulatedPaymentGatewayTests
{
    private readonly FakeClock _clock = new();
    private readonly SimulatedPaymentGateway _gateway;

    public SimulatedPaymentGatewayTests()
    {
        _gateway = new SimulatedPaymentGateway(_clock);
    }

    [Fact]
    public async Task The_Stripe_test_success_card_succeeds()
    {
        var result = await _gateway.ChargeAsync(Standard() with { CardNumber = "4242 4242 4242 4242" });

        result.Succeeded.Should().BeTrue();
        result.MaskedCardNumber.Should().Be("**** **** **** 4242");
        result.CardBrand.Should().Be("Visa");
        result.DeclineReason.Should().BeNull();
    }

    [Fact]
    public async Task The_Stripe_test_decline_card_is_declined()
    {
        var result = await _gateway.ChargeAsync(Standard() with { CardNumber = "4000 0000 0000 0002" });

        result.Succeeded.Should().BeFalse();
        result.DeclineReason.Should().Be("Your card was declined.");
    }

    [Fact]
    public async Task A_card_number_failing_the_Luhn_check_is_declined_as_invalid()
    {
        var result = await _gateway.ChargeAsync(Standard() with { CardNumber = "4242424242424241" });

        result.Succeeded.Should().BeFalse();
        result.DeclineReason.Should().Be("Invalid card number.");
    }

    [Theory]
    [InlineData("123")]
    [InlineData("12345678901234567890")]
    public async Task A_card_number_with_an_invalid_length_is_declined(string cardNumber)
    {
        var result = await _gateway.ChargeAsync(Standard() with { CardNumber = cardNumber });

        result.Succeeded.Should().BeFalse();
        result.DeclineReason.Should().Be("Invalid card number.");
    }

    [Theory]
    [InlineData("12")]
    [InlineData("12345")]
    [InlineData("abc")]
    public async Task An_invalid_cvv_is_declined(string cvv)
    {
        var result = await _gateway.ChargeAsync(Standard() with { Cvv = cvv });

        result.Succeeded.Should().BeFalse();
        result.DeclineReason.Should().Be("Invalid security code.");
    }

    [Fact]
    public async Task An_expired_card_is_declined()
    {
        _clock.UtcNow = new DateTime(2031, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var result = await _gateway.ChargeAsync(Standard() with { ExpiryMonth = 12, ExpiryYear = 2030 });

        result.Succeeded.Should().BeFalse();
        result.DeclineReason.Should().Be("Card has expired.");
    }

    [Fact]
    public async Task A_card_expiring_in_the_current_month_is_still_valid()
    {
        _clock.UtcNow = new DateTime(2030, 12, 15, 0, 0, 0, DateTimeKind.Utc);

        var result = await _gateway.ChargeAsync(Standard() with { ExpiryMonth = 12, ExpiryYear = 2030 });

        result.Succeeded.Should().BeTrue();
    }

    [Theory]
    [InlineData("4242424242424242", "Visa")]
    [InlineData("5555555555554444", "Mastercard")]
    [InlineData("378282246310005", "American Express")]
    [InlineData("6011111111111117", "Discover")]
    public async Task The_card_brand_is_detected_from_its_leading_digit(string cardNumber, string expectedBrand)
    {
        var result = await _gateway.ChargeAsync(Standard() with { CardNumber = cardNumber });

        result.CardBrand.Should().Be(expectedBrand);
    }

    [Fact]
    public async Task The_masked_number_never_exposes_more_than_the_last_four_digits()
    {
        var result = await _gateway.ChargeAsync(Standard() with { CardNumber = "4242424242424242" });

        result.MaskedCardNumber.Should().Be("**** **** **** 4242");
        result.MaskedCardNumber.Should().NotContain("424242424242");
    }

    private static ChargeRequest Standard() => new("4242424242424242", "Jane Doe", 12, 2030, "123", 100m);
}

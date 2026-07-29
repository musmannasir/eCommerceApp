using ECommerceApp.Application.Checkout;
using ECommerceApp.Application.Checkout.Models;
using ECommerceApp.Application.Marketing;
using ECommerceApp.Application.Marketing.Models;
using ECommerceApp.Application.Shipping;
using ECommerceApp.Application.Taxation;
using ECommerceApp.Application.Taxation.Models;

namespace ECommerceApp.Infrastructure.Checkout;

/// <summary>
/// Pure calculator with no persistence of its own - composes
/// IPromotionService/ITaxService/IShippingService, the same "orchestrator
/// on top of existing calculators" role IPricingService plays for a single
/// product's price. See ICheckoutCalculationService's doc comment for why
/// an invalid/missing promotion is treated as "no discount" rather than an
/// error.
/// </summary>
public sealed class CheckoutCalculationService : ICheckoutCalculationService
{
    private readonly IPromotionService _promotionService;
    private readonly ITaxService _taxService;
    private readonly IShippingService _shippingService;

    public CheckoutCalculationService(IPromotionService promotionService, ITaxService taxService, IShippingService shippingService)
    {
        _promotionService = promotionService;
        _taxService = taxService;
        _shippingService = shippingService;
    }

    public async Task<CheckoutCalculationResult> CalculateAsync(
        IReadOnlyList<CheckoutLineDto> lines,
        int? appliedPromotionId,
        string taxCountryCode,
        string? taxRegionCode,
        string shippingCountryCode,
        string? shippingRegionCode,
        int? selectedShippingMethodId = null,
        CancellationToken cancellationToken = default)
    {
        var subtotal = lines.Sum(l => l.LineTotal);
        var (discountAmount, lineDiscounts, couponCode, promotionName) =
            await ResolveDiscountAsync(lines, appliedPromotionId, subtotal, cancellationToken);
        var discountedSubtotal = subtotal - discountAmount;

        var tax = 0m;
        var anyTaxConfigured = false;
        for (var i = 0; i < lines.Count; i++)
        {
            if (!lines[i].IsTaxable)
            {
                continue;
            }

            var postDiscountAmount = lines[i].LineTotal - lineDiscounts[i];
            var result = await _taxService.CalculateTaxAsync(postDiscountAmount, lines[i].TaxCategory, taxCountryCode, taxRegionCode, cancellationToken);
            tax += result.TaxAmount;
            anyTaxConfigured |= result.RateConfigured;
        }

        var totalWeight = lines.Sum(l => l.TotalWeight);
        var options = await _shippingService.GetAvailableShippingOptionsAsync(
            totalWeight, discountedSubtotal, shippingCountryCode, shippingRegionCode, cancellationToken);
        var selectedOption = selectedShippingMethodId.HasValue
            ? options.FirstOrDefault(o => o.ShippingMethodId == selectedShippingMethodId.Value)
            : options.OrderBy(o => o.Cost).FirstOrDefault();

        var shippingCost = selectedOption?.Cost ?? 0m;
        var shippingConfigured = selectedOption is not null;

        return new CheckoutCalculationResult(
            subtotal, discountAmount, couponCode, promotionName, discountedSubtotal,
            tax, anyTaxConfigured, shippingCost, shippingConfigured,
            discountedSubtotal + tax + shippingCost);
    }

    public async Task<CheckoutCalculationResult> CalculateEstimatedAsync(
        IReadOnlyList<CheckoutLineDto> lines, int? appliedPromotionId, CancellationToken cancellationToken = default)
    {
        var subtotal = lines.Sum(l => l.LineTotal);
        var (discountAmount, lineDiscounts, couponCode, promotionName) =
            await ResolveDiscountAsync(lines, appliedPromotionId, subtotal, cancellationToken);
        var discountedSubtotal = subtotal - discountAmount;

        var taxableLines = new List<TaxableLine>();
        for (var i = 0; i < lines.Count; i++)
        {
            if (lines[i].IsTaxable)
            {
                taxableLines.Add(new TaxableLine(lines[i].LineTotal - lineDiscounts[i], lines[i].TaxCategory));
            }
        }

        var tax = await _taxService.CalculateEstimatedTaxAsync(taxableLines, cancellationToken);

        var totalWeight = lines.Sum(l => l.TotalWeight);
        var shipping = await _shippingService.CalculateEstimatedShippingAsync(totalWeight, discountedSubtotal, cancellationToken);

        return new CheckoutCalculationResult(
            subtotal, discountAmount, couponCode, promotionName, discountedSubtotal,
            tax.TaxAmount, tax.RateConfigured, shipping.Cost, shipping.RateConfigured,
            discountedSubtotal + tax.TaxAmount + shipping.Cost);
    }

    private async Task<(decimal DiscountAmount, IReadOnlyList<decimal> LineDiscounts, string? CouponCode, string? PromotionName)> ResolveDiscountAsync(
        IReadOnlyList<CheckoutLineDto> lines, int? appliedPromotionId, decimal subtotal, CancellationToken cancellationToken)
    {
        var noDiscount = ((decimal)0, (IReadOnlyList<decimal>)new decimal[lines.Count], (string?)null, (string?)null);
        if (appliedPromotionId is not { } promotionId)
        {
            return noDiscount;
        }

        var promotionLines = lines.Select(l => new PromotionCartLine(l.ProductId, l.CategoryId, l.BrandId, l.LineTotal)).ToList();
        var validation = await _promotionService.ValidateAppliedPromotionAsync(promotionId, promotionLines, subtotal, cancellationToken);
        if (validation.IsFailure)
        {
            return noDiscount;
        }

        return (validation.Value.DiscountAmount, validation.Value.LineDiscounts, validation.Value.CouponCode, validation.Value.Name);
    }
}

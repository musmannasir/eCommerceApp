using ECommerceApp.Application.Addresses.Models;
using ECommerceApp.Application.Carts.Models;
using ECommerceApp.Application.Checkout.Models;
using ECommerceApp.Application.Finance.Models;
using ECommerceApp.Application.Orders.Models;
using ECommerceApp.Application.Payments.Models;
using ECommerceApp.Application.Returns.Models;
using ECommerceApp.Application.Shipping.Models;
using ECommerceApp.Application.Storefront.Models;
using ECommerceApp.Domain.Orders;
using ECommerceApp.Infrastructure.Tests.Catalog;
using FluentAssertions;

namespace ECommerceApp.Infrastructure.Tests.Finance;

public class FinanceServiceTests : IDisposable
{
    private readonly CatalogTestHarness _harness = new();

    public void Dispose() => _harness.Dispose();

    [Fact]
    public async Task GetDashboardSummaryAsync_returns_all_zeros_when_nothing_has_happened_yet()
    {
        var summary = await _harness.FinanceService.GetDashboardSummaryAsync();

        summary.Should().Be(new FinancialSummaryDto(0m, 0m, 0m, 0, 0, 0m));
    }

    [Fact]
    public async Task GetDashboardSummaryAsync_computes_totals_across_paid_declined_and_refunded_orders()
    {
        await CreatePaidOrderAsync("user-1"); // +117 revenue
        var refundedOrder = await CreateDeliveredOrderAsync("user-2"); // +117 revenue, then -100 refund
        await CreateDeclinedOrderAsync("user-3"); // does not count - never charged successfully

        var submitted = await _harness.ReturnService.SubmitReturnRequestAsync(
            "user-2", new CreateReturnRequestRequest(refundedOrder.OrderNumber, ReturnReason.Defective, null,
                new List<CreateReturnRequestItem> { new(refundedOrder.Items[0].Id, 1) }));
        await _harness.ReturnService.ApproveAsync(submitted.Value.Id);
        await _harness.ReturnService.RefundAsync(submitted.Value.Id);

        var summary = await _harness.FinanceService.GetDashboardSummaryAsync();

        summary.TotalRevenue.Should().Be(234m);
        summary.TotalRefunded.Should().Be(100m);
        summary.NetRevenue.Should().Be(134m);
        summary.PaidOrderCount.Should().Be(2);
        summary.RefundCount.Should().Be(1);
        summary.AverageOrderValue.Should().Be(117m);
    }

    [Fact]
    public async Task GetLedgerAsync_merges_charges_and_refunds_newest_first_with_signed_amounts()
    {
        await CreatePaidOrderAsync("user-1");
        _harness.Clock.UtcNow = _harness.Clock.UtcNow.AddMinutes(1);
        var refundedOrder = await CreateDeliveredOrderAsync("user-2");

        _harness.Clock.UtcNow = _harness.Clock.UtcNow.AddMinutes(1);
        var submitted = await _harness.ReturnService.SubmitReturnRequestAsync(
            "user-2", new CreateReturnRequestRequest(refundedOrder.OrderNumber, ReturnReason.Defective, null,
                new List<CreateReturnRequestItem> { new(refundedOrder.Items[0].Id, 1) }));
        await _harness.ReturnService.ApproveAsync(submitted.Value.Id);

        _harness.Clock.UtcNow = _harness.Clock.UtcNow.AddMinutes(1);
        await _harness.ReturnService.RefundAsync(submitted.Value.Id);

        var ledger = await _harness.FinanceService.GetLedgerAsync(new LedgerQuery { Page = 1, PageSize = 20 });

        ledger.TotalCount.Should().Be(3);
        ledger.Items[0].Type.Should().Be(LedgerEntryType.Refund);
        ledger.Items[0].Amount.Should().Be(-100m);
        ledger.Items.Should().Contain(e => e.Type == LedgerEntryType.Charge && e.OrderNumber == refundedOrder.OrderNumber && e.Amount == 117m);
        ledger.Items.Count(e => e.Type == LedgerEntryType.Charge).Should().Be(2);
    }

    [Fact]
    public async Task GetLedgerAsync_paginates_correctly()
    {
        for (var i = 0; i < 3; i++)
        {
            await CreatePaidOrderAsync($"user-{i}");
            _harness.Clock.UtcNow = _harness.Clock.UtcNow.AddMinutes(1);
        }

        var firstPage = await _harness.FinanceService.GetLedgerAsync(new LedgerQuery { Page = 1, PageSize = 2 });
        var secondPage = await _harness.FinanceService.GetLedgerAsync(new LedgerQuery { Page = 2, PageSize = 2 });

        firstPage.Items.Should().HaveCount(2);
        firstPage.TotalCount.Should().Be(3);
        secondPage.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetCashFlowAsync_defaults_to_the_30_days_ending_today_when_no_range_is_given()
    {
        var today = _harness.Clock.UtcNow.Date;
        await CreatePaidOrderAsync("user-1");

        var result = await _harness.FinanceService.GetCashFlowAsync(new CashFlowQuery());

        result.From.Should().Be(today.AddDays(-29));
        result.To.Should().Be(today);
        result.Periods.Should().HaveCount(30);
        result.Periods.Last().Date.Should().Be(today);
        result.Periods.Last().Revenue.Should().Be(117m);
    }

    [Fact]
    public async Task GetCashFlowAsync_buckets_charges_and_refunds_by_day_and_fills_gap_days_with_zero()
    {
        var startDate = _harness.Clock.UtcNow.Date;
        await CreatePaidOrderAsync("user-1"); // day 0: +117 revenue

        _harness.Clock.UtcNow = _harness.Clock.UtcNow.AddDays(2);
        var refundedOrder = await CreateDeliveredOrderAsync("user-2"); // day 2: +117 revenue
        var submitted = await _harness.ReturnService.SubmitReturnRequestAsync(
            "user-2", new CreateReturnRequestRequest(refundedOrder.OrderNumber, ReturnReason.Defective, null,
                new List<CreateReturnRequestItem> { new(refundedOrder.Items[0].Id, 1) }));
        await _harness.ReturnService.ApproveAsync(submitted.Value.Id);
        await _harness.ReturnService.RefundAsync(submitted.Value.Id); // day 2: -100 refund

        var result = await _harness.FinanceService.GetCashFlowAsync(
            new CashFlowQuery { From = startDate, To = startDate.AddDays(3) });

        result.Periods.Should().HaveCount(4);
        result.Periods[0].Revenue.Should().Be(117m);
        result.Periods[0].Refunded.Should().Be(0m);
        result.Periods[1].Revenue.Should().Be(0m); // gap day
        result.Periods[1].Net.Should().Be(0m);
        result.Periods[2].Revenue.Should().Be(117m);
        result.Periods[2].Refunded.Should().Be(100m);
        result.Periods[2].Net.Should().Be(17m);
        result.Periods[3].Revenue.Should().Be(0m); // gap day

        result.TotalRevenue.Should().Be(234m);
        result.TotalRefunded.Should().Be(100m);
        result.TotalNet.Should().Be(134m);
    }

    [Fact]
    public async Task GetCashFlowAsync_swaps_a_reversed_date_range()
    {
        var today = _harness.Clock.UtcNow.Date;

        var result = await _harness.FinanceService.GetCashFlowAsync(
            new CashFlowQuery { From = today, To = today.AddDays(-5) });

        result.From.Should().Be(today.AddDays(-5));
        result.To.Should().Be(today);
        result.Periods.Should().HaveCount(6);
    }

    private async Task<OrderDto> CreateDeliveredOrderAsync(string userId)
    {
        var order = await CreatePaidOrderAsync(userId);
        await _harness.OrderService.ShipAsync(order.Id, new ShipOrderRequest("UPS", "1Z999AA10123456784"));
        var delivered = await _harness.OrderService.MarkDeliveredAsync(order.Id);
        return delivered.Value;
    }

    private async Task<OrderDto> CreatePaidOrderAsync(string userId)
    {
        var result = await _harness.OrderService.CreateOrderAsync(StandardRequest(userId, Guid.NewGuid().ToString("N")));
        result.Value.Status.Should().Be(nameof(OrderStatus.Paid));
        return result.Value;
    }

    private async Task<OrderDto> CreateDeclinedOrderAsync(string userId)
    {
        var request = StandardRequest(userId, Guid.NewGuid().ToString("N")) with
        {
            Payment = StandardPayment() with { CardNumber = "4000000000000002" },
        };
        var result = await _harness.OrderService.CreateOrderAsync(request);
        result.Value.Status.Should().Be(nameof(OrderStatus.PaymentFailed));
        return result.Value;
    }

    private static CreateOrderRequest StandardRequest(string userId, string idempotencyKey) => new(
        userId,
        idempotencyKey,
        new AddressDto(1, "Home", "Jane Doe", "555-0100", "123 Main St", null, "Springfield", "CA", "90210", "US", true),
        AppliedPromotionId: null,
        new ShippingOptionDto(1, "Standard Shipping", null, 7m, null, null),
        new List<CartItemDto>
        {
            new(1, 1, null, "Widget", "widget", null, "SKU-1", null, 100m, null, null, 1, 100m,
                ProductStockState.InStock, 10, true, false, null, false),
        },
        StandardCalculation(),
        StandardPayment());

    private static CheckoutCalculationResult StandardCalculation() => new(
        Subtotal: 100m, PromotionDiscount: 0m, AppliedCouponCode: null, AppliedPromotionName: null,
        DiscountedSubtotal: 100m, Tax: 10m, TaxRateConfigured: true,
        Shipping: 7m, ShippingRateConfigured: true, GrandTotal: 117m);

    private static ChargeRequest StandardPayment() => new(
        "4242424242424242", "Jane Doe", 12, 2030, "123", Amount: 0m);
}

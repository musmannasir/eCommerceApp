using ECommerceApp.Domain.Orders;
using FluentAssertions;

namespace ECommerceApp.Domain.Tests.Orders;

public class OrderStatusTransitionsTests
{
    [Theory]
    [InlineData(OrderStatus.Pending, OrderStatus.Paid)]
    [InlineData(OrderStatus.Pending, OrderStatus.PaymentFailed)]
    [InlineData(OrderStatus.Pending, OrderStatus.StockReservationFailed)]
    [InlineData(OrderStatus.Paid, OrderStatus.Cancelled)]
    [InlineData(OrderStatus.Paid, OrderStatus.Shipped)]
    [InlineData(OrderStatus.Shipped, OrderStatus.Delivered)]
    public void CanTransition_allows_every_legal_transition(OrderStatus from, OrderStatus to)
    {
        OrderStatusTransitions.CanTransition(from, to).Should().BeTrue();
    }

    [Theory]
    [InlineData(OrderStatus.Paid, OrderStatus.Delivered)]
    [InlineData(OrderStatus.Shipped, OrderStatus.Cancelled)]
    [InlineData(OrderStatus.Cancelled, OrderStatus.Paid)]
    [InlineData(OrderStatus.PaymentFailed, OrderStatus.Paid)]
    [InlineData(OrderStatus.StockReservationFailed, OrderStatus.Paid)]
    [InlineData(OrderStatus.Delivered, OrderStatus.Shipped)]
    [InlineData(OrderStatus.Pending, OrderStatus.Shipped)]
    public void CanTransition_rejects_every_illegal_transition(OrderStatus from, OrderStatus to)
    {
        OrderStatusTransitions.CanTransition(from, to).Should().BeFalse();
    }

    [Theory]
    [InlineData(OrderStatus.PaymentFailed)]
    [InlineData(OrderStatus.StockReservationFailed)]
    [InlineData(OrderStatus.Cancelled)]
    [InlineData(OrderStatus.Delivered)]
    public void Terminal_statuses_allow_no_further_transitions(OrderStatus terminal)
    {
        foreach (var candidate in Enum.GetValues<OrderStatus>())
        {
            OrderStatusTransitions.CanTransition(terminal, candidate).Should().BeFalse();
        }
    }
}

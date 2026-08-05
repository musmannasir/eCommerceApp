namespace ECommerceApp.Application.Reporting.Models;

/// <summary>
/// From/To are inclusive whole days, same convention as CashFlowQuery
/// (Milestone 14.2) - either or both may be omitted, defaulting to the 30
/// days ending today.
/// </summary>
public record TopSellingProductsQuery
{
    public DateTime? From { get; init; }
    public DateTime? To { get; init; }
    public int Take { get; init; } = 20;
}

/// <summary>ProductName is the most recent snapshot among the grouped OrderItem rows, not a live join to Product - a renamed or deleted product still reports correctly under the name it was sold as.</summary>
public record TopSellingProductDto(int ProductId, string ProductName, int QuantitySold, decimal Revenue);

public record TopSellingProductsResultDto(DateTime From, DateTime To, IReadOnlyList<TopSellingProductDto> Products);

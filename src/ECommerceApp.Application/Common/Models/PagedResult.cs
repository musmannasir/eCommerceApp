namespace ECommerceApp.Application.Common.Models;

public record PagedQuery
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? Search { get; init; }
    public string? SortBy { get; init; }
    public bool SortDescending { get; init; }

    /// <summary>When true, lists only soft-deleted rows (for a "Recycle bin"/restore view) instead of active ones.</summary>
    public bool OnlyDeleted { get; init; }
}

public record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize)
{
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}

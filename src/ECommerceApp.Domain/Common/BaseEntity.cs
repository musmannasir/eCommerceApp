namespace ECommerceApp.Domain.Common;

/// <summary>
/// Base type for every persisted domain entity. Uses an integer surrogate key;
/// business-facing identifiers (slugs, SKUs, order numbers) are separate fields
/// on the entities that need to be safely exposed externally.
/// </summary>
public abstract class BaseEntity
{
    public int Id { get; set; }
}

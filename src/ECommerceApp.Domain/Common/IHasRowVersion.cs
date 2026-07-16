namespace ECommerceApp.Domain.Common;

/// <summary>
/// Implemented by entities that require optimistic concurrency control
/// (stock, price, payment, and order records).
/// </summary>
public interface IHasRowVersion
{
    byte[] RowVersion { get; set; }
}

namespace ECommerceApp.Application.Common.Interfaces;

/// <summary>
/// Abstraction over wall-clock time so application/domain code never calls
/// <see cref="DateTime.UtcNow"/> directly, keeping time deterministic in tests.
/// All timestamps produced must be UTC.
/// </summary>
public interface IClock
{
    DateTime UtcNow { get; }
}

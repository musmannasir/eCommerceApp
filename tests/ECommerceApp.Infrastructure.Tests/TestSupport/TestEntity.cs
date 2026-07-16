using ECommerceApp.Domain.Common;

namespace ECommerceApp.Infrastructure.Tests.TestSupport;

/// <summary>Throwaway entity used only to exercise <c>ApplicationDbContext</c>'s shared behavior.</summary>
public class TestEntity : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
}

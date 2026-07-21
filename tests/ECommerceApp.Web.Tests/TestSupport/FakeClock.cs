using ECommerceApp.Application.Common.Interfaces;

namespace ECommerceApp.Web.Tests.TestSupport;

public class FakeClock : IClock
{
    public DateTime UtcNow { get; set; } = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
}

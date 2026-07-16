using ECommerceApp.Application.Common.Interfaces;

namespace ECommerceApp.Infrastructure.Common;

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}

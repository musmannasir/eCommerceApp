using ECommerceApp.Application.Common.Interfaces;
using ECommerceApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ECommerceApp.Infrastructure.Tests.TestSupport;

public class TestDbContext : ApplicationDbContext
{
    public TestDbContext(DbContextOptions options, ICurrentUserService currentUserService, IClock clock)
        : base(options, currentUserService, clock)
    {
    }

    public DbSet<TestEntity> TestEntities => Set<TestEntity>();
}

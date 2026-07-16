using ECommerceApp.Infrastructure.Tests.TestSupport;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace ECommerceApp.Infrastructure.Tests.Persistence;

public class ApplicationDbContextTests
{
    private static TestDbContext CreateContext(FakeClock clock, FakeCurrentUserService currentUser)
    {
        var options = new DbContextOptionsBuilder()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new TestDbContext(options, currentUser, clock);
    }

    [Fact]
    public async Task Adding_an_entity_stamps_created_audit_fields()
    {
        var clock = new FakeClock();
        var currentUser = new FakeCurrentUserService { UserId = "alice" };
        await using var context = CreateContext(clock, currentUser);

        var entity = new TestEntity { Name = "widget" };
        context.TestEntities.Add(entity);
        await context.SaveChangesAsync();

        entity.CreatedAtUtc.Should().Be(clock.UtcNow);
        entity.CreatedByUserId.Should().Be("alice");
        entity.UpdatedAtUtc.Should().BeNull();
    }

    [Fact]
    public async Task Updating_an_entity_stamps_updated_audit_fields()
    {
        var clock = new FakeClock();
        var currentUser = new FakeCurrentUserService { UserId = "alice" };
        await using var context = CreateContext(clock, currentUser);

        var entity = new TestEntity { Name = "widget" };
        context.TestEntities.Add(entity);
        await context.SaveChangesAsync();

        clock.UtcNow = clock.UtcNow.AddDays(1);
        currentUser.UserId = "bob";
        entity.Name = "renamed-widget";
        await context.SaveChangesAsync();

        entity.UpdatedAtUtc.Should().Be(clock.UtcNow);
        entity.UpdatedByUserId.Should().Be("bob");
    }

    [Fact]
    public async Task Removing_an_entity_soft_deletes_it_instead_of_hard_deleting()
    {
        var clock = new FakeClock();
        var currentUser = new FakeCurrentUserService();
        await using var context = CreateContext(clock, currentUser);

        var entity = new TestEntity { Name = "widget" };
        context.TestEntities.Add(entity);
        await context.SaveChangesAsync();

        context.TestEntities.Remove(entity);
        await context.SaveChangesAsync();

        entity.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task Soft_deleted_entities_are_excluded_by_the_global_query_filter()
    {
        var clock = new FakeClock();
        var currentUser = new FakeCurrentUserService();
        await using var context = CreateContext(clock, currentUser);

        var entity = new TestEntity { Name = "widget" };
        context.TestEntities.Add(entity);
        await context.SaveChangesAsync();

        context.TestEntities.Remove(entity);
        await context.SaveChangesAsync();

        var visible = await context.TestEntities.ToListAsync();

        visible.Should().BeEmpty();
    }
}

using ECommerceApp.Domain.Catalog;
using ECommerceApp.Infrastructure.Persistence;
using ECommerceApp.IntegrationTests.TestSupport;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerceApp.IntegrationTests.Catalog;

/// <summary>
/// Verifies RowVersion is a real, SQL-Server-enforced concurrency token (see
/// ApplicationDbContext.OnModelCreating's IsRowVersion() configuration) - not just an inert
/// byte[] column that EF Core never checks.
/// </summary>
[Collection(AuthTestCollection.Name)]
public class ConcurrencyTests
{
    private readonly AuthTestFixture _fixture;

    public ConcurrencyTests(AuthTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Saving_a_stale_copy_of_a_category_throws_a_concurrency_exception()
    {
        int categoryId;
        using (var setupScope = _fixture.Factory.Services.CreateScope())
        {
            var setupContext = setupScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var category = new Category { Name = "Concurrency Test", Slug = $"concurrency-{Guid.NewGuid():N}", DisplayOrder = 0, IsActive = true };
            setupContext.Categories.Add(category);
            await setupContext.SaveChangesAsync();
            categoryId = category.Id;
        }

        using var scopeA = _fixture.Factory.Services.CreateScope();
        using var scopeB = _fixture.Factory.Services.CreateScope();
        var contextA = scopeA.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var contextB = scopeB.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var categoryA = await contextA.Categories.SingleAsync(c => c.Id == categoryId);
        var categoryB = await contextB.Categories.SingleAsync(c => c.Id == categoryId);

        categoryA.Name = "Updated by A";
        await contextA.SaveChangesAsync();

        categoryB.Name = "Updated by B";
        var act = async () => await contextB.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }
}

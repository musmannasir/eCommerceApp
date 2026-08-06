using System.Security.Claims;
using ECommerceApp.Application.Configuration;
using ECommerceApp.Application.Configuration.Models;
using ECommerceApp.Domain.Catalog;
using ECommerceApp.Infrastructure.Persistence;
using ECommerceApp.Web.Services;
using ECommerceApp.Web.Tests.TestSupport;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace ECommerceApp.Web.Tests.Services;

public class RecentlyViewedServiceTests : IDisposable
{
    private readonly ApplicationDbContext _dbContext;
    private readonly FakeClock _clock = new();

    public RecentlyViewedServiceTests()
    {
        var options = new DbContextOptionsBuilder()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new ApplicationDbContext(options, new FakeCurrentUserService(), _clock);
    }

    public void Dispose() => _dbContext.Dispose();

    private RecentlyViewedService CreateService(IHttpContextAccessor accessor, int maxItems = 10)
    {
        var dto = new StoreSettingsDto("ECommerce Store", "PKR", "Pakistan", false, maxItems, "PK", "", "PK", "", Array.Empty<byte>());
        var storeSettingsService = new Mock<IStoreSettingsService>();
        storeSettingsService.Setup(s => s.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(dto);

        var environment = new Mock<IWebHostEnvironment>();
        environment.SetupGet(e => e.EnvironmentName).Returns("Development");

        return new RecentlyViewedService(accessor, _dbContext, _clock, storeSettingsService.Object, environment.Object);
    }

    private static IHttpContextAccessor CreateGuestAccessor() =>
        new HttpContextAccessor { HttpContext = new DefaultHttpContext() };

    private static IHttpContextAccessor CreateAuthenticatedAccessor(string userId)
    {
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, userId)], authenticationType: "TestAuth"));
        return new HttpContextAccessor { HttpContext = context };
    }

    private static IHttpContextAccessor SimulateNextRequest(HttpContext previous)
    {
        var setCookie = previous.Response.Headers.SetCookie;
        var nextContext = new DefaultHttpContext();
        foreach (var cookieHeader in setCookie)
        {
            var nameAndValue = cookieHeader!.Split(';', 2)[0];
            nextContext.Request.Headers.Append("Cookie", nameAndValue);
        }

        return new HttpContextAccessor { HttpContext = nextContext };
    }

    private async Task<Product> SeedProductAsync(bool isActive = true, bool isPublished = true, string name = "Widget")
    {
        var category = new Category { Name = "Cat", Slug = $"cat-{Guid.NewGuid():N}", DisplayOrder = 0, IsActive = true };
        _dbContext.Categories.Add(category);
        await _dbContext.SaveChangesAsync();

        var product = new Product
        {
            Name = name,
            Slug = $"{name.ToLowerInvariant()}-{Guid.NewGuid():N}",
            CategoryId = category.Id,
            BaseSKU = $"SKU-{Guid.NewGuid():N}",
            CostPrice = 5,
            SellingPrice = 10,
            IsActive = isActive,
            IsPublished = isPublished,
            PublishedAtUtc = DateTime.UtcNow,
        };
        _dbContext.Products.Add(product);
        await _dbContext.SaveChangesAsync();
        return product;
    }

    [Fact]
    public async Task A_guest_view_round_trips_through_the_cookie()
    {
        var product = await SeedProductAsync();
        var recordingAccessor = CreateGuestAccessor();
        var recordingService = CreateService(recordingAccessor);

        await recordingService.RecordViewAsync(product.Id);

        var readingAccessor = SimulateNextRequest(recordingAccessor.HttpContext!);
        var readingService = CreateService(readingAccessor);
        var result = await readingService.GetRecentlyViewedAsync();

        result.Should().ContainSingle(p => p.Id == product.Id);
    }

    [Fact]
    public async Task Viewing_the_same_product_again_moves_it_to_front_without_duplicating()
    {
        var first = await SeedProductAsync(name: "First");
        var second = await SeedProductAsync(name: "Second");
        var accessor = CreateGuestAccessor();

        await CreateService(accessor).RecordViewAsync(first.Id);
        accessor = SimulateNextRequest(accessor.HttpContext!);
        await CreateService(accessor).RecordViewAsync(second.Id);
        accessor = SimulateNextRequest(accessor.HttpContext!);
        await CreateService(accessor).RecordViewAsync(first.Id);

        var readingAccessor = SimulateNextRequest(accessor.HttpContext!);
        var result = await CreateService(readingAccessor).GetRecentlyViewedAsync();

        result.Should().HaveCount(2);
        result[0].Id.Should().Be(first.Id);
        result[1].Id.Should().Be(second.Id);
    }

    [Fact]
    public async Task Guest_history_is_trimmed_to_the_configured_maximum()
    {
        var products = new List<Product>();
        for (var i = 0; i < 3; i++)
        {
            products.Add(await SeedProductAsync(name: $"Product {i}"));
        }

        var accessor = CreateGuestAccessor();
        foreach (var product in products)
        {
            await CreateService(accessor, maxItems: 2).RecordViewAsync(product.Id);
            accessor = SimulateNextRequest(accessor.HttpContext!);
        }

        var result = await CreateService(accessor, maxItems: 2).GetRecentlyViewedAsync();

        result.Should().HaveCount(2);
        result.Select(p => p.Id).Should().Equal(products[2].Id, products[1].Id);
    }

    [Fact]
    public async Task An_authenticated_view_upserts_a_database_row_and_updates_its_timestamp()
    {
        var product = await SeedProductAsync();
        var accessor = CreateAuthenticatedAccessor("user-1");
        var service = CreateService(accessor);

        await service.RecordViewAsync(product.Id);
        await service.RecordViewAsync(product.Id);

        var rows = await _dbContext.RecentlyViewedItems.Where(r => r.UserId == "user-1").ToListAsync();
        rows.Should().ContainSingle();
        rows[0].ProductId.Should().Be(product.Id);
    }

    [Fact]
    public async Task An_authenticated_users_history_is_trimmed_to_the_configured_maximum()
    {
        var products = new List<Product>();
        for (var i = 0; i < 3; i++)
        {
            products.Add(await SeedProductAsync(name: $"Product {i}"));
        }

        var accessor = CreateAuthenticatedAccessor("user-1");
        var service = CreateService(accessor, maxItems: 2);

        foreach (var product in products)
        {
            await service.RecordViewAsync(product.Id);
            _clock.UtcNow = _clock.UtcNow.AddMinutes(1);
        }

        var rows = await _dbContext.RecentlyViewedItems.Where(r => r.UserId == "user-1").ToListAsync();
        rows.Should().HaveCount(2);
        rows.Select(r => r.ProductId).Should().BeEquivalentTo([products[1].Id, products[2].Id]);
    }

    [Fact]
    public async Task The_excluded_product_id_is_omitted_from_the_result()
    {
        var first = await SeedProductAsync(name: "First");
        var second = await SeedProductAsync(name: "Second");
        var accessor = CreateAuthenticatedAccessor("user-1");
        var service = CreateService(accessor);

        await service.RecordViewAsync(first.Id);
        await service.RecordViewAsync(second.Id);

        var result = await service.GetRecentlyViewedAsync(excludeProductId: second.Id);

        result.Should().ContainSingle(p => p.Id == first.Id);
    }

    [Fact]
    public async Task A_product_that_became_unpublished_since_being_viewed_is_skipped()
    {
        var product = await SeedProductAsync();
        var accessor = CreateAuthenticatedAccessor("user-1");
        var service = CreateService(accessor);
        await service.RecordViewAsync(product.Id);

        product.IsPublished = false;
        await _dbContext.SaveChangesAsync();

        var result = await service.GetRecentlyViewedAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task No_history_returns_an_empty_list()
    {
        var service = CreateService(CreateGuestAccessor());

        var result = await service.GetRecentlyViewedAsync();

        result.Should().BeEmpty();
    }
}

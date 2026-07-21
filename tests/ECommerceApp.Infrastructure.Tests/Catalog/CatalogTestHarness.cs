using ECommerceApp.Infrastructure.Catalog;
using ECommerceApp.Infrastructure.Marketing;
using ECommerceApp.Infrastructure.Storefront;
using ECommerceApp.Infrastructure.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace ECommerceApp.Infrastructure.Tests.Catalog;

/// <summary>Wires up the catalog services against an EF Core InMemory-backed context - no real DB needed.</summary>
public sealed class CatalogTestHarness : IDisposable
{
    public TestDbContext DbContext { get; }
    public FakeClock Clock { get; }
    public FakeFileStorage FileStorage { get; }
    public CategoryService CategoryService { get; }
    public BrandService BrandService { get; }
    public ProductAttributeService AttributeService { get; }
    public ProductService ProductService { get; }
    public HomePageBannerService HomePageBannerService { get; }
    public HomePageService HomePageService { get; }
    public CatalogBrowseService CatalogBrowseService { get; }

    public CatalogTestHarness()
    {
        var options = new DbContextOptionsBuilder()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        Clock = new FakeClock();
        FileStorage = new FakeFileStorage();
        DbContext = new TestDbContext(options, new FakeCurrentUserService(), Clock);

        CategoryService = new CategoryService(DbContext);
        BrandService = new BrandService(DbContext);
        AttributeService = new ProductAttributeService(DbContext);
        ProductService = new ProductService(DbContext, FileStorage, Clock);
        HomePageBannerService = new HomePageBannerService(DbContext);
        HomePageService = new HomePageService(DbContext);
        CatalogBrowseService = new CatalogBrowseService(DbContext);
    }

    public void Dispose() => DbContext.Dispose();
}

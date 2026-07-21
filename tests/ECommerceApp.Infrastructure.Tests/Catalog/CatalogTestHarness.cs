using ECommerceApp.Infrastructure.Carts;
using ECommerceApp.Infrastructure.Catalog;
using ECommerceApp.Infrastructure.Marketing;
using ECommerceApp.Infrastructure.Pricing;
using ECommerceApp.Infrastructure.Storefront;
using ECommerceApp.Infrastructure.Tests.TestSupport;
using ECommerceApp.Infrastructure.Wishlist;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

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
    public PricingService PricingService { get; }
    public RecommendationService RecommendationService { get; }
    public FakeRecentlyViewedService RecentlyViewedService { get; }
    public ProductDetailService ProductDetailService { get; }
    public CartService CartService { get; }
    public WishlistService WishlistService { get; }

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
        RecentlyViewedService = new FakeRecentlyViewedService();
        HomePageService = new HomePageService(DbContext, RecentlyViewedService);
        CatalogBrowseService = new CatalogBrowseService(DbContext);
        PricingService = new PricingService(new ConfigurationBuilder().Build());
        RecommendationService = new RecommendationService(DbContext);
        WishlistService = new WishlistService(DbContext, Clock);
        ProductDetailService = new ProductDetailService(DbContext, PricingService, RecommendationService, RecentlyViewedService, WishlistService);
        CartService = new CartService(DbContext, PricingService, Clock);
    }

    public void Dispose() => DbContext.Dispose();
}

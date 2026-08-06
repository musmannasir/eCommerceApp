using ECommerceApp.Infrastructure.Addresses;
using ECommerceApp.Infrastructure.Carts;
using ECommerceApp.Infrastructure.Catalog;
using ECommerceApp.Infrastructure.Checkout;
using ECommerceApp.Infrastructure.Finance;
using ECommerceApp.Infrastructure.Inventory;
using ECommerceApp.Infrastructure.Marketing;
using ECommerceApp.Infrastructure.Orders;
using ECommerceApp.Infrastructure.Payments;
using ECommerceApp.Infrastructure.Pricing;
using ECommerceApp.Infrastructure.Reporting;
using ECommerceApp.Infrastructure.Returns;
using ECommerceApp.Infrastructure.Reviews;
using ECommerceApp.Infrastructure.Shipping;
using ECommerceApp.Infrastructure.Storefront;
using ECommerceApp.Infrastructure.Taxation;
using ECommerceApp.Infrastructure.Tests.TestSupport;
using ECommerceApp.Infrastructure.Wishlist;
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
    public PricingService PricingService { get; }
    public RecommendationService RecommendationService { get; }
    public FakeRecentlyViewedService RecentlyViewedService { get; }
    public ProductDetailService ProductDetailService { get; }
    public CartService CartService { get; }
    public WishlistService WishlistService { get; }
    public ReviewService ReviewService { get; }
    public ReturnService ReturnService { get; }
    public PromotionService PromotionService { get; }
    public TaxService TaxService { get; }
    public ShippingService ShippingService { get; }
    public CheckoutCalculationService CheckoutCalculationService { get; }
    public AddressService AddressService { get; }
    public SimulatedPaymentGateway PaymentGateway { get; }
    public InventoryService InventoryService { get; }
    public OrderService OrderService { get; }
    public FinanceService FinanceService { get; }
    public ReportingService ReportingService { get; }

    public CatalogTestHarness()
    {
        var options = new DbContextOptionsBuilder()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        Clock = new FakeClock();
        FileStorage = new FakeFileStorage();
        DbContext = new TestDbContext(options, new FakeCurrentUserService(), Clock);

        var storeSettingsService = new FakeStoreSettingsService();

        CategoryService = new CategoryService(DbContext);
        BrandService = new BrandService(DbContext);
        AttributeService = new ProductAttributeService(DbContext);
        ProductService = new ProductService(DbContext, FileStorage, Clock);
        HomePageBannerService = new HomePageBannerService(DbContext);
        RecentlyViewedService = new FakeRecentlyViewedService();
        HomePageService = new HomePageService(DbContext, RecentlyViewedService);
        CatalogBrowseService = new CatalogBrowseService(DbContext);
        PricingService = new PricingService(storeSettingsService);
        RecommendationService = new RecommendationService(DbContext);
        WishlistService = new WishlistService(DbContext, Clock);
        ReviewService = new ReviewService(DbContext, Clock);
        ProductDetailService = new ProductDetailService(DbContext, PricingService, RecommendationService, RecentlyViewedService, WishlistService, ReviewService);
        PromotionService = new PromotionService(DbContext);
        TaxService = new TaxService(DbContext, storeSettingsService);
        ShippingService = new ShippingService(DbContext, storeSettingsService);
        CheckoutCalculationService = new CheckoutCalculationService(PromotionService, TaxService, ShippingService);
        CartService = new CartService(DbContext, PricingService, PromotionService, CheckoutCalculationService, Clock);
        AddressService = new AddressService(DbContext, Clock);
        PaymentGateway = new SimulatedPaymentGateway(Clock);
        InventoryService = new InventoryService(DbContext, Clock, new FakeCurrentUserService());
        ReturnService = new ReturnService(DbContext, Clock, new FakeCurrentUserService(), InventoryService, PaymentGateway);
        OrderService = new OrderService(DbContext, PaymentGateway, InventoryService, ReturnService, Clock);
        FinanceService = new FinanceService(DbContext, Clock);
        ReportingService = new ReportingService(DbContext, Clock);
    }

    public void Dispose() => DbContext.Dispose();
}

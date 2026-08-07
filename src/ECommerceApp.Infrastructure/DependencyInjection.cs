using ECommerceApp.Application.Addresses;
using ECommerceApp.Application.Auth;
using ECommerceApp.Application.Carts;
using ECommerceApp.Application.Catalog;
using ECommerceApp.Application.Checkout;
using ECommerceApp.Application.Common.Interfaces;
using ECommerceApp.Application.Common.Options;
using ECommerceApp.Application.Configuration;
using ECommerceApp.Application.Finance;
using ECommerceApp.Application.Inventory;
using ECommerceApp.Application.Marketing;
using ECommerceApp.Application.Notifications;
using ECommerceApp.Application.Orders;
using ECommerceApp.Application.Payments;
using ECommerceApp.Application.Pricing;
using ECommerceApp.Application.Reporting;
using ECommerceApp.Application.Returns;
using ECommerceApp.Application.Reviews;
using ECommerceApp.Application.Security;
using ECommerceApp.Application.Shipping;
using ECommerceApp.Application.Storefront;
using ECommerceApp.Application.Taxation;
using ECommerceApp.Application.Users;
using ECommerceApp.Application.Wishlist;
using ECommerceApp.Infrastructure.Addresses;
using ECommerceApp.Infrastructure.Carts;
using ECommerceApp.Infrastructure.Catalog;
using ECommerceApp.Infrastructure.Checkout;
using ECommerceApp.Infrastructure.Common;
using ECommerceApp.Infrastructure.Configuration;
using ECommerceApp.Infrastructure.Email;
using ECommerceApp.Infrastructure.Finance;
using ECommerceApp.Infrastructure.HealthChecks;
using ECommerceApp.Infrastructure.Identity;
using ECommerceApp.Infrastructure.Inventory;
using ECommerceApp.Infrastructure.Marketing;
using ECommerceApp.Infrastructure.Notifications;
using ECommerceApp.Infrastructure.Orders;
using ECommerceApp.Infrastructure.Payments;
using ECommerceApp.Infrastructure.Persistence;
using ECommerceApp.Infrastructure.Pricing;
using ECommerceApp.Infrastructure.Reporting;
using ECommerceApp.Infrastructure.Returns;
using ECommerceApp.Infrastructure.Reviews;
using ECommerceApp.Infrastructure.Security;
using ECommerceApp.Infrastructure.Shipping;
using ECommerceApp.Infrastructure.Storage;
using ECommerceApp.Infrastructure.Storefront;
using ECommerceApp.Infrastructure.Taxation;
using ECommerceApp.Infrastructure.Users;
using ECommerceApp.Infrastructure.Wishlist;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerceApp.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers Infrastructure-layer services: the EF Core SQL Server context,
    /// ASP.NET Core Identity (store + password/lockout policy), the auth
    /// service, the UTC clock, and the database health check.
    /// <see cref="ICurrentUserService"/> is registered by the Web layer, which
    /// owns the HTTP context. Authentication schemes (cookie/JWT) and cookie
    /// hosting options are also configured by the Web layer, which owns the
    /// HTTP pipeline.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<ApplicationDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException(
                    "Connection string 'DefaultConnection' was not found. Configure it via User Secrets " +
                    "(see README.md) before running the application.");

            // SplitQuery is the safer default for this app - several queries load more
            // than one collection navigation off the same root (e.g. Product with its
            // Images and Variants), which EF Core otherwise warns about every time it
            // compiles one (MultipleCollectionIncludeWarning): SingleQuery's default
            // cartesian-product join can multiply row counts across collections,
            // returning far more data over the wire than the caller actually wants.
            // A handful of call sites already opt into AsSplitQuery() explicitly
            // (Milestone 17.2 confirmed this is a real, ongoing warning, not a
            // hypothetical one) - this makes that the default everywhere instead of
            // requiring every future multi-collection query to remember to opt in.
            //
            // EnableRetryOnFailure was investigated for Milestone 17.3 and
            // deliberately NOT enabled - see Architecture.md's "Reliability" section
            // for why. In short: it requires every existing manual
            // Database.BeginTransactionAsync() call site (InventoryService,
            // PurchaseOrderService - 7 methods) to be restructured so a retry can't
            // re-run entity-creation code (AddMovement, etc.) against a change
            // tracker that still holds a prior failed attempt's now-orphaned
            // `Added` entities, which would silently double-insert stock movements
            // on exactly the kind of transient failure this is meant to protect
            // against. That restructuring is real, correctness-sensitive work on
            // business-critical inventory code, not a one-line config flip.
            options.UseSqlServer(connectionString, sql =>
                sql.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName)
                   .UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery));
        });

        services.AddSingleton<IClock, SystemClock>();

        services.AddHealthChecks()
            .AddCheck<SqlServerHealthCheck>("sql-server", tags: new[] { "ready" });

        services.Configure<JwtSettings>(configuration.GetSection("Jwt"));
        services.Configure<OutboxOptions>(configuration.GetSection("Outbox"));

        services
            .AddIdentity<ApplicationUser, IdentityRole>(options =>
            {
                options.Password.RequiredLength = 10;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = true;
                options.Password.RequiredUniqueChars = 4;

                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);

                options.User.RequireUniqueEmail = true;
                options.SignIn.RequireConfirmedAccount = false;
            })
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<RoleAndAdminSeeder>();
        services.AddScoped<IStoreSettingsService, StoreSettingsService>();
        services.AddScoped<StoreSettingsSeeder>();
        services.AddSingleton<IEmailSender, DevEmailSender>();
        services.AddScoped<IEmailNotificationService, EmailNotificationService>();
        services.AddScoped<IOutboxProcessor, OutboxProcessor>();
        services.AddHostedService<OutboxProcessingBackgroundService>();

        services.Configure<FileStorageOptions>(configuration.GetSection("FileStorage"));
        services.AddSingleton<IFileStorage, LocalFileStorage>();
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<IBrandService, BrandService>();
        services.AddScoped<IProductAttributeService, ProductAttributeService>();
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<IInventoryService, InventoryService>();
        services.AddScoped<ISupplierService, SupplierService>();
        services.AddScoped<IPurchaseOrderService, PurchaseOrderService>();
        services.AddScoped<IHomePageBannerService, HomePageBannerService>();
        services.AddScoped<IPromotionService, PromotionService>();
        services.AddScoped<ITaxService, TaxService>();
        services.AddScoped<IShippingService, ShippingService>();
        services.AddScoped<ICheckoutCalculationService, CheckoutCalculationService>();
        services.AddScoped<IHomePageService, HomePageService>();
        services.AddScoped<ICatalogBrowseService, CatalogBrowseService>();
        services.AddScoped<IProductDetailService, ProductDetailService>();
        services.AddScoped<IRecommendationService, RecommendationService>();
        // Scoped (not Singleton) because it depends on the scoped, DB-backed
        // IStoreSettingsService - it remains a stateless pure calculator with
        // no internal mutable state of its own.
        services.AddScoped<IPricingService, PricingService>();
        services.AddScoped<ICartService, CartService>();
        services.AddScoped<IWishlistService, WishlistService>();
        services.AddScoped<IAddressService, AddressService>();
        services.AddSingleton<IPaymentGateway, SimulatedPaymentGateway>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<IReviewService, ReviewService>();
        services.AddScoped<IReturnService, ReturnService>();
        services.AddScoped<IFinanceService, FinanceService>();
        services.AddScoped<IReportingService, ReportingService>();
        services.AddScoped<IUserManagementService, UserManagementService>();
        services.AddScoped<IAuditLogService, AuditLogService>();

        return services;
    }
}

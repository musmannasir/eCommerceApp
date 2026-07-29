using ECommerceApp.Application.Addresses;
using ECommerceApp.Application.Auth;
using ECommerceApp.Application.Carts;
using ECommerceApp.Application.Catalog;
using ECommerceApp.Application.Checkout;
using ECommerceApp.Application.Common.Interfaces;
using ECommerceApp.Application.Common.Options;
using ECommerceApp.Application.Inventory;
using ECommerceApp.Application.Marketing;
using ECommerceApp.Application.Pricing;
using ECommerceApp.Application.Shipping;
using ECommerceApp.Application.Storefront;
using ECommerceApp.Application.Taxation;
using ECommerceApp.Application.Wishlist;
using ECommerceApp.Infrastructure.Addresses;
using ECommerceApp.Infrastructure.Carts;
using ECommerceApp.Infrastructure.Catalog;
using ECommerceApp.Infrastructure.Checkout;
using ECommerceApp.Infrastructure.Common;
using ECommerceApp.Infrastructure.Email;
using ECommerceApp.Infrastructure.HealthChecks;
using ECommerceApp.Infrastructure.Identity;
using ECommerceApp.Infrastructure.Inventory;
using ECommerceApp.Infrastructure.Marketing;
using ECommerceApp.Infrastructure.Persistence;
using ECommerceApp.Infrastructure.Pricing;
using ECommerceApp.Infrastructure.Security;
using ECommerceApp.Infrastructure.Shipping;
using ECommerceApp.Infrastructure.Storage;
using ECommerceApp.Infrastructure.Storefront;
using ECommerceApp.Infrastructure.Taxation;
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

            options.UseSqlServer(connectionString, sql =>
                sql.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName));
        });

        services.AddSingleton<IClock, SystemClock>();

        services.AddHealthChecks()
            .AddCheck<SqlServerHealthCheck>("sql-server", tags: new[] { "ready" });

        services.Configure<JwtSettings>(configuration.GetSection("Jwt"));

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
        services.AddSingleton<IEmailSender, DevEmailSender>();

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
        services.AddSingleton<IPricingService, PricingService>();
        services.AddScoped<ICartService, CartService>();
        services.AddScoped<IWishlistService, WishlistService>();
        services.AddScoped<IAddressService, AddressService>();

        return services;
    }
}

using System.Text;
using System.Threading.RateLimiting;
using ECommerceApp.Application;
using ECommerceApp.Application.Common.Interfaces;
using ECommerceApp.Application.Common.Options;
using ECommerceApp.Domain.Security;
using ECommerceApp.Infrastructure;
using ECommerceApp.Infrastructure.Security;
using ECommerceApp.Web.Middleware;
using ECommerceApp.Web.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithMachineName());

    builder.Services.AddControllersWithViews();
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddMemoryCache();

    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);
    builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

    builder.Services.AddProblemDetails();
    builder.Services.AddExceptionHandler<ApiExceptionHandler>();

    // AddInfrastructure() already called AddIdentity<ApplicationUser, IdentityRole>(),
    // which registered cookie authentication as the default scheme. JWT Bearer is added
    // alongside it for /api/v1 endpoints, which opt in explicitly via [Authorize(AuthenticationSchemes = ...)].
    //
    // Jwt settings are read inside this configure callback (not captured before Build())
    // so they reflect the final, fully-merged IConfiguration - including test-time overrides
    // applied via WebApplicationFactory, which only take effect after Build() completes.
    builder.Services
        .AddAuthentication()
        .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
        {
            var jwtSettings = builder.Configuration.GetSection("Jwt").Get<JwtSettings>() ?? new JwtSettings();

            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwtSettings.Issuer,
                ValidateAudience = true,
                ValidAudience = jwtSettings.Audience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKeyResolver = (_, _, _, _) =>
                {
                    if (string.IsNullOrWhiteSpace(jwtSettings.Key))
                    {
                        throw new InvalidOperationException(
                            "Jwt:Key is not configured. Set it via User Secrets (see README.md) before using JWT authentication.");
                    }

                    return [new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Key))];
                },
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30),
            };
        });

    builder.Services.ConfigureApplicationCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Home/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromDays(14);
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
    });

    // Shortened from the 30-minute default so RevokeAllSessionsAsync's security-stamp bump
    // takes effect quickly for cookie sessions, not just for the next long-lived cookie refresh.
    builder.Services.Configure<SecurityStampValidatorOptions>(options =>
        options.ValidationInterval = TimeSpan.FromMinutes(5));

    builder.Services.AddAuthorization(options =>
    {
        options.AddPolicy(Policies.CanManageCatalog, p => p.RequireRole(Roles.SuperAdmin, Roles.Admin, Roles.CatalogManager));
        options.AddPolicy(Policies.CanManageInventory, p => p.RequireRole(Roles.SuperAdmin, Roles.Admin, Roles.InventoryManager));
        options.AddPolicy(Policies.CanManageOrders, p => p.RequireRole(Roles.SuperAdmin, Roles.Admin, Roles.OrderManager, Roles.CustomerSupport));
        options.AddPolicy(Policies.CanManageUsers, p => p.RequireRole(Roles.SuperAdmin, Roles.Admin));
        options.AddPolicy(Policies.CanViewFinancialReports, p => p.RequireRole(Roles.SuperAdmin, Roles.Admin));
        options.AddPolicy(Policies.CanProcessRefunds, p => p.RequireRole(Roles.SuperAdmin, Roles.Admin, Roles.OrderManager));
    });

    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

        // Partitioned per client IP: an un-partitioned limiter would let one abusive
        // client exhaust the shared quota and lock out every other user. Configuration
        // is resolved per-request (not captured before Build()) so it reflects the
        // final, fully-merged IConfiguration - including test-time overrides applied
        // via WebApplicationFactory, which only take effect after Build() completes.
        options.AddPolicy("auth", httpContext =>
        {
            var configuration = httpContext.RequestServices.GetRequiredService<IConfiguration>();
            var permitLimit = configuration.GetValue("RateLimiting:AuthPermitLimit", 5);
            var windowSeconds = configuration.GetValue("RateLimiting:AuthWindowSeconds", 60);

            return RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = permitLimit,
                    Window = TimeSpan.FromSeconds(windowSeconds),
                    QueueLimit = 0,
                });
        });
    });

    var app = builder.Build();

    app.UseMiddleware<CorrelationIdMiddleware>();

    if (app.Environment.IsDevelopment())
    {
        app.UseDeveloperExceptionPage();
    }
    else
    {
        app.UseExceptionHandler("/Home/Error");
        app.UseHsts();
    }

    app.UseStatusCodePagesWithReExecute("/Home/StatusCode/{0}");

    app.UseHttpsRedirection();
    app.UseRouting();

    app.UseRateLimiter();

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapStaticAssets();

    app.MapControllerRoute(
        name: "areas",
        pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}")
        .WithStaticAssets();

    app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}")
        .WithStaticAssets();

    app.MapHealthChecks("/health/live", new HealthCheckOptions
    {
        Predicate = _ => false,
    });

    app.MapHealthChecks("/health/ready", new HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("ready"),
    });

    using (var scope = app.Services.CreateScope())
    {
        try
        {
            var seeder = scope.ServiceProvider.GetRequiredService<RoleAndAdminSeeder>();
            await seeder.SeedAsync();
        }
        catch (Exception ex)
        {
            app.Logger.LogError(
                ex,
                "Role/SuperAdmin seeding failed - the database may not be migrated yet or is unreachable. " +
                "The app will keep starting; run `dotnet ef database update` and restart once the database is ready.");
        }
    }

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "ECommerceApp.Web terminated unexpectedly during startup.");
}
finally
{
    Log.CloseAndFlush();
}

namespace ECommerceApp.Web
{
    /// <summary>Marker type so <c>WebApplicationFactory&lt;Program&gt;</c> can target this entry point from tests.</summary>
    public partial class Program;
}

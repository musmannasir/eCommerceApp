using ECommerceApp.Application.Common.Interfaces;
using ECommerceApp.Application.Common.Options;
using ECommerceApp.Domain.Security;
using ECommerceApp.Infrastructure.Identity;
using ECommerceApp.Infrastructure.Persistence;
using ECommerceApp.Infrastructure.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ECommerceApp.Infrastructure.Tests.TestSupport;

/// <summary>
/// Builds a real ASP.NET Core Identity stack (UserManager/SignInManager/RoleManager)
/// backed by the EF Core InMemory provider, so <see cref="AuthService"/> can be
/// exercised end-to-end (lockout counting, password hashing, role checks) without
/// a real SQL Server instance.
/// </summary>
public sealed class AuthServiceTestHarness : IDisposable
{
    public const int TestMaxFailedAccessAttempts = 3;
    public const string ValidPassword = "Str0ng!Passw0rd";

    private readonly ServiceProvider _provider;
    private readonly IServiceScope _scope;

    public FakeClock Clock { get; }
    public TestDbContext DbContext { get; }
    public UserManager<ApplicationUser> UserManager { get; }
    public RoleManager<IdentityRole> RoleManager { get; }
    public AuthService AuthService { get; }

    private AuthServiceTestHarness(ServiceProvider provider, IServiceScope scope, FakeClock clock)
    {
        _provider = provider;
        _scope = scope;
        Clock = clock;
        DbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();
        UserManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        RoleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        AuthService = scope.ServiceProvider.GetRequiredService<AuthService>();
    }

    public static async Task<AuthServiceTestHarness> CreateAsync()
    {
        var clock = new FakeClock();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IHttpContextAccessor>(new HttpContextAccessor { HttpContext = new DefaultHttpContext() });
        services.AddSingleton<IClock>(clock);
        services.AddScoped<ICurrentUserService>(_ => new FakeCurrentUserService());
        services.AddScoped<AuthService>();

        services.Configure<JwtSettings>(o =>
        {
            o.Issuer = "ECommerceApp.Tests";
            o.Audience = "ECommerceApp.Tests.Clients";
            o.Key = "test-signing-key-please-do-not-use-in-production-0123456789";
            o.AccessTokenMinutes = 15;
            o.RefreshTokenDays = 7;
        });

        services.AddDbContext<TestDbContext>(options => options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddScoped<ApplicationDbContext>(sp => sp.GetRequiredService<TestDbContext>());

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
                options.Lockout.MaxFailedAccessAttempts = TestMaxFailedAccessAttempts;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);

                options.User.RequireUniqueEmail = true;
                options.SignIn.RequireConfirmedAccount = false;
            })
            .AddEntityFrameworkStores<TestDbContext>()
            .AddDefaultTokenProviders();

        var provider = services.BuildServiceProvider();
        var scope = provider.CreateScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();
        await dbContext.Database.EnsureCreatedAsync();

        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in Roles.All)
        {
            await roleManager.CreateAsync(new IdentityRole(role));
        }

        return new AuthServiceTestHarness(provider, scope, clock);
    }

    public void Dispose()
    {
        _scope.Dispose();
        _provider.Dispose();
    }
}

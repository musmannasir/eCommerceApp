using ECommerceApp.Application.Security.Models;
using ECommerceApp.Domain.Security;
using ECommerceApp.Infrastructure.Identity;
using ECommerceApp.Infrastructure.Security;
using ECommerceApp.Infrastructure.Tests.TestSupport;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace ECommerceApp.Infrastructure.Tests.Security;

public class AuditLogServiceTests : IDisposable
{
    private readonly TestDbContext _dbContext;
    private readonly FakeClock _clock = new();
    private readonly AuditLogService _service;

    public AuditLogServiceTests()
    {
        var options = new DbContextOptionsBuilder().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        _dbContext = new TestDbContext(options, new FakeCurrentUserService(), _clock);
        _service = new AuditLogService(_dbContext, _clock);
    }

    public void Dispose() => _dbContext.Dispose();

    private async Task<string> SeedUserAsync(string email)
    {
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = email,
            Email = email,
            FirstName = "Test",
            LastName = "User",
            IsActive = true,
            CreatedAtUtc = _clock.UtcNow,
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();
        return user.Id;
    }

    private async Task AddEventAsync(string? userId, SecurityEventType type, bool succeeded, DateTime occurredAtUtc, string? details = null)
    {
        _dbContext.SecurityAuditEvents.Add(new SecurityAuditEvent
        {
            UserId = userId,
            EventType = type,
            Succeeded = succeeded,
            OccurredAtUtc = occurredAtUtc,
            Details = details,
        });
        await _dbContext.SaveChangesAsync();
    }

    [Fact]
    public async Task GetPagedAsync_returns_events_with_the_users_email_resolved()
    {
        var userId = await SeedUserAsync("someone@example.com");
        await AddEventAsync(userId, SecurityEventType.LoginSuccess, true, _clock.UtcNow);

        var result = await _service.GetPagedAsync(new AuditLogQuery());

        result.Items.Should().ContainSingle(e => e.UserEmail == "someone@example.com");
    }

    [Fact]
    public async Task GetPagedAsync_leaves_UserEmail_null_for_an_event_with_no_user()
    {
        await AddEventAsync(null, SecurityEventType.LoginFailure, false, _clock.UtcNow, "Unknown email.");

        var result = await _service.GetPagedAsync(new AuditLogQuery());

        result.Items.Should().ContainSingle(e => e.UserEmail == null && e.Details == "Unknown email.");
    }

    [Fact]
    public async Task GetPagedAsync_filters_by_event_type()
    {
        var userId = await SeedUserAsync("filter@example.com");
        await AddEventAsync(userId, SecurityEventType.LoginSuccess, true, _clock.UtcNow);
        await AddEventAsync(userId, SecurityEventType.Logout, true, _clock.UtcNow);

        var result = await _service.GetPagedAsync(new AuditLogQuery { EventType = SecurityEventType.Logout });

        result.Items.Should().ContainSingle(e => e.EventType == SecurityEventType.Logout);
    }

    [Fact]
    public async Task GetPagedAsync_filters_by_succeeded()
    {
        var userId = await SeedUserAsync("outcome@example.com");
        await AddEventAsync(userId, SecurityEventType.LoginSuccess, true, _clock.UtcNow);
        await AddEventAsync(userId, SecurityEventType.LoginFailure, false, _clock.UtcNow);

        var failedOnly = await _service.GetPagedAsync(new AuditLogQuery { Succeeded = false });

        failedOnly.Items.Should().ContainSingle(e => e.EventType == SecurityEventType.LoginFailure);
    }

    [Fact]
    public async Task GetPagedAsync_search_matches_the_resolved_user_email()
    {
        var matchId = await SeedUserAsync("findable@example.com");
        var otherId = await SeedUserAsync("someoneelse@example.com");
        await AddEventAsync(matchId, SecurityEventType.LoginSuccess, true, _clock.UtcNow);
        await AddEventAsync(otherId, SecurityEventType.LoginSuccess, true, _clock.UtcNow);

        var result = await _service.GetPagedAsync(new AuditLogQuery { Search = "findable" });

        result.Items.Should().ContainSingle(e => e.UserEmail == "findable@example.com");
    }

    [Fact]
    public async Task GetPagedAsync_excludes_events_outside_the_date_range()
    {
        var userId = await SeedUserAsync("dated@example.com");
        await AddEventAsync(userId, SecurityEventType.LoginSuccess, true, _clock.UtcNow.AddDays(-60));
        await AddEventAsync(userId, SecurityEventType.LoginSuccess, true, _clock.UtcNow);

        var defaultRange = await _service.GetPagedAsync(new AuditLogQuery());

        defaultRange.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task GetPagedAsync_default_range_is_the_30_days_ending_today()
    {
        var userId = await SeedUserAsync("range@example.com");
        await AddEventAsync(userId, SecurityEventType.LoginSuccess, true, _clock.UtcNow.AddDays(-29));
        await AddEventAsync(userId, SecurityEventType.LoginSuccess, true, _clock.UtcNow.AddDays(-31));

        var result = await _service.GetPagedAsync(new AuditLogQuery());

        result.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task GetAllAsync_matches_GetPagedAsync_filters_but_returns_every_matching_row_unpaginated()
    {
        var userId = await SeedUserAsync("exportme@example.com");
        for (var i = 0; i < 25; i++)
        {
            await AddEventAsync(userId, SecurityEventType.LoginSuccess, true, _clock.UtcNow);
        }

        var paged = await _service.GetPagedAsync(new AuditLogQuery());
        var all = await _service.GetAllAsync(new AuditLogQuery());

        paged.Items.Should().HaveCount(20);
        all.Should().HaveCount(25);
    }

    [Fact]
    public async Task GetPagedAsync_returns_newest_first()
    {
        var userId = await SeedUserAsync("ordering@example.com");
        await AddEventAsync(userId, SecurityEventType.LoginSuccess, true, _clock.UtcNow.AddHours(-2));
        await AddEventAsync(userId, SecurityEventType.Logout, true, _clock.UtcNow.AddHours(-1));

        var result = await _service.GetPagedAsync(new AuditLogQuery());

        result.Items[0].EventType.Should().Be(SecurityEventType.Logout);
        result.Items[1].EventType.Should().Be(SecurityEventType.LoginSuccess);
    }
}

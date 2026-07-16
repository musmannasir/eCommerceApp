using ECommerceApp.Application.Common.Interfaces;
using ECommerceApp.Domain.Common;
using ECommerceApp.Domain.Security;
using ECommerceApp.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ECommerceApp.Infrastructure.Persistence;

/// <summary>
/// The single EF Core context for the application, built on
/// <see cref="IdentityDbContext{TUser,TRole,TKey}"/> so Identity's own tables
/// share this context and its migration history. Business entities are added
/// as <see cref="DbSet{TEntity}"/> properties by the milestone that introduces
/// them.
/// </summary>
public class ApplicationDbContext : IdentityDbContext<ApplicationUser, IdentityRole, string>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IClock _clock;

    public ApplicationDbContext(
        DbContextOptions options,
        ICurrentUserService currentUserService,
        IClock clock)
        : base(options)
    {
        _currentUserService = currentUserService;
        _clock = clock;
    }

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<UserSession> UserSessions => Set<UserSession>();
    public DbSet<SecurityAuditEvent> SecurityAuditEvents => Set<SecurityAuditEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(u => u.FirstName).HasMaxLength(100);
            entity.Property(u => u.LastName).HasMaxLength(100);
            entity.HasIndex(u => u.NormalizedEmail).IsUnique();
        });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.Property(t => t.TokenHash).HasMaxLength(256).IsRequired();
            entity.Property(t => t.CreatedByIp).HasMaxLength(64);
            entity.Property(t => t.RevokedByIp).HasMaxLength(64);
            entity.Property(t => t.ReasonRevoked).HasMaxLength(200);
            entity.HasIndex(t => t.TokenHash).IsUnique();
            entity.HasIndex(t => t.UserId);
        });

        modelBuilder.Entity<UserSession>(entity =>
        {
            entity.Property(s => s.IpAddress).HasMaxLength(64);
            entity.Property(s => s.UserAgent).HasMaxLength(512);
            entity.HasIndex(s => s.UserId);
        });

        modelBuilder.Entity<SecurityAuditEvent>(entity =>
        {
            entity.Property(e => e.IpAddress).HasMaxLength(64);
            entity.Property(e => e.UserAgent).HasMaxLength(512);
            entity.Property(e => e.Details).HasMaxLength(1000);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.OccurredAtUtc);
        });

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(ISoftDeletable).IsAssignableFrom(entityType.ClrType))
            {
                modelBuilder.Entity(entityType.ClrType)
                    .HasQueryFilter(SoftDeleteFilterFactory.Build(entityType.ClrType));
            }
        }
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyAuditInformationAndSoftDelete();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        ApplyAuditInformationAndSoftDelete();
        return base.SaveChanges();
    }

    private void ApplyAuditInformationAndSoftDelete()
    {
        var utcNow = _clock.UtcNow;
        var userId = _currentUserService.UserId;

        foreach (var entry in ChangeTracker.Entries<IAuditableEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAtUtc = utcNow;
                    entry.Entity.CreatedByUserId = userId;
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAtUtc = utcNow;
                    entry.Entity.UpdatedByUserId = userId;
                    break;
            }
        }

        foreach (var entry in ChangeTracker.Entries<ISoftDeletable>())
        {
            if (entry.State == EntityState.Deleted)
            {
                entry.State = EntityState.Modified;
                entry.Entity.IsDeleted = true;
            }
        }
    }
}

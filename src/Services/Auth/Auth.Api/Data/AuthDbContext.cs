using DarkVelocity.Auth.Api.Entities;
using DarkVelocity.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DarkVelocity.Auth.Api.Data;

public class AuthDbContext : BaseDbContext
{
    public AuthDbContext(DbContextOptions<AuthDbContext> options) : base(options)
    {
    }

    public DbSet<Location> Locations => Set<Location>();
    public DbSet<UserGroup> UserGroups => Set<UserGroup>();
    public DbSet<PosUser> PosUsers => Set<PosUser>();
    public DbSet<UserLocationAccess> UserLocationAccess => Set<UserLocationAccess>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Location>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Timezone).HasMaxLength(100).IsRequired();
            entity.Property(e => e.CurrencyCode).HasMaxLength(3).IsRequired();
            entity.Property(e => e.Address).HasMaxLength(500);
            entity.Property(e => e.Phone).HasMaxLength(50);
        });

        modelBuilder.Entity<UserGroup>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(500);
        });

        modelBuilder.Entity<PosUser>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Username).HasMaxLength(100).IsRequired();
            entity.Property(e => e.FirstName).HasMaxLength(100).IsRequired();
            entity.Property(e => e.LastName).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Email).HasMaxLength(255);
            entity.Property(e => e.PinHash).HasMaxLength(255).IsRequired();
            entity.Property(e => e.QrCodeToken).HasMaxLength(255);

            entity.HasIndex(e => e.Username).IsUnique();
            entity.HasIndex(e => e.QrCodeToken).IsUnique();

            entity.HasOne(e => e.UserGroup)
                .WithMany(g => g.Users)
                .HasForeignKey(e => e.UserGroupId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.HomeLocation)
                .WithMany(l => l.Users)
                .HasForeignKey(e => e.HomeLocationId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<UserLocationAccess>(entity =>
        {
            entity.HasKey(e => new { e.UserId, e.LocationId });

            entity.HasOne(e => e.User)
                .WithMany(u => u.LocationAccess)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Location)
                .WithMany(l => l.UserLocationAccess)
                .HasForeignKey(e => e.LocationId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}

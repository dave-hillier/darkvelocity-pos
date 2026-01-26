using DarkVelocity.Location.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace DarkVelocity.Location.Api.Data;

public class LocationDbContext : DbContext
{
    public LocationDbContext(DbContextOptions<LocationDbContext> options) : base(options)
    {
    }

    public DbSet<Entities.Location> Locations => Set<Entities.Location>();
    public DbSet<LocationSettings> LocationSettings => Set<LocationSettings>();
    public DbSet<OperatingHours> OperatingHours => Set<OperatingHours>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Location
        modelBuilder.Entity<Entities.Location>(entity =>
        {
            entity.HasIndex(e => e.Code).IsUnique();
            entity.Property(e => e.Name).HasMaxLength(200);
            entity.Property(e => e.Code).HasMaxLength(50);
            entity.Property(e => e.Timezone).HasMaxLength(100);
            entity.Property(e => e.CurrencyCode).HasMaxLength(3);
            entity.Property(e => e.CurrencySymbol).HasMaxLength(5);
            entity.Property(e => e.Phone).HasMaxLength(50);
            entity.Property(e => e.Email).HasMaxLength(255);
            entity.Property(e => e.Website).HasMaxLength(255);
            entity.Property(e => e.AddressLine1).HasMaxLength(200);
            entity.Property(e => e.AddressLine2).HasMaxLength(200);
            entity.Property(e => e.City).HasMaxLength(100);
            entity.Property(e => e.State).HasMaxLength(100);
            entity.Property(e => e.PostalCode).HasMaxLength(20);
            entity.Property(e => e.Country).HasMaxLength(100);
            entity.Property(e => e.TaxNumber).HasMaxLength(50);
            entity.Property(e => e.BusinessName).HasMaxLength(200);

            entity.HasOne(e => e.Settings)
                .WithOne(s => s.Location)
                .HasForeignKey<LocationSettings>(s => s.LocationId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.OperatingHours)
                .WithOne(o => o.Location)
                .HasForeignKey(o => o.LocationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // LocationSettings
        modelBuilder.Entity<LocationSettings>(entity =>
        {
            entity.HasIndex(e => e.LocationId).IsUnique();
            entity.Property(e => e.DefaultTaxRate).HasPrecision(5, 2);
            entity.Property(e => e.ReceiptHeader).HasMaxLength(500);
            entity.Property(e => e.ReceiptFooter).HasMaxLength(500);
            entity.Property(e => e.OrderNumberPrefix).HasMaxLength(10);
        });

        // OperatingHours
        modelBuilder.Entity<OperatingHours>(entity =>
        {
            entity.HasIndex(e => new { e.LocationId, e.DayOfWeek }).IsUnique();
        });
    }
}

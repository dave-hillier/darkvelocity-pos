using DarkVelocity.Hardware.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace DarkVelocity.Hardware.Api.Data;

public class HardwareDbContext : DbContext
{
    public HardwareDbContext(DbContextOptions<HardwareDbContext> options) : base(options)
    {
    }

    public DbSet<Printer> Printers => Set<Printer>();
    public DbSet<CashDrawer> CashDrawers => Set<CashDrawer>();
    public DbSet<PosDevice> PosDevices => Set<PosDevice>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Printer
        modelBuilder.Entity<Printer>(entity =>
        {
            entity.HasIndex(e => new { e.LocationId, e.Name }).IsUnique();
            entity.Property(e => e.Name).HasMaxLength(100);
            entity.Property(e => e.PrinterType).HasMaxLength(50);
            entity.Property(e => e.ConnectionType).HasMaxLength(50);
            entity.Property(e => e.IpAddress).HasMaxLength(45);
            entity.Property(e => e.MacAddress).HasMaxLength(17);
            entity.Property(e => e.UsbVendorId).HasMaxLength(10);
            entity.Property(e => e.UsbProductId).HasMaxLength(10);
            entity.Property(e => e.CharacterSet).HasMaxLength(20);
        });

        // CashDrawer
        modelBuilder.Entity<CashDrawer>(entity =>
        {
            entity.HasIndex(e => new { e.LocationId, e.Name }).IsUnique();
            entity.Property(e => e.Name).HasMaxLength(100);
            entity.Property(e => e.ConnectionType).HasMaxLength(50);
            entity.Property(e => e.IpAddress).HasMaxLength(45);

            entity.HasOne(e => e.Printer)
                .WithMany(p => p.CashDrawers)
                .HasForeignKey(e => e.PrinterId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // PosDevice
        modelBuilder.Entity<PosDevice>(entity =>
        {
            entity.HasIndex(e => e.DeviceId).IsUnique();
            entity.HasIndex(e => new { e.LocationId, e.Name }).IsUnique();
            entity.Property(e => e.Name).HasMaxLength(100);
            entity.Property(e => e.DeviceId).HasMaxLength(100);
            entity.Property(e => e.DeviceType).HasMaxLength(50);
            entity.Property(e => e.Model).HasMaxLength(100);
            entity.Property(e => e.OsVersion).HasMaxLength(50);
            entity.Property(e => e.AppVersion).HasMaxLength(20);

            entity.HasOne(e => e.DefaultPrinter)
                .WithMany()
                .HasForeignKey(e => e.DefaultPrinterId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.DefaultCashDrawer)
                .WithMany()
                .HasForeignKey(e => e.DefaultCashDrawerId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }
}

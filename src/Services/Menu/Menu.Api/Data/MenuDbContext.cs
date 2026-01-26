using DarkVelocity.Menu.Api.Entities;
using DarkVelocity.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DarkVelocity.Menu.Api.Data;

public class MenuDbContext : BaseDbContext
{
    public MenuDbContext(DbContextOptions<MenuDbContext> options) : base(options)
    {
    }

    public DbSet<AccountingGroup> AccountingGroups => Set<AccountingGroup>();
    public DbSet<MenuCategory> Categories => Set<MenuCategory>();
    public DbSet<MenuItem> Items => Set<MenuItem>();
    public DbSet<MenuDefinition> Menus => Set<MenuDefinition>();
    public DbSet<MenuScreen> Screens => Set<MenuScreen>();
    public DbSet<MenuButton> Buttons => Set<MenuButton>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<AccountingGroup>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.TaxRate).HasPrecision(5, 4);
        });

        modelBuilder.Entity<MenuCategory>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.Color).HasMaxLength(50);
            entity.HasIndex(e => new { e.LocationId, e.Name });
        });

        modelBuilder.Entity<MenuItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.Price).HasPrecision(12, 4);
            entity.Property(e => e.ImageUrl).HasMaxLength(500);
            entity.Property(e => e.Sku).HasMaxLength(50);

            entity.HasIndex(e => new { e.LocationId, e.Sku }).IsUnique();
            entity.HasIndex(e => e.LocationId);

            entity.HasOne(e => e.Category)
                .WithMany(c => c.Items)
                .HasForeignKey(e => e.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.AccountingGroup)
                .WithMany(g => g.Items)
                .HasForeignKey(e => e.AccountingGroupId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<MenuDefinition>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.HasIndex(e => e.LocationId);
        });

        modelBuilder.Entity<MenuScreen>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Color).HasMaxLength(50);

            entity.HasOne(e => e.Menu)
                .WithMany(m => m.Screens)
                .HasForeignKey(e => e.MenuId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MenuButton>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Label).HasMaxLength(100);
            entity.Property(e => e.Color).HasMaxLength(50);
            entity.Property(e => e.ButtonType).HasMaxLength(50);

            entity.HasOne(e => e.Screen)
                .WithMany(s => s.Buttons)
                .HasForeignKey(e => e.ScreenId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Item)
                .WithMany(i => i.Buttons)
                .HasForeignKey(e => e.ItemId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }
}

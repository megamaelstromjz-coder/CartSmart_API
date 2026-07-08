using CartSmart.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace CartSmart.Api.Data;

public class CartSmartDbContext(DbContextOptions<CartSmartDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<ExternalLogin> ExternalLogins => Set<ExternalLogin>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<ShoppingList> ShoppingLists => Set<ShoppingList>();
    public DbSet<ShoppingListItem> ShoppingListItems => Set<ShoppingListItem>();
    public DbSet<ProductReferenceItem> ProductReferenceItems => Set<ProductReferenceItem>();
    public DbSet<ReferenceListVersion> ReferenceListVersions => Set<ReferenceListVersion>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(e =>
        {
            e.HasIndex(u => u.Email).IsUnique();
            e.Property(u => u.Email).HasMaxLength(320);
        });

        modelBuilder.Entity<ExternalLogin>(e =>
        {
            e.HasIndex(l => new { l.Provider, l.ProviderUserId }).IsUnique();
            e.HasOne(l => l.User)
                .WithMany(u => u.ExternalLogins)
                .HasForeignKey(l => l.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RefreshToken>(e =>
        {
            e.HasIndex(t => t.TokenHash).IsUnique();
            e.HasOne(t => t.User)
                .WithMany(u => u.RefreshTokens)
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(t => t.Device)
                .WithMany()
                .HasForeignKey(t => t.DeviceId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Device>(e =>
        {
            e.HasIndex(d => new { d.UserId, d.ClientDeviceId }).IsUnique();
            e.HasOne(d => d.User)
                .WithMany(u => u.Devices)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ShoppingList>(e =>
        {
            e.HasIndex(l => new { l.UserId, l.UpdatedAt });
            e.Property(l => l.Version).IsRowVersion();
            e.HasOne(l => l.User)
                .WithMany(u => u.ShoppingLists)
                .HasForeignKey(l => l.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ShoppingListItem>(e =>
        {
            e.HasIndex(i => new { i.ShoppingListId, i.UpdatedAt });
            e.Property(i => i.Version).IsRowVersion();
            e.Property(i => i.Quantity).HasPrecision(18, 3);
            e.HasOne(i => i.ShoppingList)
                .WithMany(l => l.Items)
                .HasForeignKey(i => i.ShoppingListId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ProductReferenceItem>(e =>
        {
            e.HasIndex(p => p.Category);
        });
    }
}

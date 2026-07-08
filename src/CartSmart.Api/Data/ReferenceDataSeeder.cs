using System.Text.Json;
using CartSmart.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace CartSmart.Api.Data;

public static class ReferenceDataSeeder
{
    private record SeedProduct(string Name, string Category);

    public static async Task SeedAsync(CartSmartDbContext db, CancellationToken cancellationToken = default)
    {
        if (await db.ReferenceListVersions.AnyAsync(cancellationToken))
        {
            return;
        }

        var seedPath = Path.Combine(AppContext.BaseDirectory, "Data", "Seed", "reference-products.json");
        var json = await File.ReadAllTextAsync(seedPath, cancellationToken);
        var products = JsonSerializer.Deserialize<List<SeedProduct>>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? [];

        db.ProductReferenceItems.AddRange(products.Select(p => new ProductReferenceItem
        {
            Id = Guid.NewGuid(),
            Name = p.Name,
            Category = p.Category,
            IsActive = true,
        }));

        db.ReferenceListVersions.Add(new ReferenceListVersion
        {
            Version = 1,
            PublishedAt = DateTimeOffset.UtcNow,
        });

        await db.SaveChangesAsync(cancellationToken);
    }
}

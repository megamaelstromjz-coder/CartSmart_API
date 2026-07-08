namespace CartSmart.Api.Models;

public class ShoppingList
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public required string Name { get; set; }

    // Bumped on every write; lets clients detect changes with a single delta query.
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    // Soft delete so multi-device sync can propagate deletions via the same delta feed.
    public bool IsDeleted { get; set; }

    // Optimistic concurrency token for last-write-wins conflict detection across devices.
    public uint Version { get; set; }

    public List<ShoppingListItem> Items { get; set; } = [];
}

namespace CartSmart.Api.Models;

public class ShoppingListItem
{
    public Guid Id { get; set; }
    public Guid ShoppingListId { get; set; }
    public ShoppingList ShoppingList { get; set; } = null!;

    public required string Name { get; set; }
    public decimal? Quantity { get; set; }
    public string? Unit { get; set; }
    public string? Category { get; set; }
    public bool IsChecked { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public uint Version { get; set; }
}

namespace CartSmart.Api.Models;

// A single bundled product/category autocomplete entry. Not user data — this is
// reference data shipped to every client, versioned as a whole via ReferenceListVersion.
public class ProductReferenceItem
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public required string Category { get; set; }
    public bool IsActive { get; set; } = true;
}

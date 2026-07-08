namespace CartSmart.Api.Models;

// Single-row table tracking the currently published version of the product/category
// reference list, so clients can cheaply check "is there anything new" without
// downloading the full list every time.
public class ReferenceListVersion
{
    public int Id { get; set; }
    public int Version { get; set; }
    public DateTimeOffset PublishedAt { get; set; }
}

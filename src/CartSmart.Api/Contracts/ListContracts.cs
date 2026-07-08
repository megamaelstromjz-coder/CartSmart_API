namespace CartSmart.Api.Contracts;

public record UpsertListRequest(string Name);

public record UpsertListItemRequest(string Name, decimal? Quantity, string? Unit, string? Category, bool IsChecked);

public record ShoppingListItemResponse(
    Guid Id, Guid ShoppingListId, string Name, decimal? Quantity, string? Unit, string? Category,
    bool IsChecked, DateTimeOffset UpdatedAt, DateTimeOffset CreatedAt, bool IsDeleted);

public record ShoppingListResponse(
    Guid Id, string Name, DateTimeOffset UpdatedAt, DateTimeOffset CreatedAt, bool IsDeleted,
    List<ShoppingListItemResponse> Items);

// SchemaVersion is bumped only on breaking changes to this payload shape; additive fields
// (e.g. a future server-provided suggestion signal) do not require a bump. See CLAUDE.md NFR-6.
public record SyncResponse(int SchemaVersion, DateTimeOffset ServerTime, List<ShoppingListResponse> Lists);

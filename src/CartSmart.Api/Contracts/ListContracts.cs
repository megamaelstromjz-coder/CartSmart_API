namespace CartSmart.Api.Contracts;

// ExpectedUpdatedAt is the UpdatedAt the client last saw for this list/item (from a prior PUT
// response or GET /sync). If present and it no longer matches the server's current value, the
// write is rejected with 409 before anything is persisted — this is the actual precondition
// check backing the conflict response (see ListEndpoints). If null, the write is unconditional
// (last-write-wins), which is what a client makes a brand-new local edit uses.
public record UpsertListRequest(string Name, DateTimeOffset? ExpectedUpdatedAt = null);

public record UpsertListItemRequest(
    string Name, decimal? Quantity, string? Unit, string? Category, bool IsChecked,
    DateTimeOffset? ExpectedUpdatedAt = null);

public record ShoppingListItemResponse(
    Guid Id, Guid ShoppingListId, string Name, decimal? Quantity, string? Unit, string? Category,
    bool IsChecked, DateTimeOffset UpdatedAt, DateTimeOffset CreatedAt, bool IsDeleted);

public record ShoppingListResponse(Guid Id, string Name, DateTimeOffset UpdatedAt, DateTimeOffset CreatedAt, bool IsDeleted);

// SchemaVersion is bumped only on breaking changes to this payload shape; additive fields
// (e.g. a future server-provided suggestion signal) do not require a bump. See CLAUDE.md NFR-6.
// Lists and items are flat, sibling arrays (not nested) so there's no ambiguity about whether a
// list carries its full item set or only its changed items: Items is always exactly the set of
// items that changed since `since`, correlated to its list via ShoppingListId. A list appears in
// Lists if the list itself changed, or if it owns a changed item (so the client always has the
// parent list in context even when only an item under it changed).
public record SyncResponse(
    int SchemaVersion, DateTimeOffset ServerTime, List<ShoppingListResponse> Lists, List<ShoppingListItemResponse> Items);

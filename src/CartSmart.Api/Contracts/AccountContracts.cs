namespace CartSmart.Api.Contracts;

// HasPassword is separate from LinkedProviders because the two are independent: a user can
// register with email/password and later link Google/Apple (or vice versa), so the client
// needs an explicit signal for whether "forgot password" is a valid option for this account.
public record AccountResponse(Guid Id, string Email, bool EmailVerified, DateTimeOffset CreatedAt, bool HasPassword, List<string> LinkedProviders);

public record AccountExportItem(string Name, decimal? Quantity, string? Unit, string? Category, bool IsChecked);
public record AccountExportList(string Name, DateTimeOffset CreatedAt, List<AccountExportItem> Items);
public record AccountExportDevice(string ClientDeviceId, string Platform, DateTimeOffset RegisteredAt);
public record AccountExportResponse(
    Guid Id, string Email, bool EmailVerified, DateTimeOffset CreatedAt, List<string> LinkedProviders,
    List<AccountExportDevice> Devices, List<AccountExportList> ShoppingLists);

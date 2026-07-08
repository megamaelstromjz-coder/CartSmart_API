namespace CartSmart.Api.Contracts;

public record AccountResponse(Guid Id, string Email, bool EmailVerified, DateTimeOffset CreatedAt, List<string> LinkedProviders);

public record AccountExportItem(string Name, decimal? Quantity, string? Unit, string? Category, bool IsChecked);
public record AccountExportList(string Name, DateTimeOffset CreatedAt, List<AccountExportItem> Items);
public record AccountExportDevice(string ClientDeviceId, string Platform, DateTimeOffset RegisteredAt);
public record AccountExportResponse(
    Guid Id, string Email, bool EmailVerified, DateTimeOffset CreatedAt, List<string> LinkedProviders,
    List<AccountExportDevice> Devices, List<AccountExportList> ShoppingLists);

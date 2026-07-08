namespace CartSmart.Api.Contracts;

public record RegisterDeviceRequest(string ClientDeviceId, string Platform, string? DisplayName);

public record DeviceResponse(Guid Id, string ClientDeviceId, string Platform, string? DisplayName, DateTimeOffset RegisteredAt, DateTimeOffset LastSeenAt, DateTimeOffset? LastSyncedAt);

namespace CartSmart.Api.Models;

public enum DevicePlatform
{
    Ios,
    Android,
}

public class Device
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    // Client-generated stable identifier for the physical device/install.
    public required string ClientDeviceId { get; set; }
    public required DevicePlatform Platform { get; set; }
    public string? DisplayName { get; set; }

    public DateTimeOffset RegisteredAt { get; set; }
    public DateTimeOffset LastSeenAt { get; set; }
    public DateTimeOffset? LastSyncedAt { get; set; }
}

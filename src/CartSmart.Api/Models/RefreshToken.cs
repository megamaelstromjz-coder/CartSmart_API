namespace CartSmart.Api.Models;

public class RefreshToken
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    // SHA-256 hash of the opaque token; the raw token is only ever returned to the client once.
    public required string TokenHash { get; set; }

    public Guid? DeviceId { get; set; }
    public Device? Device { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }

    // Rotation chain: set when this token was replaced by a newer one.
    public Guid? ReplacedByTokenId { get; set; }
}

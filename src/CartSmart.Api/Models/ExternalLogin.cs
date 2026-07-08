namespace CartSmart.Api.Models;

public enum AuthProvider
{
    Apple,
    Google,
}

public class ExternalLogin
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public required AuthProvider Provider { get; set; }

    // The provider's stable subject identifier (Apple `sub`, Google `sub`).
    public required string ProviderUserId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}

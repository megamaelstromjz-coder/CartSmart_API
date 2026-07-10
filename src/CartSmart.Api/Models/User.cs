namespace CartSmart.Api.Models;

public class User
{
    public Guid Id { get; set; }
    public required string Email { get; set; }
    public bool EmailVerified { get; set; }

    // Null when the account was created via an external provider only (Apple/Google).
    public string? PasswordHash { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public List<ExternalLogin> ExternalLogins { get; set; } = [];
    public List<RefreshToken> RefreshTokens { get; set; } = [];
    public List<PasswordResetToken> PasswordResetTokens { get; set; } = [];
    public List<Device> Devices { get; set; } = [];
    public List<ShoppingList> ShoppingLists { get; set; } = [];
}

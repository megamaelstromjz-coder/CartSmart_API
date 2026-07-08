namespace CartSmart.Api.Contracts;

public record RegisterRequest(string Email, string Password);
public record LoginRequest(string Email, string Password);
public record ExternalLoginRequest(string IdToken);
public record RefreshRequest(string RefreshToken);
public record LogoutRequest(string RefreshToken);

public record AuthResponse(Guid UserId, string Email, string AccessToken, DateTimeOffset AccessTokenExpiresAt, string RefreshToken, DateTimeOffset RefreshTokenExpiresAt);

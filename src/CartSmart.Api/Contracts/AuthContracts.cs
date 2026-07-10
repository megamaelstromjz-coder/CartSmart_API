namespace CartSmart.Api.Contracts;

public record RegisterRequest(string Email, string Password);
public record LoginRequest(string Email, string Password);
public record ExternalLoginRequest(string IdToken);
public record RefreshRequest(string RefreshToken);
public record LogoutRequest(string RefreshToken);
public record ForgotPasswordRequest(string Email);
public record ResetPasswordRequest(string ResetToken, string NewPassword);
public record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public record AuthResponse(Guid UserId, string Email, string AccessToken, DateTimeOffset AccessTokenExpiresAt, string RefreshToken, DateTimeOffset RefreshTokenExpiresAt);

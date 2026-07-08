using CartSmart.Api.Auth;
using CartSmart.Api.Contracts;
using CartSmart.Api.Data;
using CartSmart.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CartSmart.Api.Endpoints;

public static class AuthEndpoints
{
    public static RouteGroupBuilder MapAuthEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/register", Register);
        group.MapPost("/login", Login);
        group.MapPost("/google", LoginWithGoogle);
        group.MapPost("/apple", LoginWithApple);
        group.MapPost("/refresh", Refresh);
        group.MapPost("/logout", Logout);

        return group;
    }

    private static async Task<IResult> Register(
        RegisterRequest request,
        CartSmartDbContext db,
        IPasswordHasherService passwordHasher,
        ITokenService tokenService,
        IOptions<JwtOptions> jwtOptions,
        CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
        {
            return Results.Problem("A valid email is required.", statusCode: StatusCodes.Status400BadRequest);
        }
        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
        {
            return Results.Problem("Password must be at least 8 characters.", statusCode: StatusCodes.Status400BadRequest);
        }

        var exists = await db.Users.AnyAsync(u => u.Email == email, cancellationToken);
        if (exists)
        {
            return Results.Problem("An account with this email already exists.", statusCode: StatusCodes.Status409Conflict);
        }

        var now = DateTimeOffset.UtcNow;
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            PasswordHash = passwordHasher.HashPassword(request.Password),
            EmailVerified = false,
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);

        var response = await IssueTokensAsync(user, null, db, tokenService, jwtOptions.Value, cancellationToken);
        return Results.Created($"/api/v1/account", response);
    }

    private static async Task<IResult> Login(
        LoginRequest request,
        CartSmartDbContext db,
        IPasswordHasherService passwordHasher,
        ITokenService tokenService,
        IOptions<JwtOptions> jwtOptions,
        CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

        if (user is null || user.PasswordHash is null || !passwordHasher.VerifyPassword(user.PasswordHash, request.Password))
        {
            return Results.Problem("Invalid email or password.", statusCode: StatusCodes.Status401Unauthorized);
        }

        var response = await IssueTokensAsync(user, null, db, tokenService, jwtOptions.Value, cancellationToken);
        return Results.Ok(response);
    }

    private static async Task<IResult> LoginWithGoogle(
        ExternalLoginRequest request,
        CartSmartDbContext db,
        IGoogleIdTokenValidator validator,
        ITokenService tokenService,
        IOptions<JwtOptions> jwtOptions,
        CancellationToken cancellationToken)
    {
        var identity = await validator.ValidateAsync(request.IdToken, cancellationToken);
        if (identity is null)
        {
            return Results.Problem("Invalid Google identity token.", statusCode: StatusCodes.Status401Unauthorized);
        }

        var user = await ResolveExternalUserAsync(AuthProvider.Google, identity, db, cancellationToken);
        var response = await IssueTokensAsync(user, null, db, tokenService, jwtOptions.Value, cancellationToken);
        return Results.Ok(response);
    }

    private static async Task<IResult> LoginWithApple(
        ExternalLoginRequest request,
        CartSmartDbContext db,
        IAppleIdTokenValidator validator,
        ITokenService tokenService,
        IOptions<JwtOptions> jwtOptions,
        CancellationToken cancellationToken)
    {
        var identity = await validator.ValidateAsync(request.IdToken, cancellationToken);
        if (identity is null)
        {
            return Results.Problem("Invalid Apple identity token.", statusCode: StatusCodes.Status401Unauthorized);
        }

        var user = await ResolveExternalUserAsync(AuthProvider.Apple, identity, db, cancellationToken);
        var response = await IssueTokensAsync(user, null, db, tokenService, jwtOptions.Value, cancellationToken);
        return Results.Ok(response);
    }

    private static async Task<User> ResolveExternalUserAsync(
        AuthProvider provider,
        ExternalIdentity identity,
        CartSmartDbContext db,
        CancellationToken cancellationToken)
    {
        var existingLogin = await db.ExternalLogins
            .Include(l => l.User)
            .FirstOrDefaultAsync(l => l.Provider == provider && l.ProviderUserId == identity.ProviderUserId, cancellationToken);

        if (existingLogin is not null)
        {
            return existingLogin.User;
        }

        var now = DateTimeOffset.UtcNow;
        var email = identity.Email?.Trim().ToLowerInvariant();

        // Link to an existing email/password (or other-provider) account with the same verified email, if any.
        var user = email is not null
            ? await db.Users.FirstOrDefaultAsync(u => u.Email == email, cancellationToken)
            : null;

        if (user is null)
        {
            user = new User
            {
                Id = Guid.NewGuid(),
                Email = email ?? $"{provider.ToString().ToLowerInvariant()}-{identity.ProviderUserId}@no-email.cartsmart",
                EmailVerified = email is not null,
                CreatedAt = now,
                UpdatedAt = now,
            };
            db.Users.Add(user);
        }

        db.ExternalLogins.Add(new ExternalLogin
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            User = user,
            Provider = provider,
            ProviderUserId = identity.ProviderUserId,
            CreatedAt = now,
        });

        await db.SaveChangesAsync(cancellationToken);
        return user;
    }

    private static async Task<IResult> Refresh(
        RefreshRequest request,
        CartSmartDbContext db,
        ITokenService tokenService,
        IOptions<JwtOptions> jwtOptions,
        CancellationToken cancellationToken)
    {
        var tokenHash = tokenService.HashRefreshToken(request.RefreshToken);
        var existing = await db.RefreshTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

        if (existing is null || existing.RevokedAt is not null || existing.ExpiresAt < DateTimeOffset.UtcNow)
        {
            return Results.Problem("Invalid or expired refresh token.", statusCode: StatusCodes.Status401Unauthorized);
        }

        var response = await IssueTokensAsync(existing.User, existing.DeviceId, db, tokenService, jwtOptions.Value, cancellationToken, revoking: existing);
        return Results.Ok(response);
    }

    private static async Task<IResult> Logout(
        LogoutRequest request,
        CartSmartDbContext db,
        ITokenService tokenService,
        CancellationToken cancellationToken)
    {
        var tokenHash = tokenService.HashRefreshToken(request.RefreshToken);
        var existing = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);
        if (existing is not null && existing.RevokedAt is null)
        {
            existing.RevokedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }

        return Results.NoContent();
    }

    private static async Task<AuthResponse> IssueTokensAsync(
        User user,
        Guid? deviceId,
        CartSmartDbContext db,
        ITokenService tokenService,
        JwtOptions jwtOptions,
        CancellationToken cancellationToken,
        RefreshToken? revoking = null)
    {
        var accessToken = tokenService.GenerateAccessToken(user, out var accessExpiresAt);
        var (rawRefreshToken, refreshTokenHash) = tokenService.GenerateRefreshToken();
        var refreshExpiresAt = DateTimeOffset.UtcNow.AddDays(jwtOptions.RefreshTokenLifetimeDays);

        var newToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = refreshTokenHash,
            DeviceId = deviceId,
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = refreshExpiresAt,
        };
        db.RefreshTokens.Add(newToken);

        if (revoking is not null)
        {
            revoking.RevokedAt = DateTimeOffset.UtcNow;
            revoking.ReplacedByTokenId = newToken.Id;
        }

        await db.SaveChangesAsync(cancellationToken);

        return new AuthResponse(user.Id, user.Email, accessToken, accessExpiresAt, rawRefreshToken, refreshExpiresAt);
    }
}

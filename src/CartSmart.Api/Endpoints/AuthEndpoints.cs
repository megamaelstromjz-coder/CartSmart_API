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
        group.MapPost("/register", Register)
            .Produces<AuthResponse>(StatusCodes.Status201Created)
            .Produces<ApiError>(StatusCodes.Status400BadRequest)
            .Produces<ApiError>(StatusCodes.Status422UnprocessableEntity);

        group.MapPost("/login", Login)
            .Produces<AuthResponse>(StatusCodes.Status200OK)
            .Produces<ApiError>(StatusCodes.Status401Unauthorized);

        group.MapPost("/google", LoginWithGoogle)
            .Produces<AuthResponse>(StatusCodes.Status200OK)
            .Produces<ApiError>(StatusCodes.Status401Unauthorized);

        group.MapPost("/apple", LoginWithApple)
            .Produces<AuthResponse>(StatusCodes.Status200OK)
            .Produces<ApiError>(StatusCodes.Status401Unauthorized);

        group.MapPost("/refresh", Refresh)
            .Produces<AuthResponse>(StatusCodes.Status200OK)
            .Produces<ApiError>(StatusCodes.Status401Unauthorized);

        group.MapPost("/logout", Logout)
            .Produces(StatusCodes.Status204NoContent);

        group.MapPasswordEndpoints();

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
            return ApiResults.BadRequest("INVALID_EMAIL", "A valid email is required.");
        }
        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
        {
            return ApiResults.BadRequest("WEAK_PASSWORD", "Password must be at least 8 characters.");
        }

        var exists = await db.Users.AnyAsync(u => u.Email == email, cancellationToken);
        if (exists)
        {
            // 422, not 409: this is a semantic business-rule violation, not a version conflict
            // (409 is reserved for optimistic-concurrency conflicts on list/item writes).
            return ApiResults.UnprocessableEntity("EMAIL_ALREADY_REGISTERED", "An account with this email already exists.");
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
            return ApiResults.Unauthorized("INVALID_CREDENTIALS", "Email or password is incorrect.");
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
            return ApiResults.Unauthorized("INVALID_GOOGLE_TOKEN", "Invalid Google identity token.");
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
            return ApiResults.Unauthorized("INVALID_APPLE_TOKEN", "Invalid Apple identity token.");
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
            return ApiResults.Unauthorized("INVALID_REFRESH_TOKEN", "Invalid or expired refresh token.");
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

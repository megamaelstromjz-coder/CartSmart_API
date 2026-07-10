using CartSmart.Api.Auth;
using CartSmart.Api.Contracts;
using CartSmart.Api.Data;
using CartSmart.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace CartSmart.Api.Endpoints;

public static class PasswordEndpoints
{
    private const int ResetTokenLifetimeMinutes = 30;

    public static RouteGroupBuilder MapPasswordEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/password/forgot", ForgotPassword)
            .Produces(StatusCodes.Status200OK);

        group.MapPost("/password/reset", ResetPassword)
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ApiError>(StatusCodes.Status400BadRequest)
            .Produces<ApiError>(StatusCodes.Status401Unauthorized);

        group.MapPost("/password/change", ChangePassword)
            .RequireAuthorization()
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ApiError>(StatusCodes.Status400BadRequest)
            .Produces<ApiError>(StatusCodes.Status401Unauthorized);

        return group;
    }

    // Always returns 200 whether or not the email exists (or is external-auth-only) to avoid
    // account enumeration. Only email/password accounts (PasswordHash != null) get an email.
    private static async Task<IResult> ForgotPassword(
        ForgotPasswordRequest request,
        CartSmartDbContext db,
        ITokenService tokenService,
        IEmailSender emailSender,
        CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

        if (user is not null && user.PasswordHash is not null)
        {
            var (rawToken, tokenHash) = tokenService.GeneratePasswordResetToken();
            var now = DateTimeOffset.UtcNow;
            db.PasswordResetTokens.Add(new PasswordResetToken
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                TokenHash = tokenHash,
                CreatedAt = now,
                ExpiresAt = now.AddMinutes(ResetTokenLifetimeMinutes),
            });
            await db.SaveChangesAsync(cancellationToken);

            await emailSender.SendPasswordResetEmailAsync(user.Email, rawToken, cancellationToken);
        }

        return Results.Ok();
    }

    private static async Task<IResult> ResetPassword(
        ResetPasswordRequest request,
        CartSmartDbContext db,
        IPasswordHasherService passwordHasher,
        ITokenService tokenService,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 8)
        {
            return ApiResults.BadRequest("WEAK_PASSWORD", "Password must be at least 8 characters.");
        }

        var tokenHash = tokenService.HashPasswordResetToken(request.ResetToken);
        var now = DateTimeOffset.UtcNow;
        var resetToken = await db.PasswordResetTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash && t.UsedAt == null && t.ExpiresAt > now, cancellationToken);

        if (resetToken is null)
        {
            return ApiResults.Unauthorized("INVALID_RESET_TOKEN", "This reset link is invalid or has expired.");
        }

        resetToken.UsedAt = now;
        resetToken.User.PasswordHash = passwordHasher.HashPassword(request.NewPassword);
        resetToken.User.UpdatedAt = now;

        // Force re-authentication on every device after a password reset.
        var activeRefreshTokens = await db.RefreshTokens
            .Where(t => t.UserId == resetToken.UserId && t.RevokedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var refreshToken in activeRefreshTokens)
        {
            refreshToken.RevokedAt = now;
        }

        await db.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> ChangePassword(
        ChangePasswordRequest request,
        HttpContext httpContext,
        CartSmartDbContext db,
        IPasswordHasherService passwordHasher,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 8)
        {
            return ApiResults.BadRequest("WEAK_PASSWORD", "Password must be at least 8 characters.");
        }

        var userId = httpContext.User.GetUserId();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user is null || user.PasswordHash is null || !passwordHasher.VerifyPassword(user.PasswordHash, request.CurrentPassword))
        {
            return ApiResults.Unauthorized("INVALID_CREDENTIALS", "Current password is incorrect.");
        }

        var now = DateTimeOffset.UtcNow;
        user.PasswordHash = passwordHasher.HashPassword(request.NewPassword);
        user.UpdatedAt = now;

        // Force re-authentication on every device (including this one) after a password change.
        var activeRefreshTokens = await db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var refreshToken in activeRefreshTokens)
        {
            refreshToken.RevokedAt = now;
        }

        await db.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }
}

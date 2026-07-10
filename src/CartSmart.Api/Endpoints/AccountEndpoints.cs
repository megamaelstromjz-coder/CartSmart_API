using CartSmart.Api.Auth;
using CartSmart.Api.Contracts;
using CartSmart.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace CartSmart.Api.Endpoints;

public static class AccountEndpoints
{
    public static RouteGroupBuilder MapAccountEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/", GetAccount)
            .Produces<AccountResponse>(StatusCodes.Status200OK)
            .Produces<ApiError>(StatusCodes.Status404NotFound);

        group.MapGet("/export", ExportAccount)
            .Produces<AccountExportResponse>(StatusCodes.Status200OK)
            .Produces<ApiError>(StatusCodes.Status404NotFound);

        group.MapDelete("/", DeleteAccount)
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ApiError>(StatusCodes.Status404NotFound);

        return group;
    }

    private static async Task<IResult> GetAccount(HttpContext httpContext, CartSmartDbContext db, CancellationToken cancellationToken)
    {
        var userId = httpContext.User.GetUserId();
        var user = await db.Users
            .Include(u => u.ExternalLogins)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user is null)
        {
            return ApiResults.NotFound("ACCOUNT_NOT_FOUND", "Account not found.");
        }

        return Results.Ok(new AccountResponse(
            user.Id, user.Email, user.EmailVerified, user.CreatedAt, user.PasswordHash is not null,
            user.ExternalLogins.Select(l => l.Provider.ToString()).ToList()));
    }

    // GDPR/CCPA data export: profile + synced list content. Purchase history and prediction
    // model state are never included because they never leave the user's device (NFR-2).
    private static async Task<IResult> ExportAccount(HttpContext httpContext, CartSmartDbContext db, CancellationToken cancellationToken)
    {
        var userId = httpContext.User.GetUserId();
        var user = await db.Users
            .Include(u => u.ExternalLogins)
            .Include(u => u.Devices)
            .Include(u => u.ShoppingLists.Where(l => !l.IsDeleted))
                .ThenInclude(l => l.Items.Where(i => !i.IsDeleted))
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user is null)
        {
            return ApiResults.NotFound("ACCOUNT_NOT_FOUND", "Account not found.");
        }

        var response = new AccountExportResponse(
            user.Id, user.Email, user.EmailVerified, user.CreatedAt,
            user.ExternalLogins.Select(l => l.Provider.ToString()).ToList(),
            user.Devices.Select(d => new AccountExportDevice(d.ClientDeviceId, d.Platform.ToString(), d.RegisteredAt)).ToList(),
            user.ShoppingLists.Select(l => new AccountExportList(
                l.Name, l.CreatedAt,
                l.Items.Select(i => new AccountExportItem(i.Name, i.Quantity, i.Unit, i.Category, i.IsChecked)).ToList())).ToList());

        return Results.Ok(response);
    }

    private static async Task<IResult> DeleteAccount(HttpContext httpContext, CartSmartDbContext db, CancellationToken cancellationToken)
    {
        var userId = httpContext.User.GetUserId();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
        {
            return ApiResults.NotFound("ACCOUNT_NOT_FOUND", "Account not found.");
        }

        // Cascades to external logins, refresh tokens, devices, lists, and items (see DbContext config).
        db.Users.Remove(user);
        await db.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }
}

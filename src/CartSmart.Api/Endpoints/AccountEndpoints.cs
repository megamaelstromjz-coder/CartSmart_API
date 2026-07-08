using CartSmart.Api.Auth;
using CartSmart.Api.Contracts;
using CartSmart.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace CartSmart.Api.Endpoints;

public static class AccountEndpoints
{
    public static RouteGroupBuilder MapAccountEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/", GetAccount);
        group.MapGet("/export", ExportAccount);
        group.MapDelete("/", DeleteAccount);

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
            return Results.NotFound();
        }

        return Results.Ok(new AccountResponse(
            user.Id, user.Email, user.EmailVerified, user.CreatedAt,
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
            return Results.NotFound();
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
            return Results.NotFound();
        }

        // Cascades to external logins, refresh tokens, devices, lists, and items (see DbContext config).
        db.Users.Remove(user);
        await db.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }
}

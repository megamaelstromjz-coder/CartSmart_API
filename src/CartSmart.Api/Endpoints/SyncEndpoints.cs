using CartSmart.Api.Auth;
using CartSmart.Api.Contracts;
using CartSmart.Api.Data;
using CartSmart.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace CartSmart.Api.Endpoints;

public static class SyncEndpoints
{
    // Bump only when the payload shape changes in a way older clients can't safely ignore.
    private const int CurrentSchemaVersion = 1;

    public static RouteGroupBuilder MapSyncEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/", Pull);
        return group;
    }

    private static async Task<IResult> Pull(
        HttpContext httpContext,
        CartSmartDbContext db,
        DateTimeOffset? since,
        CancellationToken cancellationToken)
    {
        var userId = httpContext.User.GetUserId();
        var cursor = since ?? DateTimeOffset.MinValue;

        // Capture the server time before querying so a write that lands mid-request is
        // simply picked up again on the client's next pull rather than lost between cursors.
        var serverTime = DateTimeOffset.UtcNow;

        var changedListIds = await db.ShoppingLists
            .Where(l => l.UserId == userId && l.UpdatedAt > cursor)
            .Select(l => l.Id)
            .ToListAsync(cancellationToken);

        var changedItems = await db.ShoppingListItems
            .Where(i => i.ShoppingList.UserId == userId && i.UpdatedAt > cursor)
            .ToListAsync(cancellationToken);

        var relevantListIds = changedListIds
            .Concat(changedItems.Select(i => i.ShoppingListId))
            .Distinct()
            .ToList();

        var lists = await db.ShoppingLists
            .Where(l => relevantListIds.Contains(l.Id))
            .ToListAsync(cancellationToken);

        var itemsByList = changedItems.ToLookup(i => i.ShoppingListId);

        var response = new SyncResponse(
            CurrentSchemaVersion,
            serverTime,
            lists.Select(l => new ShoppingListResponse(
                l.Id, l.Name, l.UpdatedAt, l.CreatedAt, l.IsDeleted,
                itemsByList[l.Id].Select(i => new ShoppingListItemResponse(
                    i.Id, i.ShoppingListId, i.Name, i.Quantity, i.Unit, i.Category,
                    i.IsChecked, i.UpdatedAt, i.CreatedAt, i.IsDeleted)).ToList()))
                .ToList());

        return Results.Ok(response);
    }
}

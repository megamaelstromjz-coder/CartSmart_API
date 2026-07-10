using CartSmart.Api.Auth;
using CartSmart.Api.Contracts;
using CartSmart.Api.Data;
using CartSmart.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace CartSmart.Api.Endpoints;

public static class ListEndpoints
{
    public static RouteGroupBuilder MapListEndpoints(this RouteGroupBuilder group)
    {
        group.MapPut("/{listId:guid}", UpsertList)
            .Produces<ShoppingListResponse>(StatusCodes.Status200OK)
            .Produces<ApiError>(StatusCodes.Status400BadRequest)
            .Produces<ApiError>(StatusCodes.Status409Conflict);

        group.MapDelete("/{listId:guid}", DeleteList)
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ApiError>(StatusCodes.Status404NotFound);

        group.MapPut("/{listId:guid}/items/{itemId:guid}", UpsertItem)
            .Produces<ShoppingListItemResponse>(StatusCodes.Status200OK)
            .Produces<ApiError>(StatusCodes.Status400BadRequest)
            .Produces<ApiError>(StatusCodes.Status404NotFound)
            .Produces<ApiError>(StatusCodes.Status409Conflict);

        group.MapDelete("/{listId:guid}/items/{itemId:guid}", DeleteItem)
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ApiError>(StatusCodes.Status404NotFound);

        return group;
    }

    // Postgres `timestamptz` stores microsecond precision, one digit less than .NET's 100ns
    // ticks. Truncating here means the value we persist, return in the response, and later
    // re-read from the DB are bit-for-bit identical — otherwise a client echoing an UpdatedAt
    // back as ExpectedUpdatedAt on its next write would spuriously 409 on a perfectly valid
    // write, since it could never exactly match the truncated value EF Core reloads from Postgres.
    private static DateTimeOffset UtcNowForStorage() => new(DateTimeOffset.UtcNow.Ticks / 10 * 10, TimeSpan.Zero);

    // Upsert-by-client-generated-id: the client owns the id (a GUID minted offline), so a
    // create and an update are the same call and both work fine while offline.
    private static async Task<IResult> UpsertList(
        Guid listId,
        UpsertListRequest request,
        HttpContext httpContext,
        CartSmartDbContext db,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return ApiResults.BadRequest("VALIDATION_ERROR", "Name is required.");
        }

        var userId = httpContext.User.GetUserId();
        var list = await db.ShoppingLists.FirstOrDefaultAsync(l => l.Id == listId && l.UserId == userId, cancellationToken);

        // Real optimistic-concurrency precondition: if the client tells us what UpdatedAt it
        // last saw and the server's current value has moved on, reject before writing anything.
        // Omitting ExpectedUpdatedAt (e.g. a brand-new local edit with no prior server state
        // seen yet) skips this check and falls back to unconditional last-write-wins.
        if (list is not null && request.ExpectedUpdatedAt is not null && list.UpdatedAt != request.ExpectedUpdatedAt)
        {
            return ApiResults.Conflict("LIST_CONFLICT", "The list was modified by another device. Re-fetch and retry.");
        }

        var now = UtcNowForStorage();
        if (list is null)
        {
            list = new ShoppingList
            {
                Id = listId,
                UserId = userId,
                Name = request.Name,
                CreatedAt = now,
                UpdatedAt = now,
            };
            db.ShoppingLists.Add(list);
        }
        else
        {
            list.Name = request.Name;
            list.IsDeleted = false;
            list.UpdatedAt = now;
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Defense-in-depth for the narrow window between the read above and this write
            // landing concurrently with another request; the ExpectedUpdatedAt check above is
            // the real, client-driven conflict signal.
            return ApiResults.Conflict("LIST_CONFLICT", "The list was modified by another device. Re-fetch and retry.");
        }

        return Results.Ok(new ShoppingListResponse(list.Id, list.Name, list.UpdatedAt, list.CreatedAt, list.IsDeleted));
    }

    private static async Task<IResult> DeleteList(
        Guid listId,
        HttpContext httpContext,
        CartSmartDbContext db,
        CancellationToken cancellationToken)
    {
        var userId = httpContext.User.GetUserId();
        var list = await db.ShoppingLists
            .Include(l => l.Items)
            .FirstOrDefaultAsync(l => l.Id == listId && l.UserId == userId, cancellationToken);

        if (list is null)
        {
            return ApiResults.NotFound("LIST_NOT_FOUND", "List not found.");
        }

        var now = UtcNowForStorage();
        list.IsDeleted = true;
        list.UpdatedAt = now;
        foreach (var item in list.Items.Where(i => !i.IsDeleted))
        {
            item.IsDeleted = true;
            item.UpdatedAt = now;
        }

        await db.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> UpsertItem(
        Guid listId,
        Guid itemId,
        UpsertListItemRequest request,
        HttpContext httpContext,
        CartSmartDbContext db,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return ApiResults.BadRequest("VALIDATION_ERROR", "Name is required.");
        }

        var userId = httpContext.User.GetUserId();
        var list = await db.ShoppingLists.FirstOrDefaultAsync(l => l.Id == listId && l.UserId == userId, cancellationToken);
        if (list is null || list.IsDeleted)
        {
            return ApiResults.NotFound("LIST_NOT_FOUND", "List not found.");
        }

        var item = await db.ShoppingListItems.FirstOrDefaultAsync(i => i.Id == itemId && i.ShoppingListId == listId, cancellationToken);

        if (item is not null && request.ExpectedUpdatedAt is not null && item.UpdatedAt != request.ExpectedUpdatedAt)
        {
            return ApiResults.Conflict("ITEM_CONFLICT", "The item was modified by another device. Re-fetch and retry.");
        }

        var now = UtcNowForStorage();

        if (item is null)
        {
            item = new ShoppingListItem
            {
                Id = itemId,
                ShoppingListId = listId,
                Name = request.Name,
                Quantity = request.Quantity,
                Unit = request.Unit,
                Category = request.Category,
                IsChecked = request.IsChecked,
                CreatedAt = now,
                UpdatedAt = now,
            };
            db.ShoppingListItems.Add(item);
        }
        else
        {
            item.Name = request.Name;
            item.Quantity = request.Quantity;
            item.Unit = request.Unit;
            item.Category = request.Category;
            item.IsChecked = request.IsChecked;
            item.IsDeleted = false;
            item.UpdatedAt = now;
        }

        list.UpdatedAt = now;

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Defense-in-depth for the narrow window between the read above and this write
            // landing concurrently with another request; the ExpectedUpdatedAt check above is
            // the real, client-driven conflict signal.
            return ApiResults.Conflict("ITEM_CONFLICT", "The item was modified by another device. Re-fetch and retry.");
        }

        return Results.Ok(new ShoppingListItemResponse(
            item.Id, item.ShoppingListId, item.Name, item.Quantity, item.Unit, item.Category,
            item.IsChecked, item.UpdatedAt, item.CreatedAt, item.IsDeleted));
    }

    private static async Task<IResult> DeleteItem(
        Guid listId,
        Guid itemId,
        HttpContext httpContext,
        CartSmartDbContext db,
        CancellationToken cancellationToken)
    {
        var userId = httpContext.User.GetUserId();
        var item = await db.ShoppingListItems
            .FirstOrDefaultAsync(i => i.Id == itemId && i.ShoppingListId == listId && i.ShoppingList.UserId == userId, cancellationToken);

        if (item is null)
        {
            return ApiResults.NotFound("ITEM_NOT_FOUND", "Item not found.");
        }

        var now = UtcNowForStorage();
        item.IsDeleted = true;
        item.UpdatedAt = now;

        await db.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }
}

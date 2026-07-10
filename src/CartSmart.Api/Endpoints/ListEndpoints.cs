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

        var now = DateTimeOffset.UtcNow;
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
            return ApiResults.Conflict("LIST_CONFLICT", "The list was modified by another device. Re-fetch and retry.");
        }

        return Results.Ok(new ShoppingListResponse(list.Id, list.Name, list.UpdatedAt, list.CreatedAt, list.IsDeleted, []));
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

        var now = DateTimeOffset.UtcNow;
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
        var now = DateTimeOffset.UtcNow;

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

        var now = DateTimeOffset.UtcNow;
        item.IsDeleted = true;
        item.UpdatedAt = now;

        await db.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }
}

using CartSmart.Api.Contracts;
using CartSmart.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace CartSmart.Api.Endpoints;

public static class ReferenceEndpoints
{
    public static RouteGroupBuilder MapReferenceEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/version", GetVersion)
            .Produces<ReferenceVersionResponse>(StatusCodes.Status200OK)
            .Produces<ApiError>(StatusCodes.Status404NotFound);

        group.MapGet("/products", GetProducts)
            .Produces<ReferenceListResponse>(StatusCodes.Status200OK)
            .Produces<ApiError>(StatusCodes.Status404NotFound);

        return group;
    }

    private static async Task<IResult> GetVersion(CartSmartDbContext db, CancellationToken cancellationToken)
    {
        var version = await db.ReferenceListVersions.FirstOrDefaultAsync(cancellationToken);
        if (version is null)
        {
            return ApiResults.NotFound("REFERENCE_DATA_NOT_SEEDED", "Reference data has not been seeded yet.");
        }

        return Results.Ok(new ReferenceVersionResponse(version.Version, version.PublishedAt));
    }

    // Bundled reference data is small (a few thousand rows at most) so it's returned in full
    // rather than delta-synced — clients only need to re-fetch when /version changes.
    private static async Task<IResult> GetProducts(CartSmartDbContext db, CancellationToken cancellationToken)
    {
        var version = await db.ReferenceListVersions.FirstOrDefaultAsync(cancellationToken);
        if (version is null)
        {
            return ApiResults.NotFound("REFERENCE_DATA_NOT_SEEDED", "Reference data has not been seeded yet.");
        }

        var products = await db.ProductReferenceItems
            .Where(p => p.IsActive)
            .OrderBy(p => p.Category).ThenBy(p => p.Name)
            .Select(p => new ProductReferenceItemResponse(p.Name, p.Category))
            .ToListAsync(cancellationToken);

        return Results.Ok(new ReferenceListResponse(version.Version, version.PublishedAt, products));
    }
}

using CartSmart.Api.Auth;
using CartSmart.Api.Contracts;
using CartSmart.Api.Data;
using CartSmart.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace CartSmart.Api.Endpoints;

public static class DeviceEndpoints
{
    public static RouteGroupBuilder MapDeviceEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/", RegisterDevice)
            .Produces<DeviceResponse>(StatusCodes.Status200OK)
            .Produces<ApiError>(StatusCodes.Status400BadRequest);

        group.MapGet("/", ListDevices)
            .Produces<List<DeviceResponse>>(StatusCodes.Status200OK);

        group.MapDelete("/{deviceId:guid}", RemoveDevice)
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ApiError>(StatusCodes.Status404NotFound);

        return group;
    }

    private static async Task<IResult> RegisterDevice(
        RegisterDeviceRequest request,
        HttpContext httpContext,
        CartSmartDbContext db,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<DevicePlatform>(request.Platform, ignoreCase: true, out var platform))
        {
            return ApiResults.BadRequest("INVALID_PLATFORM", "Platform must be 'ios' or 'android'.");
        }

        var userId = httpContext.User.GetUserId();
        var now = DateTimeOffset.UtcNow;

        var device = await db.Devices.FirstOrDefaultAsync(
            d => d.UserId == userId && d.ClientDeviceId == request.ClientDeviceId, cancellationToken);

        if (device is null)
        {
            device = new Device
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                ClientDeviceId = request.ClientDeviceId,
                Platform = platform,
                DisplayName = request.DisplayName,
                RegisteredAt = now,
                LastSeenAt = now,
            };
            db.Devices.Add(device);
        }
        else
        {
            device.Platform = platform;
            device.DisplayName = request.DisplayName;
            device.LastSeenAt = now;
        }

        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(ToResponse(device));
    }

    private static async Task<IResult> ListDevices(
        HttpContext httpContext,
        CartSmartDbContext db,
        CancellationToken cancellationToken)
    {
        var userId = httpContext.User.GetUserId();
        var devices = await db.Devices
            .Where(d => d.UserId == userId)
            .OrderByDescending(d => d.LastSeenAt)
            .ToListAsync(cancellationToken);

        return Results.Ok(devices.Select(ToResponse));
    }

    private static async Task<IResult> RemoveDevice(
        Guid deviceId,
        HttpContext httpContext,
        CartSmartDbContext db,
        CancellationToken cancellationToken)
    {
        var userId = httpContext.User.GetUserId();
        var device = await db.Devices.FirstOrDefaultAsync(d => d.Id == deviceId && d.UserId == userId, cancellationToken);
        if (device is null)
        {
            return ApiResults.NotFound("DEVICE_NOT_FOUND", "Device not found.");
        }

        // Revoke any refresh tokens tied to this device so a lost/removed device can't keep syncing.
        var tokens = await db.RefreshTokens
            .Where(t => t.DeviceId == deviceId && t.RevokedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var token in tokens)
        {
            token.RevokedAt = DateTimeOffset.UtcNow;
        }

        db.Devices.Remove(device);
        await db.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }

    private static DeviceResponse ToResponse(Device device) => new(
        device.Id, device.ClientDeviceId, device.Platform.ToString(), device.DisplayName,
        device.RegisteredAt, device.LastSeenAt, device.LastSyncedAt);
}

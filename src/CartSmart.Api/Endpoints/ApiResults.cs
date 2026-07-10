using CartSmart.Api.Contracts;

namespace CartSmart.Api.Endpoints;

// Thin wrapper around Results.Json so every non-2xx response uses the same { code, message }
// shape (see ApiError) instead of ASP.NET Core's default ProblemDetails envelope.
public static class ApiResults
{
    public static IResult BadRequest(string code, string message) =>
        Results.Json(new ApiError(code, message), statusCode: StatusCodes.Status400BadRequest);

    public static IResult Unauthorized(string code, string message) =>
        Results.Json(new ApiError(code, message), statusCode: StatusCodes.Status401Unauthorized);

    public static IResult NotFound(string code, string message) =>
        Results.Json(new ApiError(code, message), statusCode: StatusCodes.Status404NotFound);

    public static IResult Conflict(string code, string message) =>
        Results.Json(new ApiError(code, message), statusCode: StatusCodes.Status409Conflict);

    public static IResult UnprocessableEntity(string code, string message) =>
        Results.Json(new ApiError(code, message), statusCode: StatusCodes.Status422UnprocessableEntity);
}

namespace CartSmart.Api.Contracts;

// Single error envelope reused by every endpoint so the client can build one error-handling
// path instead of per-endpoint parsing. Code is a stable machine-readable string; Message is
// human-readable and safe to surface in UI.
public record ApiError(string Code, string Message);

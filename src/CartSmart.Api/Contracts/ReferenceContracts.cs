namespace CartSmart.Api.Contracts;

public record ReferenceVersionResponse(int Version, DateTimeOffset PublishedAt);

public record ProductReferenceItemResponse(string Name, string Category);

public record ReferenceListResponse(int Version, DateTimeOffset PublishedAt, List<ProductReferenceItemResponse> Products);

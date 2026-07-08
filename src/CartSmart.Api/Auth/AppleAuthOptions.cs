namespace CartSmart.Api.Auth;

public class AppleAuthOptions
{
    public const string SectionName = "Auth:Apple";

    // The app's bundle identifier(s) — validated against the identity token's audience.
    public string[] AllowedAudiences { get; set; } = [];
}

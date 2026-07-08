namespace CartSmart.Api.Auth;

public class GoogleAuthOptions
{
    public const string SectionName = "Auth:Google";

    // OAuth client IDs the id_token audience is validated against (iOS + Android client IDs).
    public string[] AllowedAudiences { get; set; } = [];
}

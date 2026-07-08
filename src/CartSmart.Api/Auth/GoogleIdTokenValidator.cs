using Google.Apis.Auth;
using Microsoft.Extensions.Options;

namespace CartSmart.Api.Auth;

public record ExternalIdentity(string ProviderUserId, string? Email);

public interface IGoogleIdTokenValidator
{
    Task<ExternalIdentity?> ValidateAsync(string idToken, CancellationToken cancellationToken);
}

public class GoogleIdTokenValidator(IOptions<GoogleAuthOptions> options) : IGoogleIdTokenValidator
{
    private readonly GoogleAuthOptions _options = options.Value;

    public async Task<ExternalIdentity?> ValidateAsync(string idToken, CancellationToken cancellationToken)
    {
        try
        {
            var settings = new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = _options.AllowedAudiences,
            };
            var payload = await GoogleJsonWebSignature.ValidateAsync(idToken, settings);
            return new ExternalIdentity(payload.Subject, payload.Email);
        }
        catch (InvalidJwtException)
        {
            return null;
        }
    }
}

using System.IdentityModel.Tokens.Jwt;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace CartSmart.Api.Auth;

public interface IAppleIdTokenValidator
{
    Task<ExternalIdentity?> ValidateAsync(string idToken, CancellationToken cancellationToken);
}

// Verifies a Sign in with Apple identity token against Apple's published JWKS.
// https://developer.apple.com/documentation/sign_in_with_apple/verifying_a_user
public class AppleIdTokenValidator(
    IHttpClientFactory httpClientFactory,
    IMemoryCache cache,
    IOptions<AppleAuthOptions> options) : IAppleIdTokenValidator
{
    private const string JwksUrl = "https://appleid.apple.com/auth/keys";
    private const string JwksCacheKey = "apple-jwks";
    private const string Issuer = "https://appleid.apple.com";

    private readonly AppleAuthOptions _options = options.Value;

    public async Task<ExternalIdentity?> ValidateAsync(string idToken, CancellationToken cancellationToken)
    {
        var signingKeys = await GetSigningKeysAsync(cancellationToken);

        var handler = new JwtSecurityTokenHandler();
        var validationParameters = new TokenValidationParameters
        {
            ValidIssuer = Issuer,
            ValidAudiences = _options.AllowedAudiences,
            IssuerSigningKeys = signingKeys,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
        };

        try
        {
            var principal = handler.ValidateToken(idToken, validationParameters, out _);
            var subject = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            if (string.IsNullOrEmpty(subject))
            {
                return null;
            }

            var email = principal.FindFirst(JwtRegisteredClaimNames.Email)?.Value;
            return new ExternalIdentity(subject, email);
        }
        catch (SecurityTokenException)
        {
            return null;
        }
    }

    private async Task<IReadOnlyCollection<SecurityKey>> GetSigningKeysAsync(CancellationToken cancellationToken)
    {
        if (cache.TryGetValue<IReadOnlyCollection<SecurityKey>>(JwksCacheKey, out var cached) && cached is not null)
        {
            return cached;
        }

        var client = httpClientFactory.CreateClient(nameof(AppleIdTokenValidator));
        var jwksJson = await client.GetStringAsync(JwksUrl, cancellationToken);
        var jwks = new JsonWebKeySet(jwksJson);
        var keys = jwks.Keys.Cast<SecurityKey>().ToList();

        cache.Set(JwksCacheKey, (IReadOnlyCollection<SecurityKey>)keys, TimeSpan.FromHours(24));
        return keys;
    }
}

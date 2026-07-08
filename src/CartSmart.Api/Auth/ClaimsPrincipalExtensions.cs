using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace CartSmart.Api.Auth;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal principal)
    {
        var sub = principal.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? throw new InvalidOperationException("Principal is missing a sub claim.");
        return Guid.Parse(sub);
    }
}

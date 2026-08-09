using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace BuildingBlocks.Common
{
    public static class ClaimsPrincipalExtensions
    {
        public static Guid GetUserId(this ClaimsPrincipal user)
        {
            if (user?.Identity?.IsAuthenticated != true)
            {
                return Guid.Empty;
            }

            var claimValue = user.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? user.FindFirstValue(JwtRegisteredClaimNames.Sub)
                ?? user.FindFirstValue("sub");

            return Guid.TryParse(claimValue, out var id) ? id : Guid.Empty;
        }
    }
}

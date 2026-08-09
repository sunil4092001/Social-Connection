using System.Security.Claims;
using BuildingBlocks.Common;

namespace LinkedIn.Tests;

public sealed class ClaimsPrincipalExtensionsTests
{
    [Fact]
    public void GetUserId_returns_empty_for_unauthenticated_user()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity());

        var userId = principal.GetUserId();

        Assert.Equal(Guid.Empty, userId);
    }

    [Fact]
    public void GetUserId_reads_nameidentifier_claim_when_present()
    {
        var expected = Guid.NewGuid();
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, expected.ToString())
        }, "TestAuth");

        var principal = new ClaimsPrincipal(identity);

        var userId = principal.GetUserId();

        Assert.Equal(expected, userId);
    }
}

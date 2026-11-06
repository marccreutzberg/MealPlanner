using System.Globalization;
using System.Security.Claims;
using MealPlanner.Api.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace MealPlanner.Api.Common.Auth;

/// <summary>Every signed-in principal (cookie or API key) carries its household, so handlers can scope data.</summary>
public static class HouseholdClaims
{
    public const string HouseholdId = "household_id";

    public static int GetHouseholdId(this ClaimsPrincipal principal) =>
        int.Parse(principal.FindFirstValue(HouseholdId)
            ?? throw new InvalidOperationException("Principal has no household claim"), CultureInfo.InvariantCulture);

    public static string GetUserId(this ClaimsPrincipal principal) =>
        principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("Principal has no user id");
}

/// <summary>Adds the household claim to the cookie principal at sign-in.</summary>
public sealed class AppClaimsPrincipalFactory(UserManager<User> userManager, IOptions<IdentityOptions> options)
    : UserClaimsPrincipalFactory<User>(userManager, options)
{
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(User user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        identity.AddClaim(new Claim(HouseholdClaims.HouseholdId, user.HouseholdId.ToString(CultureInfo.InvariantCulture)));
        return identity;
    }
}

using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using MealPlanner.Api.Common.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MealPlanner.Api.Common.Auth;

/// <summary>
/// Authenticates the Claude connector by the X-Api-Key header (spec 9). Keys are stored only as SHA-256 hashes.
/// </summary>
public sealed class ApiKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder, AppDbContext db)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "ApiKey";
    public const string HeaderName = "X-Api-Key";
    public const string ApiKeyIdClaim = "api_key_id";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(HeaderName, out var values) || string.IsNullOrWhiteSpace(values.ToString()))
            return AuthenticateResult.NoResult();

        var hash = ApiKeyGenerator.Hash(values.ToString().Trim());
        var key = await db.ApiKeys
            .Where(k => k.KeyHash == hash && k.RevokedAt == null)
            .Select(k => new { k.Id, k.UserId, k.HouseholdId, k.User.Email, k.User.DisplayName })
            .SingleOrDefaultAsync(Context.RequestAborted);
        if (key is null)
            return AuthenticateResult.Fail("Invalid or revoked API key");

        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, key.UserId),
            new Claim(ClaimTypes.Name, key.Email ?? key.DisplayName),
            new Claim(HouseholdClaims.HouseholdId, key.HouseholdId.ToString(CultureInfo.InvariantCulture)),
            new Claim(ApiKeyIdClaim, key.Id.ToString(CultureInfo.InvariantCulture)),
        ], SchemeName);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName));
    }
}

public static class ApiKeyGenerator
{
    private const string Prefix = "mp_";

    /// <summary>A new random key. Shown to the user once; only its hash is stored.</summary>
    public static string Generate() => Prefix + Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

    public static string Hash(string key) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)));

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}

/// <summary>Authorization policies. The default policy accepts both the cookie and an API key.</summary>
public static class Policies
{
    /// <summary>Browser sessions only: Hangfire dashboard and API key management.</summary>
    public const string Cookie = "Cookie";

    /// <summary>The Claude connector only: /mcp.</summary>
    public const string ApiKey = "ApiKey";
}

using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using ProGlass.Saas.Application.Auth;

namespace ProGlass.Saas.Infrastructure.Security;

/// <summary>JWT settings (bound from Authentication:Jwt in appsettings).</summary>
public sealed class JwtSettings
{
    public string Issuer { get; set; } = "proglass-saas";
    public string Audience { get; set; } = "proglass-saas-api";
    public string SigningKey { get; set; } = "";

    /// <summary>Validated lazily — throws with an actionable message when
    /// the key is missing or too short for HS256.</summary>
    public SymmetricSecurityKey CreateSigningKey()
    {
        if (string.IsNullOrEmpty(SigningKey) || Encoding.UTF8.GetByteCount(SigningKey) < 32)
            throw new InvalidOperationException(
                "Authentication:Jwt:SigningKey is missing or shorter than 32 bytes. " +
                "Generate one with: openssl rand -base64 48 — and provide it via " +
                "environment/secret store, never in the repository (§43).");
        return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey));
    }
}

/// <summary>
/// Issues the short-lived HS256 access tokens. Claims read by the API:
/// sub (user id), tid (tenant id), role, preferred_username, display_name.
/// The refresh token (long-lived, rotating) is a separate opaque token —
/// see AuthService/RefreshTokens.
/// </summary>
public sealed class JwtTokenService : ITokenService
{
    // Claim names spelled out (not JwtRegisteredClaimNames — that class
    // lives in the System.IdentityModel.Tokens.Jwt package, which this
    // project deliberately does not reference; JsonWebTokens 7.x suffices).
    private const string ClaimSub = "sub";
    private const string ClaimJti = "jti";
    private const string ClaimPreferredUsername = "preferred_username";

    private readonly JwtSettings _settings;
    private readonly AuthOptions _authOptions;
    private readonly SymmetricSecurityKey _signingKey;

    public JwtTokenService(JwtSettings settings, AuthOptions authOptions)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _authOptions = authOptions ?? throw new ArgumentNullException(nameof(authOptions));
        _signingKey = settings.CreateSigningKey();
    }

    public (string Token, DateTime ExpiresAtUtc) IssueAccessToken(LoginUser user)
    {
        ArgumentNullException.ThrowIfNull(user);

        var issuedAt = DateTime.UtcNow;
        var expiresAt = issuedAt.AddMinutes(_authOptions.AccessTokenMinutes);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _settings.Issuer,
            Audience = _settings.Audience,
            IssuedAt = issuedAt,
            NotBefore = issuedAt,
            Expires = expiresAt,
            SigningCredentials = new SigningCredentials(_signingKey, SecurityAlgorithms.HmacSha256),
            Claims = new Dictionary<string, object>
            {
                [ClaimSub] = user.UserId.ToString(),
                [ClaimJti] = Guid.NewGuid().ToString("N"),
                ["tid"] = user.TenantId.ToString(),
                ["role"] = user.Role,
                [ClaimPreferredUsername] = user.Username,
                ["display_name"] = user.DisplayName
            }
        };

        var token = new JsonWebTokenHandler().CreateToken(descriptor);
        return (token, expiresAt);
    }
}

using System.Text.RegularExpressions;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using ProGlass.Saas.Application.Auth;
using ProGlass.Saas.Infrastructure.Security;
using Xunit;

namespace ProGlass.Saas.IntegrationTests;

/// <summary>
/// Argon2idPasswordHasher + JwtTokenService contract tests. Hosted in the
/// integration project because it references Infrastructure; these need
/// NO database — they run everywhere (plain [Fact]).
/// </summary>
public sealed class Argon2idPasswordHasherTests
{
    // (Konscious m=64MiB,t=3,p=2 ≈ 100ms per hash — 8 hashes here.)
    private readonly Argon2idPasswordHasher _hasher = new();

    [Fact]
    public void Hash_produces_the_documented_self_describing_format()
    {
        var hash = _hasher.Hash("Str0ng-Pass!");
        Assert.Matches(
            new Regex(@"^\$argon2id\$v=19\$m=\d+,t=\d+,p=\d+\$[A-Za-z0-9+/=]+\$[A-Za-z0-9+/=]+$"),
            hash);
    }

    [Fact]
    public void Hash_salts_every_call()
    {
        Assert.NotEqual(_hasher.Hash("Str0ng-Pass!"), _hasher.Hash("Str0ng-Pass!"));
    }

    [Fact]
    public void Verify_accepts_the_right_password()
    {
        var hash = _hasher.Hash("Str0ng-Pass!");
        Assert.True(_hasher.Verify(hash, "Str0ng-Pass!"));
    }

    [Theory]
    [InlineData("Str0ng-Pass")]
    [InlineData("str0ng-pass!")]
    [InlineData("")]
    public void Verify_rejects_wrong_passwords(string candidate)
    {
        var hash = _hasher.Hash("Str0ng-Pass!");
        Assert.False(_hasher.Verify(hash, candidate));
    }

    [Fact]
    public void Verify_rejects_malformed_stored_hashes()
    {
        Assert.False(_hasher.Verify("not-a-hash", "Str0ng-Pass!"));
        Assert.False(_hasher.Verify("", "Str0ng-Pass!"));
        Assert.False(_hasher.Verify(null!, "Str0ng-Pass!"));
        Assert.False(_hasher.Verify("$argon2id$v=19$m=65536,t=3,p=2$###$???", "Str0ng-Pass!"));
    }

    [Fact]
    public void Verify_honors_the_parameters_of_a_legacy_hash()
    {
        // Self-describing format: an older/weaker hash must still verify
        // with ITS parameters, so strengthening defaults later never
        // invalidates existing credentials.
        var hash = _hasher.Hash("Str0ng-Pass!");
        var parts = hash.Split('$', StringSplitOptions.RemoveEmptyEntries);
        parts[2] = "m=8192,t=1,p=1"; // weakened parameters
        var salt = parts[3];
        // Recompute the digest with the weakened parameters.
        using var argon2 = new Konscious.Security.Cryptography.Argon2id(
            System.Text.Encoding.UTF8.GetBytes("Str0ng-Pass!"))
        {
            Salt = Convert.FromBase64String(salt),
            MemorySize = 8192,
            Iterations = 1,
            DegreeOfParallelism = 1
        };
        var digest = Convert.ToBase64String(argon2.GetBytes(32));
        var weakened = "$" + string.Join("$", parts[0], parts[1], parts[2], salt, digest);

        Assert.True(_hasher.Verify(weakened, "Str0ng-Pass!"));
        Assert.False(_hasher.Verify(weakened, "Wrong-1!"));
    }
}

public sealed class JwtTokenServiceTests
{
    private static JwtTokenService Service() => new(
        AuthFlowTests.TestJwtSettings, new AuthOptions());

    private static LoginUser User() => new(
        Guid.NewGuid(), Guid.NewGuid(), "juber", "Juber Saleem",
        "juber@example.com", "Manager", "hash", true, false, 0, null, null);

    [Fact]
    public void Access_token_carries_the_tenancy_claims_the_pipeline_reads()
    {
        var user = User();
        var (token, expiresAt) = Service().IssueAccessToken(user);

        var result = new JsonWebTokenHandler().ValidateToken(
            token,
            new TokenValidationParameters
            {
                ValidIssuer = AuthFlowTests.TestJwtSettings.Issuer,
                ValidAudience = AuthFlowTests.TestJwtSettings.Audience,
                IssuerSigningKey = AuthFlowTests.TestJwtSettings.CreateSigningKey(),
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            });
        Assert.True(result.IsValid, result.Exception?.Message);
        var claims = result.ClaimsIdentity.Claims;

        Assert.Equal(user.UserId.ToString(),
            claims.Single(c => c.Type == "sub").Value);
        Assert.Equal(user.TenantId.ToString(),
            claims.Single(c => c.Type == "tid").Value);
        Assert.Equal(user.Role,
            claims.Single(c => c.Type == "role").Value);
        Assert.Equal(user.Username,
            claims.Single(c => c.Type == "preferred_username").Value);
        Assert.True(expiresAt <= DateTime.UtcNow.AddMinutes(15).AddSeconds(5));
    }

    [Fact]
    public void Tokens_with_a_tampered_signature_do_not_validate()
    {
        var token = Service().IssueAccessToken(User()).Token;
        var tampered = token[..^8] + "AAAAAAAA";

        var result = new JsonWebTokenHandler().ValidateToken(
            tampered,
            new TokenValidationParameters
            {
                ValidIssuer = AuthFlowTests.TestJwtSettings.Issuer,
                ValidAudience = AuthFlowTests.TestJwtSettings.Audience,
                IssuerSigningKey = AuthFlowTests.TestJwtSettings.CreateSigningKey(),
                ValidateLifetime = true
            });
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Rejects_signing_keys_shorter_than_32_bytes()
    {
        var settings = new JwtSettings { SigningKey = "too-short" };
        Assert.Throws<InvalidOperationException>(() =>
            new JwtTokenService(settings, new AuthOptions()));
    }
}

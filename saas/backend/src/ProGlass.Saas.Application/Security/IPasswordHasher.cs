namespace ProGlass.Saas.Application.Security;

/// <summary>
/// Password hashing port. Production implementation: Argon2id
/// (ADR-006 — the WPF SHA-256(salt+password) scheme is deliberately NOT
/// portable to the SaaS; migrated users get must_reset_password=true).
/// Hash strings are self-describing (algorithm + parameters + salt + digest)
/// so parameters can be strengthened later without invalidating old hashes.
/// </summary>
public interface IPasswordHasher
{
    /// <summary>Hashes a new password (used at registration/change).</summary>
    string Hash(string password);

    /// <summary>Constant-time verification of a candidate password.</summary>
    bool Verify(string storedHash, string password);
}

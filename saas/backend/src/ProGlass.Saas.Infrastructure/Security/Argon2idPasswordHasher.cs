using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;
using ProGlass.Saas.Application.Security;

namespace ProGlass.Saas.Infrastructure.Security;

/// <summary>
/// Argon2id password hashing (ADR-006). Stored format is PHC-style and
/// self-describing so parameters can be strengthened later without
/// invalidating existing hashes:
///
///   $argon2id$v=19$m=65536,t=3,p=2$&lt;salt b64&gt;$&lt;digest b64&gt;
///
/// Parameters follow the OWASP Password Storage Cheat Sheet recommendation
/// for Argon2id (m=64 MiB, t=3, p=2 as a floor; p is capped by typical
/// container CPU counts). Verification parses the stored parameters — old
/// hashes verify with the parameters they were created with.
///
/// The WPF app stored SHA-256(salt+password) hex; that scheme is not
/// portable (documented ADR-006) — migrated users get must_reset_password.
/// </summary>
public sealed class Argon2idPasswordHasher : IPasswordHasher
{
    private const int SaltBytes = 16;
    private const int HashBytes = 32;
    private const int MemoryKib = 65536;
    private const int Iterations = 3;
    private const int Parallelism = 2;

    public string Hash(string password)
    {
        ArgumentNullException.ThrowIfNull(password);
        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var digest = Compute(Encoding.UTF8.GetBytes(password), salt,
            MemoryKib, Iterations, Parallelism, HashBytes);
        return $"$argon2id$v=19$m={MemoryKib},t={Iterations},p={Parallelism}" +
               $"${Convert.ToBase64String(salt)}${Convert.ToBase64String(digest)}";
    }

    public bool Verify(string storedHash, string password)
    {
        if (string.IsNullOrEmpty(storedHash) || string.IsNullOrEmpty(password))
            return false;
        if (!TryParse(storedHash, out var salt, out var expected,
                out var memory, out var iterations, out var parallelism))
            return false;

        var actual = Compute(Encoding.UTF8.GetBytes(password), salt,
            memory, iterations, parallelism, expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    private static byte[] Compute(byte[] password, byte[] salt,
        int memoryKib, int iterations, int parallelism, int hashBytes)
    {
        using var argon2 = new Argon2id(password)
        {
            Salt = salt,
            MemorySize = memoryKib,
            Iterations = iterations,
            DegreeOfParallelism = parallelism
        };
        return argon2.GetBytes(hashBytes);
    }

    private static bool TryParse(string stored, out byte[] salt, out byte[] digest,
        out int memory, out int iterations, out int parallelism)
    {
        salt = Array.Empty<byte>();
        digest = Array.Empty<byte>();
        memory = iterations = parallelism = 0;

        // $argon2id$v=19$m=65536,t=3,p=2$b64salt$b64digest
        var parts = stored.Split('$', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 5 || parts[0] != "argon2id" || parts[1] != "v=19")
            return false;

        var parameters = parts[2].Split(',');
        foreach (var parameter in parameters)
        {
            var kv = parameter.Split('=');
            if (kv.Length != 2) continue;
            switch (kv[0])
            {
                case "m": memory = int.Parse(kv[1]); break;
                case "t": iterations = int.Parse(kv[1]); break;
                case "p": parallelism = int.Parse(kv[1]); break;
            }
        }
        if (memory <= 0 || iterations <= 0 || parallelism <= 0)
            return false;

        try
        {
            salt = Convert.FromBase64String(parts[3]);
            digest = Convert.FromBase64String(parts[4]);
        }
        catch (FormatException)
        {
            return false;
        }
        return salt.Length > 0 && digest.Length > 0;
    }
}

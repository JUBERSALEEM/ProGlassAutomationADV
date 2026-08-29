using System.Threading;

namespace ProGlassAutomation.Services.Licensing;

/// <summary>
/// Legacy compatibility holder for the removed client-side trial private key.
///
/// The offline max-security redesign no longer ships a recoverable signing key
/// for self-issued trials. This class remains only so existing test/source
/// references continue to compile; in production it resolves to a placeholder.
/// </summary>
internal static class TrialKeyVault
{
    private static string? _cachedXml;
    private static readonly object _cacheLock = new();

    public static string GetPrivateKeyXml()
    {
        var cached = Volatile.Read(ref _cachedXml);
        if (cached != null) return cached;

        lock (_cacheLock)
        {
            _cachedXml ??= "PLACEHOLDER_TRIAL_PRIVATE_KEY_REMOVED";
            return _cachedXml;
        }
    }

    internal static void ResetCacheForTests()
    {
        lock (_cacheLock)
        {
            _cachedXml = null;
        }
    }
}

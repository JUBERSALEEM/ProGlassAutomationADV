// ═══════════════════════════════════════════════════════════════════════
// PORTED from the WPF application: Services/Security/PasswordPolicy.cs
// (read 2026-09-27). The WPF file is the source of truth; every rule and
// rejection message is preserved. Only the namespace changed.
//
// POLICY (deliberate, NIST SP 800-63B aligned — unchanged from WPF):
//   • Minimum 6 characters, no maximum, no forced composition rules.
//   • REJECTED outright: shorter than 6 / blank; common-password blocklist
//     (incl. trailing-digit variants); one repeated character; one
//     unbroken ascending/descending run; equal to / containing the
//     account's own username or display name.
//   • Everything else ACCEPTED with a 0–4 strength score + guidance.
//
// This governs only passwords being CHOSEN now. Verify() never consults
// it, so no existing credential stops working (WPF backward-compat rule).
// NEVER THROWS.
// ═══════════════════════════════════════════════════════════════════════

namespace ProGlass.Saas.Application.Security;

/// <summary>
/// Result of evaluating a candidate password: whether it is accepted,
/// how strong it is, and what would make it stronger.
/// </summary>
public readonly struct PasswordEvaluation
{
    /// <summary>False only for known-bad passwords (see class docs).</summary>
    public bool Accepted { get; }

    /// <summary>Why it was rejected; empty when accepted.</summary>
    public string RejectionReason { get; }

    /// <summary>0–4 strength score for meter bars.</summary>
    public int Score { get; }

    /// <summary>"Too short" / "Weak" / "Fair" / "Good" / "Strong".</summary>
    public string StrengthLabel { get; }

    /// <summary>One actionable improvement; empty when strong or rejected.</summary>
    public string Guidance { get; }

    public PasswordEvaluation(
        bool accepted, string rejectionReason,
        int score, string strengthLabel, string guidance)
    {
        Accepted = accepted;
        RejectionReason = rejectionReason ?? "";
        Score = Math.Clamp(score, 0, 4);
        StrengthLabel = strengthLabel ?? "";
        Guidance = guidance ?? "";
    }
}

/// <summary>
/// Single home for password acceptance rules and strength scoring —
/// WPF PasswordPolicy, verbatim.
/// </summary>
public static class PasswordPolicy
{
    /// <summary>Minimum length for a NEW or CHANGED password.</summary>
    public const int MinLength = 6;

    // NOTE: there is deliberately NO MaxLength. See the WPF class docs.

    /// <summary>Short hint for password screens. Names the same number as
    /// <see cref="MinLength"/> — keep them equal.</summary>
    public static string HintText =>
        $"At least {MinLength} characters — no maximum. " +
        "Longer is stronger; common passwords are rejected.";

    /// <summary>
    /// Service-layer validation: accepted + the error to show when not.
    /// </summary>
    public static (bool Accepted, string Error) ValidateNewPassword(
        string? password, string? username = null, string? displayName = null)
    {
        var eval = Evaluate(password, username, displayName);
        return eval.Accepted ? (true, "") : (false, eval.RejectionReason);
    }

    /// <summary>
    /// Full evaluation: acceptance + score + label + guidance. Never
    /// throws; null reads as empty.
    /// </summary>
    public static PasswordEvaluation Evaluate(
        string? password, string? username = null, string? displayName = null)
    {
        try
        {
            string pw = password ?? "";

            if (pw.Length < MinLength)
                return new PasswordEvaluation(
                    false,
                    $"Password must be at least {MinLength} characters.",
                    0, "Too short", "");

            if (string.IsNullOrWhiteSpace(pw))
                return new PasswordEvaluation(
                    false, "Password cannot be blank.",
                    0, "Too short", "");

            if (IsUniform(pw))
                return new PasswordEvaluation(
                    false, "Password cannot be one character repeated.",
                    0, "Weak", "");

            if (IsSingleRun(pw))
                return new PasswordEvaluation(
                    false, "Password cannot be a simple sequence like \"abcdef\".",
                    0, "Weak", "");

            string? blockedAs = BlockedWord(pw);
            if (blockedAs != null)
                return new PasswordEvaluation(
                    false,
                    $"\"{blockedAs}\" is too common — choose something less guessable.",
                    0, "Weak", "");

            string? identityHit = MatchesIdentity(pw, username, displayName);
            if (identityHit != null)
                return new PasswordEvaluation(
                    false, identityHit,
                    0, "Weak", "");

            // ── Accepted: score length + variety ────────────────────────
            int score = 0;
            if (pw.Length >= 8) score++;
            if (pw.Length >= 12) score++;
            int classes = CharacterClasses(pw);
            if (classes >= 2) score++;
            if (classes >= 3 || pw.Length >= 16) score++;
            score = Math.Clamp(score, 1, 4);

            string label = score switch
            {
                1 => "Weak",
                2 => "Fair",
                3 => "Good",
                _ => "Strong"
            };

            return new PasswordEvaluation(
                true, "", score, label, GuidanceFor(pw, classes));
        }
        catch
        {
            // Guidance must never crash a password screen (WPF rule).
            return new PasswordEvaluation(true, "", 1, "Weak", "");
        }
    }

    private static string GuidanceFor(string pw, int classes)
    {
        if (pw.Length < 10)
            return "Add more characters — length beats complexity.";
        if (classes < 2)
            return "Mix upper/lower-case, digits or symbols.";
        if (pw.Length < 14)
            return "Longer still resists guessing — there is no maximum.";
        if (classes < 3)
            return "A third character type would make it stronger.";
        return "";
    }

    private static int CharacterClasses(string pw)
    {
        bool lower = false, upper = false, digit = false, symbol = false;
        foreach (char c in pw)
        {
            if (char.IsLower(c)) lower = true;
            else if (char.IsUpper(c)) upper = true;
            else if (char.IsDigit(c)) digit = true;
            else symbol = true;
        }
        return (lower ? 1 : 0) + (upper ? 1 : 0)
             + (digit ? 1 : 0) + (symbol ? 1 : 0);
    }

    private static bool IsUniform(string pw)
    {
        for (int i = 1; i < pw.Length; i++)
            if (pw[i] != pw[0]) return false;
        return true;
    }

    /// <summary>
    /// True when the whole password is one unbroken ascending or descending
    /// run of letters or digits ("abcdef", "987656"). Mixed content
    /// ("abc123x") is not a run and passes. WPF logic verbatim.
    /// </summary>
    private static bool IsSingleRun(string pw)
    {
        if (pw.Length < MinLength) return false;

        bool ascending = true, descending = true;
        for (int i = 1; i < pw.Length; i++)
        {
            char prev = char.ToLowerInvariant(pw[i - 1]);
            char cur = char.ToLowerInvariant(pw[i]);
            if (!char.IsLetterOrDigit(prev) || !char.IsLetterOrDigit(cur))
                return false;
            if (char.IsLetter(prev) != char.IsLetter(cur))
                return false;
            if (cur != prev + 1) ascending = false;
            if (cur != prev - 1) descending = false;
            if (!ascending && !descending) return false;
        }
        return ascending || descending;
    }

    /// <summary>
    /// Account-identity check. Equality is always rejected. Containment
    /// requires a 4+ char username so short names cannot veto ordinary
    /// words ("ali" must not forbid "quality123"). WPF verbatim.
    /// </summary>
    private static string? MatchesIdentity(
        string pw, string? username, string? displayName)
    {
        string folded = pw.Trim().ToLowerInvariant();

        string user = (username ?? "").Trim().ToLowerInvariant();
        if (user.Length >= 3)
        {
            if (folded == user)
                return "Password cannot be the same as the username.";
            if (user.Length >= 4 && folded.Contains(user))
                return "Password cannot contain the username.";
        }

        string display = (displayName ?? "").Trim().ToLowerInvariant();
        if (display.Length >= 3)
        {
            string compact = display.Replace(" ", "");
            if (folded == display || (compact.Length >= 4 && folded == compact))
                return "Password cannot be the same as the display name.";
            if (compact.Length >= 6 && folded.Contains(compact))
                return "Password cannot contain the display name.";
        }

        return null;
    }

    /// <summary>
    /// Blocklist hit, returned as the matched word for the message — or
    /// null. Matches the password itself plus the password with trailing
    /// digits stripped ("password1" and "glass12345" are caught).
    /// </summary>
    private static string? BlockedWord(string pw)
    {
        string folded = pw.Trim().ToLowerInvariant();
        if (Blocked.Contains(folded)) return folded;

        string stripped = folded.TrimEnd('0', '1', '2', '3', '4', '5', '6', '7', '8', '9');
        if (stripped.Length >= 4
            && stripped.Length < folded.Length
            && Blocked.Contains(stripped))
            return stripped;

        return null;
    }

    /// <summary>True when the password is blocklisted (tests/diagnostics).</summary>
    public static bool IsBlocked(string? password)
    {
        if (string.IsNullOrWhiteSpace(password)) return false;
        return BlockedWord(password) != null;
    }

    // Worst-passwords + keyboard walks + product/domain-specific words —
    // WPF blocklist verbatim. An offline-capable floor, not a breach-corpus
    // lookup; the words an attacker would try FIRST against THIS product
    // (proglass, glass, dubai, …) are blocked locally.
    private static readonly HashSet<string> Blocked = new(
        new[]
        {
            "password", "passw0rd", "passwd", "pass123", "password!", "password12",
            "123456", "1234567", "12345678", "123456789", "1234567890",
            "123123", "123321", "12345", "1234", "111111", "000000", "654321",
            "qwerty", "qwerty123", "qwertyuiop", "asdfgh", "asdfghjkl", "zxcvbn",
            "1qaz2wsx", "1q2w3e4r", "zaq12wsx",
            "letmein", "welcome", "welcome1", "welcome123", "changeme", "default",
            "admin", "admin1", "admin12", "admin123", "administrator",
            "root", "toor", "superuser", "sysadmin", "manager", "operator",
            "login", "master", "test", "test123", "guest", "guest123", "demo", "demo123",
            "abc123", "abcd1234", "abcdef", "abcdefg", "abcdef123",
            "football", "baseball", "basketball", "superman", "batman",
            "dragon", "monkey", "shadow", "sunshine", "princess", "qazwsx",
            "charlie", "aa123456", "donald", "freedom", "whatever",
            "michael", "jordan", "michelle", "jennifer", "thomas",
            "hunter", "buster", "sophie", "pepper", "ginger", "summer",
            // Product / domain specific: tried first against this app.
            "proglass", "proglass123", "glass", "glass123",
            "dubai", "dubai123", "sharjah", "abudhabi", "abu", "ajman",
            "uae", "emirates", "gulf", "arabia",
            "invoice", "invoice123", "proforma", "quotation", "accounts",
            "company", "office", "factory", "store", "workshop",
            "windows", "computer", "laptop", "mobile", "samsung", "iphone",
        },
        StringComparer.OrdinalIgnoreCase);
}

using System;
using System.Text;

namespace ProGlassAutomation.Services.Audit
{
    /// <summary>
    /// The one username key format used by account creation, login, and the
    /// unique SQLite index. Form KC folds compatibility variants before an
    /// invariant case fold; leading/trailing whitespace is not identity.
    /// </summary>
    public static class UsernameNormalizer
    {
        public static string Normalize(string? username)
        {
            string value = (username ?? string.Empty).Trim();
            if (value.Length == 0) return string.Empty;
            return value.Normalize(NormalizationForm.FormKC).ToUpperInvariant();
        }
    }
}

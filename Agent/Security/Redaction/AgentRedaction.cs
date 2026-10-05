// ═══════════════════════════════════════════════════════════════════════
// FILE: Agent/Security/Redaction/AgentRedaction.cs
// PROJECT: ProGlassAutomation — ProGlass Agent (PHASE T)
//
// REDACTION + INPUT SANITIZING, as their own named module.
//
// The master prompt names Redaction as a distinct security component.
// These patterns and the injection filter previously lived inside
// AgentSecurityService; moving them here means the answer to "what does
// the agent scrub, exactly?" is one short file.
//
// AgentSecurityService now DELEGATES to this class rather than
// duplicating it. There is deliberately one implementation: two copies of
// a security rule is how they drift apart.
//
// HONEST LIMIT
//   Pattern matching catches known credential shapes. It cannot catch a
//   secret that looks like ordinary prose. This reduces accidental
//   leakage; it is not a guarantee, and must not be described as one.
// ═══════════════════════════════════════════════════════════════════════

using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace ProGlassAutomation.Agent.Security.Redaction
{
    /// <summary>Scrubs secrets out, and hostile instructions in.</summary>
    public static class AgentRedaction
    {
        /// <summary>Maximum length of any sanitized input.</summary>
        public const int MaxUntrustedLength = 8000;

        /// <summary>Replacement token written over anything secret-shaped.</summary>
        public const string RedactedToken = "[REDACTED]";

        /// <summary>Replacement token written over injection attempts.</summary>
        public const string FilteredToken = "[filtered]";

        // ══════════════════════════════════════════════════════════════
        //  SECRET PATTERNS
        // ══════════════════════════════════════════════════════════════

        private static readonly (Regex Pattern, string Label)[] SecretPatterns =
        {
            (new Regex(@"AIza[0-9A-Za-z\-_]{10,}", RegexOptions.Compiled), "google-key"),
            (new Regex(@"\bsk-[0-9A-Za-z\-_]{10,}", RegexOptions.Compiled), "sk-key"),
            (new Regex(@"Bearer\s+[0-9A-Za-z\-_\.=]{10,}",
                RegexOptions.Compiled | RegexOptions.IgnoreCase), "bearer"),
            (new Regex(@"password\s*=\s*\S+",
                RegexOptions.Compiled | RegexOptions.IgnoreCase), "password"),
            (new Regex(@"api[_\-]?key\s*=\s*\S+",
                RegexOptions.Compiled | RegexOptions.IgnoreCase), "api-key")
        };

        /// <summary>Replaces anything that looks like a secret.</summary>
        public static string Redact(string? text)
        {
            if (string.IsNullOrEmpty(text)) return "";

            string result = text;
            foreach (var (pattern, _) in SecretPatterns)
                result = pattern.Replace(result, RedactedToken);

            return result;
        }

        /// <summary>True when the text still looks like it holds a secret.</summary>
        public static bool ContainsSecret(string? text) =>
            !string.IsNullOrEmpty(text) &&
            SecretPatterns.Any(p => p.Pattern.IsMatch(text));

        // ══════════════════════════════════════════════════════════════
        //  PROMPT INJECTION
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Phrases that only ever appear in an attempt to hijack an agent.
        /// Untrusted content is DATA, never instructions — filtering these
        /// keeps a pasted invoice note from becoming a command.
        /// </summary>
        private static readonly string[] InjectionPhrases =
        {
            "ignore all previous instructions",
            "ignore previous instructions",
            "ignore the above",
            "disregard all previous",
            "disregard previous instructions",
            "forget your instructions",
            "you are now",
            "system prompt",
            "developer mode",
            "jailbreak",
            "reveal your instructions",
            "print your prompt",
            "act as an unrestricted"
        };

        /// <summary>
        /// Neutralizes untrusted text: strips control characters, filters
        /// injection phrases, caps length. Never throws, never returns null.
        /// </summary>
        public static string SanitizeUntrusted(string? text)
        {
            if (string.IsNullOrEmpty(text)) return "";

            // Strip control characters, keeping tab/newline as whitespace.
            var sb = new StringBuilder(text.Length);
            foreach (var ch in text)
            {
                if (char.IsControl(ch) && ch != '\n' && ch != '\r' && ch != '\t')
                    continue;
                sb.Append(ch);
            }

            string cleaned = sb.ToString();

            foreach (var phrase in InjectionPhrases)
            {
                cleaned = Regex.Replace(
                    cleaned, Regex.Escape(phrase), FilteredToken,
                    RegexOptions.IgnoreCase);
            }

            if (cleaned.Length > MaxUntrustedLength)
                cleaned = cleaned[..MaxUntrustedLength];

            return cleaned;
        }
    }
}

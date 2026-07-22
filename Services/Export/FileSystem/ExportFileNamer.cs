using ProGlassAutomation.Services.Export.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace ProGlassAutomation.Services.Export.FileSystem
{
    /// <summary>
    /// Consistent filename convention across all modules:
    ///
    ///   {DocumentId}_{CompanyName}_{Reference}_{DocumentDate:dd-MM-yyyy}.{ext}
    ///
    /// Examples:
    ///   PI-2026-001_EMAAR-PROPERTIES_TWR-A-L15_22-07-2026.pdf
    ///   JOB-2026-001_ALDAR-PROPERTIES_YAS-TWR-B_03-08-2026.pdf
    ///   DN-2026-001_EMAAR-PROPERTIES_TWR-A-L15_22-07-2026.pdf
    ///   TAX-2026-001_MERAAS_BB-VILLAS_15-09-2026.xlsx
    ///
    /// Sanitization rules:
    /// - UPPERCASE
    /// - Spaces → hyphens
    /// - Strip Windows-invalid filename characters
    /// - Cap each segment at 40 characters
    /// - Collapse multiple consecutive hyphens into one
    /// - Trim leading/trailing hyphens per segment
    /// - Empty segments are skipped entirely (no placeholders)
    /// </summary>
    public static class ExportFileNamer
    {
        private const int MaxSegmentLength = 40;

        private static readonly Regex MultipleHyphens =
            new(@"-{2,}", RegexOptions.Compiled);

        private static readonly Regex NonAlphaNumOrHyphen =
            new(@"[^A-Z0-9\-]", RegexOptions.Compiled);

        public static string Build(IDocumentLayout layout, ExportFormat format)
        {
            if (layout == null)
                throw new ArgumentNullException(nameof(layout));

            var segments = new List<string>();

            // Segment 1 — Document ID (mandatory)
            string id = Sanitize(layout.DocumentId);
            if (string.IsNullOrWhiteSpace(id))
                id = "DOCUMENT";
            segments.Add(id);

            // Segment 2 — Company name (optional)
            string company = Sanitize(layout.CompanyName);
            if (!string.IsNullOrWhiteSpace(company))
                segments.Add(company);

            // Segment 3 — Reference (optional)
            string reference = Sanitize(layout.Reference);
            if (!string.IsNullOrWhiteSpace(reference))
                segments.Add(reference);

            // Segment 4 — Document date (mandatory)
            string date = layout.DocumentDate == default
                ? DateTime.Now.ToString("dd-MM-yyyy")
                : layout.DocumentDate.ToString("dd-MM-yyyy");
            segments.Add(date);

            // ── Assemble ───────────────────────────────────────────────
            string ext = format switch
            {
                ExportFormat.Pdf => "pdf",
                ExportFormat.Excel => "xlsx",
                ExportFormat.Csv => "csv",
                _ => "bin"
            };

            return string.Join("_", segments) + "." + ext;
        }

        /// <summary>
        /// Sanitizes a segment: uppercase, spaces→hyphens, strip invalid,
        /// collapse hyphens, cap length, trim edge hyphens.
        /// Returns empty string if input is null/whitespace or nothing
        /// survives sanitization.
        /// </summary>
        private static string Sanitize(string? input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;

            // 1. Uppercase + replace whitespace clusters with single hyphen
            var sb = new StringBuilder(input.Length);
            bool lastWasSpace = false;
            foreach (var ch in input.Trim())
            {
                if (char.IsWhiteSpace(ch))
                {
                    if (!lastWasSpace)
                    {
                        sb.Append('-');
                        lastWasSpace = true;
                    }
                }
                else
                {
                    sb.Append(char.ToUpperInvariant(ch));
                    lastWasSpace = false;
                }
            }

            string s = sb.ToString();

            // 2. Strip Windows-invalid filename characters
            foreach (var bad in Path.GetInvalidFileNameChars())
                s = s.Replace(bad.ToString(), string.Empty);

            // 3. Strip anything that's not [A-Z0-9-] (removes underscores,
            //    dots, punctuation like slashes, quotes, commas, etc.)
            s = NonAlphaNumOrHyphen.Replace(s, "");

            // 4. Collapse multiple hyphens
            s = MultipleHyphens.Replace(s, "-");

            // 5. Trim leading/trailing hyphens
            s = s.Trim('-');

            // 6. Cap length (then re-trim edge hyphens in case cap
            //    landed on a hyphen)
            if (s.Length > MaxSegmentLength)
                s = s.Substring(0, MaxSegmentLength).Trim('-');

            return s;
        }
    }
}
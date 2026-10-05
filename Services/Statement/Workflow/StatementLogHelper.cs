// ═══════════════════════════════════════════════════════════════════════
//  Services/Statement/Workflow/StatementLogHelper.cs
//
//  Small display helpers for the statement generator's live log and PDF
//  attachment handling. Extracted from StatementGeneratorViewModel
//  (Phase 11/12 God-VM split) — behaviour unchanged.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Models.Statement.Workflow;
using System;
using System.IO;

namespace ProGlassAutomation.Services.Statement.Workflow
{
    /// <summary>Display helpers for the statement generator log/PDF.</summary>
    public static class StatementLogHelper
    {
        /// <summary>
        /// Infers a log severity from emoji/check glyphs in a workflow line.
        /// </summary>
        public static LogSeverity InferLogSeverity(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return LogSeverity.Info;

            if (text.Contains("✅") || text.Contains("✓")) return LogSeverity.Success;
            if (text.Contains("❌") || text.Contains("✗")) return LogSeverity.Error;
            if (text.Contains("⚠")) return LogSeverity.Warning;
            if (text.Contains("⏭")) return LogSeverity.Info;

            return LogSeverity.Info;
        }

        /// <summary>True when the file name is a common raster image.</summary>
        public static bool IsImageFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName)) return false;
            string ext = Path.GetExtension(fileName).ToLowerInvariant();
            return ext == ".png" || ext == ".jpg" || ext == ".jpeg" ||
                   ext == ".bmp" || ext == ".gif";
        }
    }
}

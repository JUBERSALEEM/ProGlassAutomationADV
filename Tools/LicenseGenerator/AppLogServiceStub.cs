// ═══════════════════════════════════════════════════════════════════════
// FILE: Tools/LicenseGenerator/AppLogServiceStub.cs
// PROJECT: ProGlassAutomation.LicenseGenerator
// MODULE: Developer-only tool — self-contained logging stand-in
//
// WHY THIS FILE EXISTS
// ────────────────────
// The generator shares the licensing primitives with the main
// app via <Compile Include="..." Link="Shared/..." />. One of those
// primitives, MachineFingerprint.cs, logs through
// ProGlassAutomation.Services.Logging.AppLogService. That class lives
// in the MAIN application project and drags in the Audit /
// CorrelationContext dependency graph — none of which a developer
// utility needs. Pulling them in would break the tool's "restore +
// build in seconds" promise and its zero-project-reference design.
//
// So this project defines a tiny, dependency-free stand-in for
// AppLogService, exposing only the members the shared licensing
// code and the generator's own code actually call. It writes to its
// own log file under %AppData%\ProGlassAutomation\logs and, like the
// real logger, NEVER throws — logging must never break the tool.
//
// The REAL AppLogService still ships with the main app; this stub is
// compiled ONLY into the LicenseGenerator project. Source files that
// are shared verbatim (MachineFingerprint.cs) are unchanged, so the
// app-side test suite that guards the value/type contract keeps
// passing.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.IO;
using System.Text;

namespace ProGlassAutomation.Services.Logging
{
    /// <summary>
    /// Minimal <c>LogContext</c> stand-in exposing the same field
    /// names as the app's real <c>LogContext</c>. The shared
    /// licensing code populates it with <c>Module</c> / <c>Class</c>
    /// / <c>Operation</c>; the authoritative implementation lives
    /// with the app and is never referenced from the tool.
    /// </summary>
    public sealed class LogContext
    {
        public string Module { get; set; } = "";
        public string Screen { get; set; } = "";
        public string Class { get; set; } = "";
        public string Method { get; set; } = "";
        public string Operation { get; set; } = "";
        public string CorrelationId { get; set; } = "";
        public string DocumentId { get; set; } = "";
        public string DocumentType { get; set; } = "";
        public string Extra { get; set; } = "";
    }

    /// <summary>
    /// Minimal, dependency-free <c>AppLogService</c> for the
    /// developer-only LicenseGenerator tool. Mirrors the static
    /// facade of the app's real logger (same method names and
    /// signatures) but writes to a dedicated log file and never
    /// throws.
    /// </summary>
    public static class AppLogService
    {
        private static readonly object _lock = new();

        private static string LogFolder => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ProGlassAutomation", "logs");

        private static string LogFilePath =>
            Path.Combine(LogFolder, $"license-generator-{DateTime.Now:yyyyMMdd}.log");

        public static void Debug(string message, LogContext? context = null)
            => Write("DEBUG", message);

        public static void Info(string message, LogContext? context = null)
            => Write("INFO ", message);

        public static void Warning(string message, LogContext? context = null)
            => Write("WARN ", message);

        public static string Error(
            string message, Exception? ex = null, LogContext? context = null)
        {
            Write("ERROR", message, ex);
            return "ERR-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        }

        public static string Critical(
            string message, Exception? ex = null, LogContext? context = null)
        {
            Write("CRIT ", message, ex);
            return "ERR-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        }

        private static void Write(string level, string message, Exception? ex = null)
        {
            try
            {
                Directory.CreateDirectory(LogFolder);
                var line = new StringBuilder()
                    .Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"))
                    .Append("  [").Append(level).Append("]  ")
                    .Append(message);
                if (ex != null)
                {
                    line.AppendLine()
                        .Append("  ").Append(ex.GetType().Name)
                        .Append(": ").Append(ex.Message);
                }

                lock (_lock)
                    File.AppendAllText(LogFilePath, line + Environment.NewLine);
            }
            catch
            {
                // Logging must never break the tool.
            }
        }
    }
}

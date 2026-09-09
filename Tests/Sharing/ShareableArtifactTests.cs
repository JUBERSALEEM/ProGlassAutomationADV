// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Sharing/ShareableArtifactTests.cs
// PROJECT: ProGlassAutomation.Tests
//
// Pins the format-agnostic artifact gate: supported files wrap with
// identity + MIME + size; missing/empty/unsupported files return a
// reason instead of throwing.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.Sharing;
using System;
using System.IO;
using Xunit;

namespace ProGlassAutomation.Tests.Sharing
{
    public class ShareableArtifactTests : IDisposable
    {
        private readonly string _scratchDir = Path.Combine(
            Path.GetTempPath(),
            "ProGlassShareTests_" + Guid.NewGuid().ToString("N"));

        public ShareableArtifactTests()
        {
            Directory.CreateDirectory(_scratchDir);
        }

        public void Dispose()
        {
            try { Directory.Delete(_scratchDir, recursive: true); }
            catch { /* best effort */ }
        }

        private string WriteTemp(string fileName, string content = "x")
        {
            string path = Path.Combine(_scratchDir, fileName);
            File.WriteAllText(path, content);
            return path;
        }

        [Theory]
        [InlineData("PI-2026-001.pdf", ShareFormat.Pdf, "application/pdf")]
        [InlineData("report.xlsx", ShareFormat.Excel,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
        [InlineData("cutlist.csv", ShareFormat.Csv, "text/csv")]
        public void FromFile_wraps_supported_files(
            string fileName, ShareFormat format, string mime)
        {
            string path = WriteTemp(fileName, "payload-bytes");

            var artifact = ShareableArtifact.FromFile(
                path, "PI", "PI-2026-001", "ProformaInvoice", true,
                out string? reason);

            Assert.NotNull(artifact);
            Assert.Null(reason);
            Assert.Equal(format, artifact!.Format);
            Assert.Equal(mime, artifact.MimeType);
            Assert.Equal(fileName, artifact.FileName);
            Assert.True(artifact.FileSizeBytes > 0);
            Assert.True(artifact.IsTemporary);
            Assert.Equal("PI-2026-001", artifact.DocumentNumber);
        }

        [Fact]
        public void FromFile_rejects_unsupported_extension()
        {
            string path = WriteTemp("notes.exe", "payload");

            var artifact = ShareableArtifact.FromFile(
                path, "PI", "PI-1", "ProformaInvoice", false,
                out string? reason);

            Assert.Null(artifact);
            Assert.Contains(".exe", reason);
        }

        [Fact]
        public void FromFile_rejects_missing_file()
        {
            var artifact = ShareableArtifact.FromFile(
                Path.Combine(_scratchDir, "gone.pdf"),
                "PI", "PI-1", "ProformaInvoice", false,
                out string? reason);

            Assert.Null(artifact);
            Assert.Contains("no longer exists", reason);
        }

        [Fact]
        public void FromFile_rejects_empty_file()
        {
            string path = Path.Combine(_scratchDir, "empty.pdf");
            File.WriteAllBytes(path, Array.Empty<byte>());

            var artifact = ShareableArtifact.FromFile(
                path, "PI", "PI-1", "ProformaInvoice", false,
                out string? reason);

            Assert.Null(artifact);
            Assert.Contains("empty", reason);
        }

        [Fact]
        public void FromFile_rejects_blank_path()
        {
            var artifact = ShareableArtifact.FromFile(
                null, "PI", "PI-1", "ProformaInvoice", false,
                out string? reason);

            Assert.Null(artifact);
            Assert.False(string.IsNullOrWhiteSpace(reason));
        }
    }
}

// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Agent/AttachmentValidationServiceTests.cs
// PROJECT: ProGlassAutomation.Tests
// MODULE: Phase 8 — Security Hardening v2 — AttachmentValidationService
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.Security;
using System.IO;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    public class AttachmentValidationServiceTests
    {
        [Theory]
        [InlineData("photo.jpg", true)]
        [InlineData("image.jpeg", true)]
        [InlineData("scan.png", true)]
        [InlineData("doc.pdf", true)]
        [InlineData("evil.exe", false)]
        [InlineData("script.bat", false)]
        [InlineData("file with no ext", false)]
        public void ValidateExtension_checks_allow_list(string fileName, bool shouldPass)
        {
            var allowed = new System.Collections.Generic.HashSet<string>(System.StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".pdf" };
            var result = AttachmentValidationService.ValidateExtension(fileName, allowed, "CORR-TEST");

            if (shouldPass)
                Assert.True(result.IsSuccess);
            else
                Assert.True(result.IsFailure);
        }

        [Theory]
        [InlineData("normal.pdf", true)]
        [InlineData("my file.jpg", true)]
        [InlineData("file..jpg", false)] // contains ..
        [InlineData("folder/file.jpg", false)] // contains separator
        [InlineData("file.", false)] // ends with dot
        [InlineData("file ", false)] // ends with space
        [InlineData("file:name.jpg", false)] // contains colon
        [InlineData("...", false)] // all dots
        [InlineData("", false)]
        public void ValidateFileName_blocks_traversal_and_invalid(string fileName, bool shouldPass)
        {
            var result = AttachmentValidationService.ValidateFileName(fileName, "CORR-TEST");

            if (shouldPass)
                Assert.True(result.IsSuccess);
            else
                Assert.True(result.IsFailure);
        }

        [Fact]
        public void ValidateSize_fails_when_file_too_large()
        {
            // Create temp file >10MB
            string tempFile = Path.Combine(Path.GetTempPath(), "large_test.pdf");
            try
            {
                // Create 11MB file
                using var fs = new FileStream(tempFile, FileMode.Create, FileAccess.Write);
                fs.SetLength(11L * 1024 * 1024);

                var result = AttachmentValidationService.ValidateSize(tempFile, "CORR-TEST");
                Assert.True(result.IsFailure);
                Assert.Contains("too large", result.Error.ToLower());
            }
            finally
            {
                try { File.Delete(tempFile); } catch { }
            }
        }

        [Fact]
        public void ValidateSize_succeeds_when_small()
        {
            string tempFile = Path.Combine(Path.GetTempPath(), "small_test.pdf");
            try
            {
                File.WriteAllText(tempFile, "small pdf content");
                var result = AttachmentValidationService.ValidateSize(tempFile, "CORR-TEST");
                Assert.True(result.IsSuccess);
            }
            finally
            {
                try { File.Delete(tempFile); } catch { }
            }
        }

        [Fact]
        public void ValidateMime_detects_jpg_header()
        {
            string tempFile = Path.Combine(Path.GetTempPath(), "mime_test.jpg");
            try
            {
                // Write JPEG header
                File.WriteAllBytes(tempFile, new byte[] { 0xFF, 0xD8, 0xFF, 0x00, 0x01, 0x02 });

                var result = AttachmentValidationService.ValidateMime(tempFile, "CORR-TEST");
                Assert.True(result.IsSuccess);
            }
            finally
            {
                try { File.Delete(tempFile); } catch { }
            }
        }

        [Fact]
        public void ValidateMime_fails_when_header_mismatch()
        {
            string tempFile = Path.Combine(Path.GetTempPath(), "mime_fail.jpg");
            try
            {
                // Write PNG header but .jpg extension
                File.WriteAllBytes(tempFile, new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x00 });

                var result = AttachmentValidationService.ValidateMime(tempFile, "CORR-TEST");
                Assert.True(result.IsFailure);
                Assert.Contains("does not match", result.Error.ToLower());
            }
            finally
            {
                try { File.Delete(tempFile); } catch { }
            }
        }

        [Fact]
        public void ValidateImage_combines_extension_size_mime_name()
        {
            string tempFile = Path.Combine(Path.GetTempPath(), "combined_test.jpg");
            try
            {
                File.WriteAllBytes(tempFile, new byte[] { 0xFF, 0xD8, 0xFF, 0x00 });

                var result = AttachmentValidationService.ValidateImage(tempFile, "CORR-TEST");
                Assert.True(result.IsSuccess);
            }
            finally
            {
                try { File.Delete(tempFile); } catch { }
            }
        }

        [Fact]
        public void ValidatePdf_combines_checks()
        {
            string tempFile = Path.Combine(Path.GetTempPath(), "combined_test.pdf");
            try
            {
                File.WriteAllBytes(tempFile, new byte[] { 0x25, 0x50, 0x44, 0x46, 0x00 }); // %PDF

                var result = AttachmentValidationService.ValidatePdf(tempFile, "CORR-TEST");
                Assert.True(result.IsSuccess);
            }
            finally
            {
                try { File.Delete(tempFile); } catch { }
            }
        }

        [Fact]
        public async System.Threading.Tasks.Task ValidateAttachmentAsync_full_pipeline()
        {
            string tempFile = Path.Combine(Path.GetTempPath(), "full_pipeline_test.pdf");
            try
            {
                File.WriteAllBytes(tempFile, new byte[] { 0x25, 0x50, 0x44, 0x46, 0x00 });

                var result = await AttachmentValidationService.ValidateAttachmentAsync(tempFile, "CORR-TEST");
                Assert.True(result.IsSuccess);
            }
            finally
            {
                try { File.Delete(tempFile); } catch { }
            }
        }
    }
}

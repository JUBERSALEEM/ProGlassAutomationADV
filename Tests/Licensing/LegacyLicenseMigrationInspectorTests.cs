using System;
using System.IO;
using ProGlassAutomation.Services.Licensing;
using Xunit;

namespace ProGlassAutomation.Tests.Licensing
{
    public sealed class LegacyLicenseMigrationInspectorTests : IDisposable
    {
        private readonly string _tempFolder;

        public LegacyLicenseMigrationInspectorTests()
        {
            _tempFolder = Path.Combine(
                Path.GetTempPath(),
                "proglass-legacy-migration-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempFolder);
        }

        public void Dispose()
        {
            try { Directory.Delete(_tempFolder, true); }
            catch { }
        }

        [Fact]
        public void Inspect_returns_missing_when_file_does_not_exist()
        {
            var result = LegacyLicenseMigrationInspector.Inspect(Path.Combine(_tempFolder, "missing.lic"));
            Assert.False(result.Exists);
            Assert.False(result.CanMigrate);
        }

        [Fact]
        public void Inspect_extracts_safe_metadata_from_parseable_legacy_artifact()
        {
            string path = Path.Combine(_tempFolder, "legacy.lic");
            File.WriteAllText(path,
                "{" +
                "\"envelopeVersion\":1," +
                "\"payload\":{" +
                "\"schemaVersion\":1," +
                "\"licenseId\":\"LEG-001\"," +
                "\"edition\":\"Business\"," +
                "\"customerId\":\"CUST-LEG\"," +
                "\"companyName\":\"Legacy Glass LLC\"," +
                "\"issuedAt\":\"2026-08-01T00:00:00Z\"," +
                "\"expiresAt\":\"2027-08-01T00:00:00Z\"," +
                "\"maxActivations\":3," +
                "\"features\":[\"Inquiry.*\"]," +
                "\"machineFingerprint\":null," +
                "\"notes\":\"Legacy issue\"," +
                "\"trialSource\":\"\"," +
                "\"trialExpiresAt\":\"0001-01-01T00:00:00.000Z\"}," +
                "\"signature\":\"not-trusted-here\"}");

            var result = LegacyLicenseMigrationInspector.Inspect(path);
            Assert.True(result.Exists);
            Assert.True(result.CanMigrate);
            Assert.Equal("LEG-001", result.LicenseId);
            Assert.Equal("CUST-LEG", result.CustomerId);
            Assert.Equal("Legacy Glass LLC", result.CompanyName);
            Assert.Equal("Business", result.Edition);
            Assert.True(result.HasSignatureField);
        }

        [Fact]
        public void BuildMigrationPrompt_mentions_detected_legacy_metadata()
        {
            string path = Path.Combine(_tempFolder, "legacy2.lic");
            File.WriteAllText(path,
                "{" +
                "\"envelopeVersion\":2," +
                "\"payload\":{" +
                "\"schemaVersion\":1," +
                "\"licenseId\":\"LEG-002\"," +
                "\"edition\":\"Factory\"," +
                "\"customerId\":\"CUST-002\"," +
                "\"companyName\":\"Factory Glass\"," +
                "\"issuedAt\":\"2026-08-01T00:00:00Z\"," +
                "\"expiresAt\":\"2027-08-01T00:00:00Z\"," +
                "\"maxActivations\":1," +
                "\"features\":[\"*\"]," +
                "\"machineFingerprint\":\"abc123\"," +
                "\"notes\":\"Legacy issue\"," +
                "\"trialSource\":\"\"," +
                "\"trialExpiresAt\":\"0001-01-01T00:00:00.000Z\"}," +
                "\"signature\":\"sig\"}");

            string prompt = LegacyLicenseMigrationInspector.BuildMigrationPrompt(path);
            Assert.Contains("LEG-002", prompt, StringComparison.Ordinal);
            Assert.Contains("Factory Glass", prompt, StringComparison.Ordinal);
            Assert.Contains("machine-bound", prompt, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("secure offline activation flow", prompt, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Inspect_rejects_oversized_legacy_artifact()
        {
            string path = Path.Combine(_tempFolder, "huge.lic");
            File.WriteAllText(path, new string('X', 600_000));
            var result = LegacyLicenseMigrationInspector.Inspect(path);
            Assert.True(result.Exists);
            Assert.False(result.CanMigrate);
            Assert.Contains("size limit", result.Message, StringComparison.OrdinalIgnoreCase);
        }
    }
}

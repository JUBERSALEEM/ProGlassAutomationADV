// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Sharing/ProviderValidationTests.cs
// PROJECT: ProGlassAutomation.Tests
//
// Pins provider Validate() + Capabilities ONLY. ShareAsync() is never
// invoked — it launches external processes (browser, mail client,
// Explorer), which a test must not do.
//
// Validate() returns "" when the request is executable, else the
// user-facing reason the composer shows.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.Sharing;
using ProGlassAutomation.Services.Sharing.Providers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Xunit;

namespace ProGlassAutomation.Tests.Sharing
{
    public class ProviderValidationTests : IDisposable
    {
        private readonly string _scratchDir = Path.Combine(
            Path.GetTempPath(),
            "ProGlassProviderTests_" + Guid.NewGuid().ToString("N"));

        public ProviderValidationTests()
        {
            Directory.CreateDirectory(_scratchDir);
        }

        public void Dispose()
        {
            try { Directory.Delete(_scratchDir, recursive: true); }
            catch { /* best effort */ }
        }

        private ShareableArtifact MakeArtifact()
        {
            string path = Path.Combine(_scratchDir, "PI-2026-001.pdf");
            File.WriteAllText(path, "payload-bytes");
            return ShareableArtifact.FromFile(
                path, "PI", "PI-2026-001", "ProformaInvoice", true,
                out _)!;
        }

        private static ShareExecutionRequest Request(
            IReadOnlyList<ShareableArtifact> artifacts,
            ShareRecipient? recipient,
            CancellationToken token = default) =>
            new(new ShareContext { DocumentTypeKey = "PI" },
                artifacts, recipient, "message", "subject", token);

        [Fact]
        public void WhatsApp_accepts_recipient_with_usable_phone()
        {
            var provider = new WhatsAppShareProvider();
            var recipient = new ShareRecipient(
                ShareRecipientKind.CustomerAttention,
                "Mr. Ahmed", "Skyline", "050 123 4567");

            string invalid = provider.Validate(
                Request(new[] { MakeArtifact() }, recipient));

            Assert.Equal("", invalid);
            Assert.True(provider.Capabilities.RequiresPhone);
        }

        [Fact]
        public void WhatsApp_rejects_missing_or_unusable_phone()
        {
            var provider = new WhatsAppShareProvider();
            var artifacts = new[] { MakeArtifact() };

            Assert.NotEqual("", provider.Validate(
                Request(artifacts, null)));
            Assert.NotEqual("", provider.Validate(
                Request(artifacts, new ShareRecipient(
                    ShareRecipientKind.Customer, "Skyline"))));
            Assert.NotEqual("", provider.Validate(
                Request(artifacts, new ShareRecipient(
                    ShareRecipientKind.Customer, "Skyline", "", "12"))));
        }

        [Fact]
        public void Email_accepts_recipient_with_plausible_email()
        {
            var provider = new EmailShareProvider();
            var recipient = new ShareRecipient(
                ShareRecipientKind.Customer,
                "Skyline", "", "", "accounts@skyline.example");

            string invalid = provider.Validate(
                Request(new[] { MakeArtifact() }, recipient));

            Assert.Equal("", invalid);
            Assert.True(provider.Capabilities.RequiresEmail);
        }

        [Fact]
        public void Email_rejects_missing_or_implausible_email()
        {
            var provider = new EmailShareProvider();
            var artifacts = new[] { MakeArtifact() };

            Assert.NotEqual("", provider.Validate(
                Request(artifacts, null)));
            Assert.NotEqual("", provider.Validate(
                Request(artifacts, new ShareRecipient(
                    ShareRecipientKind.Customer, "Skyline"))));
            Assert.NotEqual("", provider.Validate(
                Request(artifacts, new ShareRecipient(
                    ShareRecipientKind.Customer, "Skyline",
                    "", "", "not-an-email"))));
        }

        [Fact]
        public void SystemFile_needs_no_recipient()
        {
            var provider = new SystemFileShareProvider();

            string invalid = provider.Validate(
                Request(new[] { MakeArtifact() }, null));

            Assert.Equal("", invalid);
            Assert.False(provider.Capabilities.RequiresRecipient);
        }

        [Fact]
        public void All_providers_reject_empty_artifacts_and_cancellation()
        {
            ISharingProvider[] providers =
            {
                new WhatsAppShareProvider(),
                new EmailShareProvider(),
                new SystemFileShareProvider()
            };
            var recipient = new ShareRecipient(
                ShareRecipientKind.Customer,
                "Skyline", "", "050 123 4567", "a@b.example");
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            foreach (var provider in providers)
            {
                Assert.NotEqual("", provider.Validate(
                    Request(Array.Empty<ShareableArtifact>(), recipient)));
                Assert.NotEqual("", provider.Validate(
                    Request(new[] { MakeArtifact() }, recipient, cts.Token)));
            }
        }
    }
}

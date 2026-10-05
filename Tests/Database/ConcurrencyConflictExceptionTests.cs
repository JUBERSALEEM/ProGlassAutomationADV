// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Database/ConcurrencyConflictExceptionTests.cs
// PROJECT: ProGlassAutomation.Tests
//
// ERP hardening (Phase 5). Pins the shape of the optimistic-concurrency
// signal a repository raises when two users save the same document: it must
// carry both versions and produce a message that tells the operator their
// change was NOT saved (so the other user's work is safe) and to reload.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.Database;
using Xunit;

namespace ProGlassAutomation.Tests.Database
{
    public class ConcurrencyConflictExceptionTests
    {
        [Fact]
        public void Carries_both_versions_and_identity()
        {
            var ex = new ConcurrencyConflictException("Tax Invoice", "INV-1001", 3, 5);

            Assert.Equal("Tax Invoice", ex.DocumentKind);
            Assert.Equal("INV-1001", ex.DocumentId);
            Assert.Equal(3, ex.ExpectedRowVersion);
            Assert.Equal(5, ex.ActualRowVersion);
        }

        [Fact]
        public void Message_names_the_document_and_reassures_no_data_loss()
        {
            var ex = new ConcurrencyConflictException("Tax Invoice", "INV-1001", 3, 5);

            Assert.Contains("INV-1001", ex.Message);
            Assert.Contains("Tax Invoice", ex.Message);
            // The other user's update must NOT be lost — the message says so.
            Assert.Contains("not saved", ex.Message);
            Assert.Contains("reload", ex.Message, System.StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Handles_missing_id_gracefully()
        {
            var ex = new ConcurrencyConflictException("Delivery Note", "", 1, 2);
            Assert.Contains("Delivery Note", ex.Message);
        }
    }
}

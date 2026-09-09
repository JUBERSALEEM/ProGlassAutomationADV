// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Sharing/ShareClipboardTests.cs
// PROJECT: ProGlassAutomation.Tests
//
// Pins ShareClipboard's SAFE, headless paths only. The real clipboard is
// never exercised here (a CI agent has no interactive session / STA
// clipboard), so we only assert the guard branches that short-circuit
// BEFORE any Clipboard call: null input and non-existent files return
// false without throwing.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.Sharing;
using System;
using System.IO;
using Xunit;

namespace ProGlassAutomation.Tests.Sharing
{
    public class ShareClipboardTests
    {
        [Fact]
        public void CopyFiles_null_returns_false_without_throwing()
        {
            Assert.False(ShareClipboard.CopyFiles(null));
        }

        [Fact]
        public void CopyFiles_empty_returns_false()
        {
            Assert.False(ShareClipboard.CopyFiles(Array.Empty<string>()));
        }

        [Fact]
        public void CopyFiles_nonexistent_paths_return_false()
        {
            string missing = Path.Combine(
                Path.GetTempPath(),
                "ProGlassClipboardTest_" + Guid.NewGuid().ToString("N") + ".pdf");
            Assert.False(ShareClipboard.CopyFiles(new[] { missing }));
        }
    }
}

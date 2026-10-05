// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/About/AboutCrashReportsViewModelTests.cs
// PROJECT: ProGlassAutomation
// MODULE: Tests — AboutCrashReportsViewModel
//
// Light tests for the VM. The constructor calls
// Refresh() which reads the live %AppData% folder, so
// we cannot easily assert specific entries. We
// assert the structural invariants: IsEmpty reflects
// the count, Refresh() does not throw, the entry's
// formatted strings are sensible.
// ═══════════════════════════════════════════════════════════════════════

using System;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.ViewModels.About;
using Xunit;

namespace ProGlassAutomation.Tests.About
{
    public sealed class AboutCrashReportsViewModelTests
    {
        [Fact]
        public void Constructor_does_not_throw()
        {
            // The constructor calls Refresh which
            // reads the live %AppData% folder. As long
            // as that folder is accessible, the
            // constructor returns. We assert no
            // exception escapes.
            var ex = Record.Exception(() => new AboutCrashReportsViewModel());
            Assert.Null(ex);
        }

        [Fact]
        public void IsEmpty_reflects_Reports_count()
        {
            var vm = new AboutCrashReportsViewModel();
            // The two states must agree.
            Assert.Equal(vm.Reports.Count == 0, vm.IsEmpty);
        }

        [Fact]
        public void Refresh_does_not_throw()
        {
            var vm = new AboutCrashReportsViewModel();
            var ex = Record.Exception(() => vm.Refresh());
            Assert.Null(ex);
        }

        [Fact]
        public void ClearAllCommand_canExecute_reflects_count()
        {
            var vm = new AboutCrashReportsViewModel();
            if (vm.Reports.Count == 0)
            {
                Assert.False(vm.ClearAllCommand.CanExecute(null));
            }
            else
            {
                Assert.True(vm.ClearAllCommand.CanExecute(null));
            }
        }

        [Fact]
        public void CrashReportEntry_formats_size_humanely()
        {
            // Direct test of the entry's size
            // formatter: we construct a synthetic
            // listing so we know the bytes count.
            var listing = new CrashReportService.ReportListing
            {
                FilePath = "x",
                FileSize = 2_048
            };
            var entry = new AboutCrashReportsViewModel.CrashReportEntry(listing);
            Assert.Contains("KB", entry.SizeDisplay);
        }

        [Fact]
        public void CrashReportEntry_handles_missing_metadata()
        {
            var listing = new CrashReportService.ReportListing
            {
                FilePath = "x"
                // No ErrorId, no type, no message.
            };
            var entry = new AboutCrashReportsViewModel.CrashReportEntry(listing);
            Assert.Equal("(no id)", entry.ErrorId);
            Assert.False(entry.HasErrorId);
            Assert.False(entry.HasMessage);
            // Timestamp and size still produced.
            Assert.False(string.IsNullOrEmpty(entry.TimestampDisplay));
        }
    }
}

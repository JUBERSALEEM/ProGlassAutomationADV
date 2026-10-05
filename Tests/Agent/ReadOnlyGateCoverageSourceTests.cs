using System;
using System.IO;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    /// <summary>
    /// Regression gate for the read-only mode enforcement sweep (2026-09-02,
    /// RELEASE_GATE §1 amber). Every repository / service that persists
    /// business data must call <c>ReadOnlyGate.Demand(...)</c> on each write
    /// entry point, mirroring the ProformaInvoice / TaxInvoice pattern.
    /// A repository that gains a new write method without a Demand call
    /// fails this suite.
    /// </summary>
    public sealed class ReadOnlyGateCoverageSourceTests
    {
        private static string Read(string root, params string[] parts)
            => File.ReadAllText(Path.Combine(root, Path.Combine(parts)));

        private static string Root()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "ProGlassAutomation.csproj")))
                dir = dir.Parent;
            return dir!.FullName;
        }

        [Fact]
        public void JobOrder_repository_mutations_are_guarded()
        {
            var src = Read(Root(), "Services", "JobOrder", "Repository", "JobOrderRepository.cs");
            Assert.Contains("ReadOnlyGate.Demand(\"create a job order\")", src, StringComparison.Ordinal);
            Assert.Contains("ReadOnlyGate.Demand(\"update a job order\")", src, StringComparison.Ordinal);
            Assert.Contains("ReadOnlyGate.Demand(\"delete a job order\")", src, StringComparison.Ordinal);
        }

        [Fact]
        public void Inquiry_repository_mutations_are_guarded()
        {
            var src = Read(Root(), "Services", "Inquiry", "InquiryRepository.cs");
            Assert.Contains("ReadOnlyGate.Demand(\"create an inquiry\")", src, StringComparison.Ordinal);
            Assert.Contains("ReadOnlyGate.Demand(\"update an inquiry\")", src, StringComparison.Ordinal);
            Assert.Contains("ReadOnlyGate.Demand(\"change an inquiry status\")", src, StringComparison.Ordinal);
            Assert.Contains("ReadOnlyGate.Demand(\"add an inquiry attachment\")", src, StringComparison.Ordinal);
            Assert.Contains("ReadOnlyGate.Demand(\"delete an inquiry attachment\")", src, StringComparison.Ordinal);
            Assert.Contains("ReadOnlyGate.Demand(\"delete an inquiry\")", src, StringComparison.Ordinal);
        }

        [Fact]
        public void Statement_repository_mutations_are_guarded()
        {
            var src = Read(Root(), "Services", "Statement", "Repository", "StatementRepository.cs");
            Assert.Contains("ReadOnlyGate.Demand(\"create a statement\")", src, StringComparison.Ordinal);
            Assert.Contains("ReadOnlyGate.Demand(\"update a statement\")", src, StringComparison.Ordinal);
            Assert.Contains("ReadOnlyGate.Demand(\"delete a statement\")", src, StringComparison.Ordinal);
            Assert.Contains("ReadOnlyGate.Demand(\"update a statement PDF path\")", src, StringComparison.Ordinal);
        }

        [Fact]
        public void Sheet_store_repository_mutations_are_guarded()
        {
            var src = Read(Root(), "Services", "SheetStore", "SheetStoreRepository.cs");
            Assert.Contains("ReadOnlyGate.Demand(\"update a glass sheet\")", src, StringComparison.Ordinal);
            Assert.Contains("ReadOnlyGate.Demand(\"delete a glass sheet\")", src, StringComparison.Ordinal);
            Assert.Contains("ReadOnlyGate.Demand(\"record a stock purchase\")", src, StringComparison.Ordinal);
            Assert.Contains("ReadOnlyGate.Demand(\"record stock usage\")", src, StringComparison.Ordinal);
        }

        [Fact]
        public void Master_data_repository_mutations_are_guarded()
        {
            var src = Read(Root(), "Services", "MasterData", "Repository", "MasterDataRepository.cs");
            foreach (var op in new[]
            {
                "save a company", "delete a company", "save a client", "delete a client",
                "save a project", "delete a project", "save a salesman", "delete a salesman",
                "auto-save master data from an invoice",
            })
                Assert.Contains("ReadOnlyGate.Demand(\"" + op + "\")", src, StringComparison.Ordinal);
        }

        [Fact]
        public void Specification_repository_mutations_are_guarded()
        {
            var src = Read(Root(), "Services", "ProformaInvoice", "SpecificationsRepository", "SpecificationsRepository.cs");
            Assert.Contains("ReadOnlyGate.Demand(\"save a specification\")", src, StringComparison.Ordinal);
            Assert.Contains("ReadOnlyGate.Demand(\"mark a specification as used\")", src, StringComparison.Ordinal);
            Assert.Contains("ReadOnlyGate.Demand(\"delete a specification\")", src, StringComparison.Ordinal);
        }

        [Fact]
        public void Ai_memory_writes_are_guarded()
        {
            var src = Read(Root(), "Services", "AI", "AIMemory", "AiMemoryService.cs");
            Assert.Contains("ReadOnlyGate.Demand(\"write AI memory\")", src, StringComparison.Ordinal);
            Assert.Contains("ReadOnlyGate.Demand(\"clear AI memory\")", src, StringComparison.Ordinal);
        }

        [Fact]
        public void Sample_data_service_mutations_are_guarded()
        {
            var src = Read(Root(), "Services", "SampleData", "SampleDataService.cs");
            Assert.Contains("ReadOnlyGate.Demand(\"seed sample data\")", src, StringComparison.Ordinal);
            Assert.Contains("ReadOnlyGate.Demand(\"clear all data\")", src, StringComparison.Ordinal);
            Assert.Contains("ReadOnlyGate.Demand(\"seed master profiles\")", src, StringComparison.Ordinal);
        }

        // By-design exclusions (documented, kept ungated):
        //  - SheetHistoryRepository.LogAsync  — audit/journal semantics, same
        //    category as AuditService which must keep writing in read-only mode.
        //  - InquiryLinkSync After*Async      — invoked only from
        //    ProformaInvoiceRepository methods that already Demand first, so
        //    they are transitively gated.
        //  - SqliteResilience                 — infrastructure retry wrapper,
        //    not a business write path.
    }
}

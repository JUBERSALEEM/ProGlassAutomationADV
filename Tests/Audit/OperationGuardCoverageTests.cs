// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Audit/OperationGuardCoverageTests.cs
// PROJECT: ProGlassAutomation.Tests
//
// AUDIT FINDING A-1 (re-check): 18 permission checks existed in
// ViewModels and ZERO in the service / repository layer. The
// brief's rule — "never rely only on hiding a button" — was not
// implemented. The OperationGuard.Demand coverage was added
// (28 calls across 5 repositories) in an earlier round, but
// the coverage was not pinned: a developer adding a new
// mutating method could skip the Demand call without the
// build failing. This test pins the coverage.
//
// HOW IT WORKS
//   1. Walks the configured list of repository files.
//   2. Parses each one with Roslyn and finds every public
//      method whose name starts with a mutating verb
//      (Save / Insert / Update / Delete / Mark / Approve /
//      Reject / Post / Cancel / Close / Submit / Convert /
//      Assign / Reset / Clear / Duplicate / Archive / Soft-
//      Delete / HardDelete / AutoSave), suffixed with
//      anything that ends in `Async`.
//   3. Excludes methods on the best-effort allow list
//      (DeleteMasterDnQuietlyAsync, ClearLinkedDeliveryNotes-
//      Async, ClearInvoicedByTaxInvoiceAsync, AutoSaveFrom-
//      InvoiceAsync) — these are explicitly the "do best
//      effort, do not block" path. Adding a new name to the
//      allow list requires a comment explaining why.
//   4. Asserts every other mutating method body contains a
//      call to OperationGuard.Demand.
//
// WHY IT IS A TEST
//   A new mutating method without a Demand call is a security
//   regression. This test fails the build on that regression
//   and prints the file + line + method name in the test
//   output, so the developer sees the gap immediately.
//
// WHAT IT DOES NOT DO
//   - It does not check the permission argument is the
//     CORRECT permission for the operation. That is a
//     human-review concern; this test only verifies that
//     a Demand call exists.
//   - It does not check public methods on services that
//     are not repositories. Other services that mutate
//     data (e.g. SampleDataService, BackupService) are
//     out of scope for this test; they have their own
//     permission models.
// ═══════════════════════════════════════════════════════════════════════

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;
using Xunit.Abstractions;

namespace ProGlassAutomation.Tests.Audit
{
    public class OperationGuardCoverageTests
    {
        private readonly ITestOutputHelper _out;

        public OperationGuardCoverageTests(ITestOutputHelper outputHelper)
        {
            _out = outputHelper;
        }

        // ── Repository files to scan ────────────────────────────
        //
        // Add a new file here when a new module's repository
        // lands. Keep this list explicit so a developer who
        // adds a module also has to add the file to the audit
        // gate.
        private static readonly string[] RepositoryFiles =
        {
            "Services/MasterData/Repository/MasterDataRepository.cs",
            "Services/DeliveryNote/Repository/DeliveryNoteRepository.cs",
            "Services/TaxInvoice/Repository/TaxInvoiceRepository.cs",
            "Services/Statement/Repository/StatementRepository.cs",
            "Services/JobOrder/Repository/JobOrderRepository.cs",
            "Services/ProformaInvoice/ProformaInvoiceRepository/ProformaInvoiceRepository.cs",
            "Services/Inquiry/InquiryRepository.cs",
        };

        // ── Mutating-verb prefix list ──────────────────────────
        //
        // Every public method whose name starts with one of
        // these verbs (and ends in Async) is checked for a
        // Demand call. The verbs match the existing naming
        // convention in the codebase; if a new convention
        // lands, add the new verb here.
        private static readonly Regex MutatingVerbPattern = new(
            @"^(?<verb>Save|Insert|Update|Delete|Mark|Approve|Reject|Post|"
            + @"Cancel|Close|Submit|Convert|Assign|Reset|Clear|Duplicate|"
            + @"Archive|SoftDelete|HardDelete|AutoSave)",
            RegexOptions.Compiled);

        // ── Best-effort allow list ─────────────────────────────
        //
        // Methods whose name advertises "Quietly", "AutoSave",
        // or "ClearLinked" are explicitly the best-effort path:
        // they are called from contexts where the permission
        // has already been checked at a higher level, or the
        // operation is a data fixup that the system is allowed
        // to skip. Adding a new name here requires a comment
        // explaining the reason.
        private static readonly string[] BestEffortMethods =
        {
            "DeleteMasterDnQuietlyAsync",
            "ClearLinkedDeliveryNotesAsync",
            "ClearInvoicedByTaxInvoiceAsync",
            "AutoSaveFromInvoiceAsync",
        };

        [Fact]
        public void Every_mutating_repository_method_calls_OperationGuard_Demand()
        {
            string repoRoot = FindRepoRoot();
            var missing = new List<MissingDemand>();

            foreach (var relativePath in RepositoryFiles)
            {
                var fullPath = Path.Combine(repoRoot, relativePath);
                if (!File.Exists(fullPath))
                {
                    // Surfaced as a "missing" entry so a new module
                    // added to the project is not silently absent
                    // from the audit.
                    missing.Add(new MissingDemand(
                        relativePath, 0, "<file not found>",
                        "Repository file listed in audit but not present on disk"));
                    continue;
                }

                missing.AddRange(ScanRepository(fullPath, repoRoot));
            }

            // Print a complete list of findings so a developer
            // who just made a regression can see all of them
            // in one test run.
            if (missing.Count == 0)
            {
                _out.WriteLine(
                    $"All mutating methods in {RepositoryFiles.Length} " +
                    "repository files call OperationGuard.Demand.");
            }
            else
            {
                _out.WriteLine(
                    $"{missing.Count} mutating methods do not call " +
                    "OperationGuard.Demand:");
                foreach (var m in missing
                    .OrderBy(m => m.File, StringComparer.Ordinal)
                    .ThenBy(m => m.Line))
                {
                    _out.WriteLine(
                        $"  {m.File}:{m.Line}  {m.Method}  — {m.Reason}");
                }
            }

            Assert.True(
                missing.Count == 0,
                "Mutating repository methods without OperationGuard.Demand:\n" +
                string.Join("\n", missing.Select(
                    m => $"  {m.File}:{m.Line}  {m.Method}")));
        }

        // ── Discovery + scan ──────────────────────────────────

        private static string FindRepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                if (dir.EnumerateFiles("ProGlassAutomation.csproj").Any())
                    return dir.FullName;
                dir = dir.Parent;
            }
            throw new InvalidOperationException(
                "Could not locate repo root from " +
                AppContext.BaseDirectory);
        }

        private record MissingDemand(
            string File, int Line, string Method, string Reason);

        private static IEnumerable<MissingDemand> ScanRepository(
            string fullPath, string repoRoot)
        {
            string source;
            try { source = File.ReadAllText(fullPath); }
            catch { yield break; }

            SyntaxTree tree;
            try { tree = CSharpSyntaxTree.ParseText(source); }
            catch { yield break; } // preprocessor-only file; compiler will report

            var root = tree.GetCompilationUnitRoot();
            var relative = fullPath.Substring(repoRoot.Length)
                                   .TrimStart(Path.DirectorySeparatorChar,
                                              Path.AltDirectorySeparatorChar)
                                   .Replace('\\', '/');

            foreach (var method in root.DescendantNodes()
                                       .OfType<MethodDeclarationSyntax>()
                                       .Where(m => m.Modifiers.Any(
                                            IsPublic)))
            {
                var name = method.Identifier.Text;

                // Only public async Task / Task<T> / void methods
                // count. Private helpers are not service-layer
                // entry points and don't need a Demand.
                if (!IsPublicEntryPoint(method)) continue;

                // Only mutating verb names.
                var verbMatch = MutatingVerbPattern.Match(name);
                if (!verbMatch.Success) continue;

                // Only Async methods (the codebase convention).
                if (!name.EndsWith("Async",
                        StringComparison.Ordinal)) continue;

                // Best-effort allow list.
                if (BestEffortMethods.Contains(name)) continue;

                // Look for an OperationGuard.Demand call anywhere
                // in the method body.
                bool hasDemand = method.DescendantNodes()
                    .OfType<InvocationExpressionSyntax>()
                    .Any(inv =>
                    {
                        var expr = inv.Expression.ToString();
                        // Match both `OperationGuard.Demand(...)`
                        // and `ProGlassAutomation.Services.Audit.
                        // OperationGuard.Demand(...)`.
                        return expr == "OperationGuard.Demand"
                            || expr.EndsWith(".OperationGuard.Demand",
                                StringComparison.Ordinal);
                    });

                if (!hasDemand)
                {
                    var line = method.SyntaxTree
                        .GetLineSpan(method.Span)
                        .StartLinePosition.Line + 1;
                    yield return new MissingDemand(
                        relative, line, name,
                        "no OperationGuard.Demand call in body");
                }
            }
        }

        private static bool IsPublic(SyntaxToken t) =>
            t.IsKind(SyntaxKind.PublicKeyword);

        private static bool IsPublicEntryPoint(MethodDeclarationSyntax m)
        {
            // Public modifier must be present.
            if (!m.Modifiers.Any(IsPublic)) return false;

            // Must be on a class (not a local function or a
            // nested type method).
            if (m.Parent is not TypeDeclarationSyntax) return false;

            // Return type must be one of the repository
            // entry-point shapes. We accept any return type
            // here; the body's `Demand` call is what we
            // actually check. We do NOT filter on return
            // type because some mutating methods return
            // `Result` (a custom type) and we want all of
            // them covered.
            return true;
        }
    }
}

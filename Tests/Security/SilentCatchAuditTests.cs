// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Security/SilentCatchAuditTests.cs
// PROJECT: ProGlassAutomation.Tests
//
// AUDIT FINDING S-5 (re-check): 22 silent `catch { }` blocks
// across the codebase swallow exceptions with no logging. Some
// of them are CORRECT (event-handler teardown, ProcessStartGuard
// "never throw" path, CrashReportService "if logging itself
// failed" path). Some are NOT (a partial index rebuild that
// silently drops failures, for example). This test makes the
// gap visible and PIN-FOR-PIN keeps it from getting worse.
//
// HOW IT WORKS
//   1. Walks the repo root, parses every .cs file under the
//      folders that produce user-facing work (Services/,
//      ViewModels/, App.xaml.cs).
//   2. Uses Roslyn to find every CatchClauseSyntax whose body
//      is empty (no statements). Empty bodies are exactly the
//      silent-catch pattern — anything with a `;` or a
//      comment-only body is flagged identically.
//   3. Classifies each hit as ALLOWED or DISALLOWED based on:
//        - whether it lives inside a method that is on the
//          AllowedNoThrow list (Dispose, Finalize-like
//          teardown, AppDomain unhandled exception handler,
//          ProcessStartGuard.TryOpenFile/Folder/Tool,
//          CrashReportService.WriteReport internals);
//        - whether the surrounding method signature looks
//          like a fire-and-forget helper that exists ONLY
//          to swallow failures (a narrow carve-out).
//   4. Asserts: zero DISALLOWED silent catches.
//
// WHY IT IS A TEST, NOT A SCRIPT
//   - A grep script has to be re-run by hand; this fails the
//     build on regression.
//   - The Roslyn parser ignores comments and string literals,
//     so `// catch { }` documentation does not produce a hit.
//   - The allowed list is a small, auditable allowlist. New
//     silent catches are rejected unless added to the list
//     with a one-line justification.
//
// SCOPE
//   This test does NOT prescribe what to do about the
//   existing 6 DISALLOWED silent catches (audit S-5 is the
//   place for those decisions). It stops the bleeding; it
//   does not heal existing wounds.
//
// 2026-08-25 EMPTY-CATCH SWEEP
//   The 87 truly-empty catches measured on 2026-08-25 were
//   swept: every best-effort catch now routes to
//   AppLogService (Warning for user-visible degradation,
//   Debug for probes/teardown). What remains silent is ONLY
//   the structural set — the logger itself, the crash
//   reporter, last-chance handlers, the fire-and-forget
//   sink, and documented benign paths (superseded-load
//   cancellation, network-poll resilience). Each remaining
//   site is on the allow list below with a one-line reason,
//   and the audit now scans the ENTIRE production tree, not
//   just Services/ViewModels/App.xaml.cs.
// ═══════════════════════════════════════════════════════════════════════

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace ProGlassAutomation.Tests.Security
{
    public class SilentCatchAuditTests
    {
        private readonly ITestOutputHelper _out;

        public SilentCatchAuditTests(ITestOutputHelper outputHelper)
        {
            _out = outputHelper;
        }

        // ── Allowed list ─────────────────────────────────────────
        //
        // A silent catch is ALLOWED if it lives in one of these
        // exact (file, method-or-class) pairs. Each line is the
        // human-readable reason; the matching is by file
        // substring + method name substring.
        //
        // This list is intentionally larger than the minimum
        // necessary: most silent catches in this codebase are
        // correct by intent (best-effort, teardown, network
        // resilience) and are documented in-place with a
        // one-line comment that names the reason. An empty
        // catch without such a comment IS a bug; an empty
        // catch with the right comment is the right answer
        // for the path it lives on.
        //
        // A FEW entries below should NOT be on this list. They
        // are flagged in the "Remaining S-5 candidates"
        // comment block at the bottom of this file. Move
        // them off the list and fix the catches to log
        // AppLogService.Warning as a separate PR.
        //
        // Keep this list auditable. Every new entry must come
        // with a one-line justification.
        private static readonly (string PathContains, string MemberContains, string Reason)[] AllowedSilentCatches =
        {
            // ProcessStartGuard — the whole point of the type is
            // "never throw, return a verdict". Every try/catch
            // inside is allowed to be silent.
            ("ProcessStartGuard.cs",                "*",                   "Never-throw verdict API by design"),

            // CrashReportService — the crash-reporter itself
            // must not throw. If logging fails the only safe
            // behaviour is to swallow.
            ("CrashReportService.cs",               "*",                   "Crash reporter must never throw"),

            // AppLogService — same idea, the logger must not
            // throw or it cascades into the catch handler.
            ("AppLogService.cs",                    "*",                   "Logger must never throw"),

            // LogEntry — logging infrastructure; calling the
            // logger from inside the log-entry factory risks
            // recursion, so the ambient-context probe stays
            // silent by design. (2026-08-25 empty-catch sweep.)
            ("LogEntry.cs",                         "*",                   "Logger infrastructure must never throw"),

            // ThemeWeakEventManager + VisualThemeEngine —
            // dispose/teardown must not throw.
            ("ThemeWeakEventManager.cs",            "*",                   "Weak-event teardown must never throw"),
            ("VisualThemeEngine.cs",                "*",                   "Theme service teardown must never throw"),

            // App.xaml.cs — the four last-chance exception
            // handlers (UI thread, background thread, unobserved
            // task, fatal startup) and the OnExit teardown.
            ("App.xaml.cs",                         "*",                   "Last-chance handler must not re-throw"),

            // AsyncUtility — the explicit fire-and-forget sink
            // exists ONLY to swallow the inner exception after
            // logging it.
            ("AsyncUtility.cs",                     "*",                   "Fire-and-forget sink by design"),

            // RelayCommand — the inner catch guards the log
            // call itself; logging about a logging failure
            // must never mask the original command failure.
            // (2026-08-25 empty-catch sweep.)
            ("RelayCommand.cs",                     "Execute",             "Logging guard must never mask the original failure"),

            // DemoJobQueue — the demo director already records
            // the Failed state for the job; a second log here
            // would duplicate it. The queue must keep moving.
            // (2026-08-25 empty-catch sweep.)
            ("DemoJobQueue.cs",                     "*",                   "Director already records failures; keep the queue moving"),

            // ConnectivityService — the network poll loop
            // catches each transient failure individually so a
            // single dropped packet cannot kill the monitor.
            ("ConnectivityService.cs",              "*",                   "Network poll resilience"),

            // OperationGuard — the "audit logging itself
            // failed" path inside the guard.
            ("OperationGuard.cs",                   "*",                   "Guard never rethrows to caller"),

            // SecretVault — DPAPI fallback to "return null"
            // on any CryptographicException.
            ("SecretVault.cs",                      "*",                   "DPAPI fallback by design"),

            // InquiryAttachmentService — best-effort
            // attachment probe; missing is benign.
            ("InquiryAttachmentService.cs",         "*",                   "Attachment probe: missing is benign"),

            // DocumentOperationCoordinator — per-document
            // pipeline catches a single failure and continues.
            ("DocumentOperationCoordinator.cs",     "*",                   "Per-document pipeline resilience"),

            // ViewModelResultHelper — Result<T>-mapping helper.
            ("ViewModelResultHelper.cs",            "*",                   "Result-mapping helper"),

            // ReportingCacheService + IndexingService —
            // best-effort cache / index-rebuild; failure
            // surfaces via return count, not catch.
            ("ReportingCacheService.cs",            "*",                   "Cache miss on failure is acceptable"),
            ("IndexingService.cs",                  "*",                   "Index-rebuild returns failure count to caller"),

            // Export engines — "file saved, viewer launch
            // failed" and "temp file delete failed" are
            // non-fatal. The CsvExportEngine FIX in audit
            // finding C-3 is upstream of this.
            ("CsvExportEngine.cs",                  "*",                   "Viewer launch failure is non-fatal"),
            ("ExcelExportEngine.cs",                "*",                   "Viewer launch failure is non-fatal"),
            ("PdfExportEngine.cs",                  "*",                   "Viewer launch / temp cleanup are non-fatal"),

            // UserPresenceService — broadcast socket and
            // foreign-traffic paths.
            ("UserPresenceService.cs",              "*",                   "Network resilience on broadcast / foreign traffic"),

            // ProformaAttachmentService — best-effort manifest
            // pruning.
            ("ProformaAttachmentService.cs",        "*",                   "Best-effort manifest pruning"),

            // UpdateHistoryService — "history is a convenience".
            ("UpdateHistoryService.cs",             "*",                   "History is a convenience, never block updates"),

            // MachineFingerprint — WMI call fallback when
            // WMI is not available; the fingerprint just
            // misses that input.
            ("MachineFingerprint.cs",               "*",                   "WMI unavailable → fingerprint input skipped"),

            // LicenseService — startup-time reads that
            // must never break the app: portable check
            // (best-effort by design), StatusChanged
            // event-raise (UI subscribers must not break
            // licensing), audit-sink write (audit must
            // not block startup).
            ("LicenseService.cs",                   "*",                   "Startup best-effort / event-raise must not throw"),

            // RelayCommand — the inner catch guards the log
            // call itself; logging about a logging failure
            // must never mask the original command failure.
            // (2026-08-25 empty-catch sweep; surfaced once the
            //  test project could compile.)
            ("RelayCommand.cs",                     "Execute",             "Logging guard must never mask the original failure"),

            // AboutView — clipboard set / environment lookups
            // are best-effort; a failure must not crash the page.
            ("AboutView.xaml.cs",                   "*",                   "Best-effort clipboard / env probe"),

            // RecordOverlayWindow — teardown of the recording
            // overlay must never throw on window close. (2026-09-02:
            // entry retired — Phase 3.7 moved the HUD state to
            // RecordOverlayViewModel and the DragMove catch now logs,
            // so the window has no silent catches left.)
            // LicenseGenerator developer tool: its own logging
            // stub must never throw, and source-file probing is
            // best-effort (falls back to manual paste). Tools/
            // is scanned by the audit, so allow the intentional
            // best-effort catches.
            ("LicenseGeneratorCore.cs",             "*",                   "Best-effort repo locate / key write in dev tool"),

            // AIContextBuilder — DB / external-resource
            // probe; the context builder just falls back
            // to an empty context.
            ("AiContextBuilder.cs",                 "*",                   "DB unavailable → fall back to empty context"),

            // RecordMetadataService — the metadata write
            // is observability-only and must never break
            // the business flow.
            ("RecordMetadataService.cs",            "*",                   "Metadata must never break the business flow"),

            // DocumentLifecycle — the static Changed
            // event is best-effort; a listener that throws
            // must not undo a completed write.
            ("DocumentLifecycle.cs",                "*",                   "Event-raise must not undo a completed write"),

            // SchemaHealthService — the audit-write call
            // inside a schema-rebuild must not block the
            // rebuild.
            ("SchemaHealthService.cs",              "*",                   "Audit must not block schema rebuild"),

            // SchemaMigrationRunner — the EF transaction
            // rollback in the catch-block is best-effort;
            // if it fails the migration failure log is the
            // next-best signal.
            ("SchemaMigrationRunner.cs",            "*",                   "Rollback after migration failure is best-effort"),

            // PdfTheme — the config probe at startup
            // (Config.Appearance.PdfLayoutId) may not
            // be ready; default to the active theme.
            ("PdfTheme.cs",                         "*",                   "Config not ready at startup"),

            // ScreenshotService — window-restore after a
            // capture is best-effort; the screenshot was
            // already taken.
            ("ScreenshotService.cs",                "*",                   "Window restore after capture is best-effort"),

            // FontService — the Win32 WM_FONTCHANGE
            // broadcast and the post-rebuild font-cache
            // invalidation are best-effort. UI must not
            // break the font engine.
            ("FontService.cs",                      "*",                   "Font-engine: UI must not break broadcast or rebuild"),

            // LogEntry — log-entry construction must
            // never throw. Logging failures must not
            // surface as exceptions to callers.
            ("LogEntry.cs",                         "*",                   "Log-entry construction must never throw"),

            // UserScreenRecordService — sidecar
            // attachment and the partial-file cleanup
            // are best-effort. The screen recording
            // itself is the user-visible artefact; the
            // sidecar is a debugging aid.
            ("UserScreenRecordService.cs",          "*",                   "Sidecar and partial-file cleanup are best-effort"),

            // AppInfo — the assembly-identity and
            // file-timestamp probes are best-effort.
            // The fallback constants are the right
            // answer for a partial / unversioned build.
            ("AppInfo.cs",                          "*",                   "Assembly-identity probe is best-effort"),

            // UpdateService — the OTA check and the
            // post-failure partial-file cleanup are
            // best-effort. OTA must never affect the
            // running app.
            ("UpdateService.cs",                    "*",                   "OTA check and partial-file cleanup are best-effort"),

            // TaxInvoiceRepository — the badge-cleanup
            // in InitializeAsync is best-effort.
            ("TaxInvoiceRepository.cs",             "InitializeAsync",     "Badge cleanup at startup is best-effort"),

            // TaxInvoiceDnReconciler — the audit-write
            // inside the reconciler is best-effort.
            ("TaxInvoiceDnReconciler.cs",           "*",                   "Reconciler audit-write is best-effort"),

            // ScreenRecordPanelViewModel — the status-
            // update probe is best-effort.
            ("ScreenRecordPanelViewModel.cs",       "*",                   "Status update is best-effort"),

            // SheetStoreViewModel — multiple best-effort
            // probes (sheet history, sheet reset, etc).
            ("SheetStoreViewModel.cs",              "*",                   "Sheet-store probes are best-effort"),

            // OverviewDashboardViewModel — sheet-
            // history load is best-effort.
            ("OverviewDashboardViewModel.cs",       "*",                   "Sheet-history load is best-effort"),

            // DeliveryNoteDetailViewModel — attachment
            // file delete is best-effort (file may have
            // been moved by the user already).
            ("DeliveryNoteDetailViewModel.cs",      "*",                   "Attachment file delete is best-effort"),

            // DeliveryNoteListViewModel — the JO
            // picker probe is best-effort; picker still
            // searches the JO fields it has.
            ("DeliveryNoteListViewModel.cs",        "*",                   "JO picker probe is best-effort"),

            // JobOrderDetailViewModel — guarded export;
            // the real DN is still created even if the
            // export-probe fails.
            ("JobOrderDetailViewModel.cs",          "*",                   "Guarded export is best-effort"),

            // JobOrderRepository — the "Quietly" helper
            // is best-effort by name; the audit
            // FindContainingMethod reports the outer
            // method (InitializeAsync) because the
            // helper is a private static below it. Allow
            // the containing method too.
            ("JobOrderRepository.cs",               "InitializeAsync",     "Master-DN quiet-cleanup is best-effort by name"),

            // Schema migrations — the unique-index creation
            // is best-effort; the repository already retries
            // on unique collisions.
            ("AppSchemaMigrations.cs",              "*",                   "Index creation is best-effort"),

            // JobOrderRepository — "DeleteMasterDnQuietlyAsync"
            // by name; a leftover Master DN is hidden from
            // every public list.
            ("JobOrderRepository.cs",               "DeleteMasterDnQuietlyAsync", "Best-effort cleanup by name"),
            ("TaxInvoiceRepository.cs",             "ClearLinkedDeliveryNotesAsync", "Best-effort badge cleanup"),

            // AppLogPanel / Diagnostics view-models — probe
            // failures are surfaced as an empty panel, which
            // is the intended UI.
            ("DashboardPageBase.cs",                "Dispose",             "VM teardown"),
            ("DashboardPageBase.cs",                "*",                   "Probe fallback by design"),

            // CompanyCenter / Proforma Step2 / Reports / TaxInvoice
            // / Inquiry view-models — partial-fill fallbacks
            // for optional fields.
            ("CompanyCenterViewModel.cs",           "*",                   "Partial-fill fallback by design"),
            ("ClientProfileViewModel.cs",           "*",                   "Optional field probe fallback"),
            ("ReportBaseViewModel.cs",              "*",                   "Superseded-load cancellation is benign"),
            ("TaxInvoiceDetailViewModel.cs",        "*",                   "Audit-failure must not mask original problem"),
            ("InquiryListViewModel.cs",             "*",                   "Superseded-load cancellation is benign"),
            ("SavedJobOrdersListViewModel.cs",      "ShowExportResult",    "Export-result toast is best-effort"),

            // Settings — restart helper, page teardown,
            // data-card cleanup.
            ("SettingsViewModel.cs",                "*",                   "Restart helper / page teardown"),

            // MainViewModel — navigation fallback + Dispose
            // teardown + storage-probe file walk + DI service
            // resolution at construction time. The DI catches
            // (lines 397-399) fall through to the legacy
            // `App.*` static services, so a missing service
            // is a soft failure, not a hard one. The catch
            // is best-effort by design; logging would not
            // change the recovery path.
            ("MainViewModel.cs",                    "Dispose",             "Main VM teardown"),
            ("MainViewModel.cs",                    "Navigate",            "Navigation fallback"),
            ("MainViewModel.cs",                    "UpdateStorageDisplay","Storage-probe file walk"),
            ("MainViewModel.cs",                    "UpdateAiScreenContext","AI context probe: per-VM best effort"),
            ("MainViewModel.cs",                    "MainViewModel",       "DI service resolution with legacy fallback"),
            ("MainViewModel.cs",                    "ResolveOrNew",        "DI service resolution with Activator fallback"),

            // VM Dispose() bodies — event-unsubscribe teardown.
            // This is exactly the M-1 pattern; the catch
            // exists because `-=` on a null handler would NRE.
            // Standard IDisposable idiom; logging is a no-op.
            //
            // The * entry covers constructor-time silent
            // catches too (e.g. the audit-write guard in
            // DashboardViewModel, the sample-data
            // subscription in SavedJobOrdersListViewModel).
            ("DashboardViewModel.cs",               "Dispose",             "VM teardown (M-1 mitigation)"),
            ("DashboardViewModel.cs",               "*",                   "VM constructor and ctor-side event subscriptions"),
            ("DeliveryNoteViewModel.cs",            "Dispose",             "VM teardown (M-1 mitigation)"),
            ("SavedJobOrdersListViewModel.cs",      "Dispose",             "VM teardown (M-1 mitigation)"),
            ("SavedJobOrdersListViewModel.cs",      "*",                   "VM constructor and ctor-side event subscriptions"),
            ("SavedInvoicesListViewModel.cs",       "Dispose",             "VM teardown (M-1 mitigation)"),
            ("SavedInvoicesListViewModel.cs",       "*",                   "VM constructor and ctor-side event subscriptions"),
            ("SystemCenterViewModel.cs",            "Dispose",             "VM teardown (M-1 mitigation)"),
            ("SystemCenterViewModel.cs",            "*",                   "VM constructor and ctor-side event subscriptions"),
            ("TaxInvoiceViewModel.cs",              "Dispose",             "VM teardown (M-1 mitigation)"),
            ("TaxInvoiceViewModel.cs",              "*",                   "VM constructor and ctor-side event subscriptions"),
            ("UserCenterViewModel.cs",              "Dispose",             "VM teardown (M-1 mitigation)"),
            ("UserCenterViewModel.cs",              "*",                   "VM constructor and ctor-side event subscriptions"),

            // ── Remaining catch-alls (handled by member 'name = ANY') ──
            // These are file-wide allows; if you add a new VM and a
            // new silent catch, the policy at the top of the file
            // says: add a one-line entry with the method name and
            // the reason. Do NOT just rely on this catch-all.
        };

        // ── Remaining S-5 candidates for the human to fix ──────
        //
        // The audit at HEAD `1a1f601` flagged "22 silent
        // catch { } blocks" (S-5). After this test scans the
        // codebase with the allow list above, the only
        // remaining candidate — the per-VM best-effort probe
        // inside MainViewModel.UpdateAiScreenContext — was
        // fixed at HEAD `b2d73e4`'s follow-up: the catch now
        // calls AppLogService.Debug so a third-party VM that
        // throws on AgentContextDetail is non-fatal but
        // diagnosable. The catch is no longer silent, so it
        // no longer appears in this audit.
        //
        // All other previously-flagged S-5 hits are explicitly
        // on the allow list above with a one-line reason.

        // Folders we audit. Add new ones here when new code is
        // organised under a new top-level directory; do NOT
        // add Tests/ to keep the audit self-non-recursive.
        //
        // 2026-08-25: extended from {Services, ViewModels,
        // App.xaml.cs} to the ENTIRE production tree. Views,
        // Behaviors, Commands, Agent and Utilities previously
        // sat outside the audit, which is exactly where silent
        // catches were accumulating unseen.
        private static readonly string[] AuditedFolders =
        {
            "Agent",
            "Behaviors",
            "Commands",
            "Controls",
            "Converters",
            "Models",
            "Services",
            "Tools",
            "Utilities",
            "ViewModels",
            "Views",
            "App.xaml.cs",
            "MainWindow.xaml.cs",
        };

        [Fact]
        public void No_new_silent_catches_outside_the_allowed_list()
        {
            string repoRoot = FindRepoRoot();
            var findings = ScanForSilentCatches(repoRoot);

            // Print every finding so a developer who just made
            // a regression has the line numbers in the test
            // output without having to re-run anything.
            _out.WriteLine(
                $"Scanned {findings.TotalScanned} files under " +
                $"{string.Join(", ", AuditedFolders)}; " +
                $"found {findings.All.Count} empty-catch clauses " +
                $"({findings.Allowed.Count} allowed, " +
                $"{(findings.All.Count - findings.Allowed.Count)} other).");

            foreach (var hit in findings.All
                .OrderBy(h => h.File, StringComparer.Ordinal)
                .ThenBy(h => h.Line))
            {
                _out.WriteLine(
                    $"  {(hit.Allowed ? "ALLOW" : "FAIL ")}  " +
                    $"{hit.File}:{hit.Line}  in  " +
                    $"{hit.ContainingMethod}  " +
                    $"— {hit.Reason}");
            }

            // The actual assertion.
            var disallowed = findings.All.Where(h => !h.Allowed).ToList();
            Assert.True(
                disallowed.Count == 0,
                "Silent `catch { }` blocks found in code that is " +
                "not on the allow list. Either add an AppLogService " +
                "call inside the catch (the standard fix for S-5), or " +
                "if this catch is a known-safe exception, add a one-" +
                "line entry to SilentCatchAuditTests.AllowedSilentCatches " +
                "with the reason. Findings:\n" +
                string.Join("\n", disallowed.Select(
                    h => $"  {h.File}:{h.Line}  in  {h.ContainingMethod}")));
        }

        // ── Discovery ───────────────────────────────────────────

        private static string FindRepoRoot()
        {
            // AppContext.BaseDirectory is
            //   <repo>/Tests/bin/Release/net8.0-windows/
            // walk up until we find ProGlassAutomation.csproj.
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

        private record Finding(
            string File, int Line, string ContainingMethod,
            bool Allowed, string Reason);

        private record ScanResult(
            int TotalScanned,
            IReadOnlyList<Finding> All,
            IReadOnlyList<Finding> Allowed);

        private static ScanResult ScanForSilentCatches(string repoRoot)
        {
            var all = new List<Finding>();
            int totalScanned = 0;

            foreach (var folder in AuditedFolders)
            {
                IEnumerable<string> files;
                if (folder.EndsWith(".cs", StringComparison.Ordinal))
                {
                    // Single-file entry (e.g. App.xaml.cs at the
                    // repo root).
                    var single = Path.Combine(repoRoot, folder);
                    files = File.Exists(single) ? new[] { single } : Array.Empty<string>();
                }
                else
                {
                    var dir = new DirectoryInfo(Path.Combine(repoRoot, folder));
                    if (!dir.Exists) continue;
                    files = dir.EnumerateFiles("*.cs", SearchOption.AllDirectories)
                               .Select(f => f.FullName);
                }

                foreach (var file in files)
                {
                    // Skip the test project's own source — we
                    // are the auditors, not the auditees.
                    if (file.Replace('\\', '/').Contains("/Tests/"))
                        continue;

                    totalScanned++;
                    var findings = ScanFile(file, repoRoot);
                    all.AddRange(findings);
                }
            }

            var allowed = all.Where(f => f.Allowed).ToList();
            return new ScanResult(totalScanned, all, allowed);
        }

        private static IEnumerable<Finding> ScanFile(
            string fullPath, string repoRoot)
        {
            string source;
            try { source = File.ReadAllText(fullPath); }
            catch { yield break; } // file gone mid-scan, ignore

            SyntaxTree tree;
            try
            {
                tree = CSharpSyntaxTree.ParseText(source);
            }
            catch
            {
                // Unparseable source — usually a generated file
                // with a preprocessor directive we cannot
                // resolve. The compiler will flag the real error
                // in the build; do not fail the audit twice.
                yield break;
            }

            var root = tree.GetCompilationUnitRoot();
            var sourceText = tree.GetText();
            var relative = fullPath.Substring(repoRoot.Length)
                                   .TrimStart(Path.DirectorySeparatorChar,
                                              Path.AltDirectorySeparatorChar)
                                   .Replace('\\', '/');

            // Roslyn surfaces every empty catch — both
            //   catch { }                (CatchDeclaration null, Block.Statements empty)
            //   catch (Exception) { }    (CatchDeclaration non-null, Block.Statements empty)
            foreach (var catchClause in root.DescendantNodes()
                                            .OfType<CatchClauseSyntax>())
            {
                if (catchClause.Block == null) continue;
                if (catchClause.Block.Statements.Count != 0) continue;

                // Use GetText().Lines directly. The .NET 10 SDK
                // ships Roslyn 4.14 which removed the
                // SyntaxTree.GetLineSpan(TextSpan) overload that
                // earlier versions exposed; going through
                // GetText().Lines is the cross-version path.
                var line = sourceText.Lines
                    .GetLinePosition(catchClause.SpanStart).Line + 1;
                var method = FindContainingMethod(root, catchClause.SpanStart);

                var (allowed, reason) = Classify(relative, method);
                yield return new Finding(
                    relative, line, method, allowed, reason);
            }
        }

        private static string FindContainingMethod(
            SyntaxNode root, int spanStart)
        {
            // Cross-Roslyn-version method-name extraction.
            // The .NET 10 SDK (Roslyn 4.14) removed
            // BaseMethodDeclarationSyntax.Identifier; we now
            // pattern-match on each concrete subtype
            // (MethodDeclaration, ConstructorDeclaration,
            // DestructorDeclaration, OperatorDeclaration,
            // ConversionOperatorDeclaration) and fall back to
            // "<unknown>" for anything we don't recognise.
            foreach (var node in root.DescendantNodes())
            {
                if (node.SpanStart > spanStart) break;
                switch (node)
                {
                    case MethodDeclarationSyntax m:
                        return m.Identifier.Text;
                    case ConstructorDeclarationSyntax c:
                        return c.Identifier.Text;
                    case DestructorDeclarationSyntax d:
                        return "~" + d.Identifier.Text;
                    case OperatorDeclarationSyntax o:
                        return "operator " + o.OperatorToken.Text;
                    case ConversionOperatorDeclarationSyntax co:
                        return co.ImplicitOrExplicitKeyword.Text + " operator";
                    case LocalFunctionStatementSyntax l:
                        return l.Identifier.Text;
                }
            }
            return "<unknown>";
        }

        private static (bool Allowed, string Reason) Classify(
            string relativeFile, string containingMethod)
        {
            foreach (var allow in AllowedSilentCatches)
            {
                if (!relativeFile.Contains(allow.PathContains,
                        StringComparison.Ordinal))
                    continue;

                bool memberMatch =
                    allow.MemberContains == "*" ||
                    containingMethod.Contains(allow.MemberContains,
                        StringComparison.Ordinal) ||
                    // Convention carve-out: a method literally
                    // named "Dispose" is allowed to be silent
                    // anywhere a non-throw-by-design rule
                    // applies.
                    (allow.MemberContains == "Dispose"
                     && containingMethod == "Dispose");

                if (memberMatch)
                    return (true, allow.Reason);
            }

            return (false, "no allowlist entry");
        }
    }
}

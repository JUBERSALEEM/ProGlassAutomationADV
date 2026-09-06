// ═══════════════════════════════════════════════════════════════════════
//  SOURCE PINS — Sheet movement reversal (undo a mistaken Use / Buy)
//  Added 2026-09-06.
//
//  Owner report: "if we mistakenly used sheet include add option to undo
//  that action or edit properly".
//
//  THE CONSTRAINT THAT SHAPED THE FIX
//  SheetHistory documents itself as an immutable audit record: the sheet
//  really was consumed for that customer on that day, and the record
//  "must not silently change or vanish". So undo may NOT delete the row
//  and may NOT rewrite its quantity. It writes a COMPENSATING (contra)
//  entry instead:
//
//      #41  Used      qty  10   IsReversed = 1
//      #57  Reversed  qty -10   ReversesHistoryId = 41
//
//  Stock is restored by the second movement; the ledger still records
//  that the mistake happened and who corrected it, and any SUM over
//  QuantityChanged nets to the truth with no special-casing.
//
//  These are SOURCE pins, not runtime tests: Tests/** is Compile Remove'd
//  in the csproj, so they document and lock the contract without entering
//  the WPF build. Each asserts on the shipped source text, which is what
//  a future edit would have to break.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.IO;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    public class SheetMovementReversalSourceTests
    {
        private static string RepoRoot()
        {
            var dir = AppContext.BaseDirectory;
            while (dir != null && !File.Exists(Path.Combine(dir, "ProGlassAutomation.csproj")))
                dir = Path.GetDirectoryName(dir);
            return dir ?? throw new InvalidOperationException("repo root not found");
        }

        private static string Read(params string[] parts) =>
            File.ReadAllText(Path.Combine(RepoRoot(), Path.Combine(parts)));

        // ── 1. The audit row is never destroyed ────────────────────────
        //
        // The whole design rests on this. If a future edit "simplifies"
        // undo into a delete, the immutability contract is gone and this
        // pin fails.
        [Fact]
        public void Reversal_never_deletes_or_removes_the_original_history_row()
        {
            var repo = Read("Services", "SheetStore", "SheetStoreHistory",
                            "SheetHistoryRepository.cs");

            Assert.DoesNotContain("SheetHistories.Remove", repo);
            Assert.DoesNotContain("RemoveRange", repo);

            // The original is FLAGGED, not erased.
            Assert.Contains("original.IsReversed = true;", repo);
        }

        // ── 2. The compensating row is a true contra entry ─────────────
        [Fact]
        public void Compensating_entry_is_negative_and_links_back_to_the_original()
        {
            var repo = Read("Services", "SheetStore", "SheetStoreHistory",
                            "SheetHistoryRepository.cs");

            Assert.Contains("Action = \"Reversed\"", repo);
            Assert.Contains("QuantityChanged = -qty", repo);
            Assert.Contains("ReversesHistoryId = original.Id", repo);
        }

        // ── 3. Counters are restored, and never driven negative ────────
        //
        // Undoing a Use decrements Used (Balance = Stock - Used, so that
        // is what "restores stock"). Undoing a Buy decrements Stock, but
        // only if the sheets have not already been consumed — otherwise
        // Used > Stock and the ledger is corrupt. Refuse, never clamp.
        [Fact]
        public void Restore_refuses_rather_than_clamps_when_it_would_corrupt_the_ledger()
        {
            var repo = Read("Services", "SheetStore", "SheetStoreHistory",
                            "SheetHistoryRepository.cs");

            Assert.Contains("sheet.Used -= qty;", repo);
            Assert.Contains("sheet.Stock -= qty;", repo);

            Assert.Contains("if (sheet.Used < qty)", repo);
            Assert.Contains("if (sheet.Stock - qty < sheet.Used)", repo);
            Assert.Contains("InvalidOperationException", repo);
        }

        // ── 4. Guards are re-validated server-side ─────────────────────
        //
        // The button's Visibility uses CanReverse, but a stale grid must
        // not be able to double-undo. The repository re-checks every rule
        // itself.
        [Fact]
        public void Repository_revalidates_the_reversibility_rules_itself()
        {
            var repo = Read("Services", "SheetStore", "SheetStoreHistory",
                            "SheetHistoryRepository.cs");

            Assert.Contains("original.IsReversed", repo);
            Assert.Contains("original.ReversesHistoryId.HasValue", repo);
            Assert.Contains("original.Action != \"Used\"", repo);
            Assert.Contains("original.Action != \"Bought\"", repo);

            // Write access is gated like every other mutating repo call.
            Assert.Contains("ReadOnlyGate.Demand(\"undo a sheet movement\")", repo);
        }

        // ── 5. Restore + both history writes commit atomically ─────────
        //
        // One DbContext, one SaveChanges. A crash mid-undo must not leave
        // stock restored with no ledger entry explaining it.
        [Fact]
        public void Reversal_commits_counter_and_ledger_in_a_single_save()
        {
            var repo = Read("Services", "SheetStore", "SheetStoreHistory",
                            "SheetHistoryRepository.cs");

            int start = repo.IndexOf("public async Task<SheetHistory?> ReverseMovementAsync",
                                     StringComparison.Ordinal);
            Assert.True(start > 0, "ReverseMovementAsync not found");
            string body = repo.Substring(start);

            // Exactly one context and one save inside the method.
            int contexts = CountOf(body, "new AppDbContext()");
            int saves = CountOf(body, "SaveWithRetryAsync");
            Assert.Equal(1, contexts);
            Assert.Equal(1, saves);
        }

        // ── 6. The model exposes the rule the UI binds to ──────────────
        //
        // CanReverse is the single source of truth for "may this row be
        // undone" — used by the button's Visibility, the command's
        // CanExecute, and mirrored by the repository guard.
        [Fact]
        public void Model_exposes_CanReverse_covering_movements_only_and_once_only()
        {
            var model = Read("Models", "SheetStore", "SheetStoreHistory", "SheetHistory.cs");

            Assert.Contains("public bool CanReverse", model);
            Assert.Contains("!IsReversed", model);
            Assert.Contains("!IsReversalEntry", model);
            Assert.Contains("Action == \"Used\"", model);
            Assert.Contains("Action == \"Bought\"", model);

            // The new Action value needs its own colour or the badge
            // renders in the generic gray "unknown" swatch.
            Assert.Contains("\"Reversed\" =>", model);
        }

        // ── 7. Legacy databases keep working ───────────────────────────
        //
        // EnsureCreated() never alters an existing database, so the two
        // new columns need a migration. They must be NULL / DEFAULT 0 —
        // migration 1001 documents what a non-nullable column does to
        // rows written by an older build ("The data is NULL at ordinal 2").
        [Fact]
        public void Reversal_columns_ship_as_an_idempotent_nullable_migration()
        {
            var mig = Read("Services", "Database", "SchemaMigrations",
                           "AppSchemaMigrations.cs");

            Assert.Contains("1002", mig);
            Assert.Contains("\"ReversesHistoryId\", \"INTEGER NULL\"", mig);
            Assert.Contains("\"IsReversed\",        \"INTEGER NOT NULL DEFAULT 0\"", mig);

            // Idempotent helpers only — never a bare ALTER TABLE.
            Assert.Contains("AddColumnIfMissing(connection, \"SheetHistories\", \"ReversesHistoryId\"", mig);
        }

        // ── 8. The grid behind History reloads after an undo ───────────
        //
        // Undo restores stock while the modal is open, so the Sheet Store
        // list underneath is instantly stale.
        [Fact]
        public void Sheet_store_reloads_when_a_movement_is_reversed()
        {
            var vm = Read("ViewModels", "SheetStore", "SheetStoreViewModel.Part2.cs");

            Assert.Contains("MovementReversed += OnReversed", vm);
            Assert.Contains("MovementReversed -= OnReversed", vm);

            var history = Read("ViewModels", "SheetStore", "SheetStoreHistory",
                               "HistoryViewModel.cs");
            Assert.Contains("public event Action? MovementReversed", history);
            Assert.Contains("MovementReversed?.Invoke()", history);
        }

        // ── 9. Undo is permission-gated and confirmed ──────────────────
        [Fact]
        public void Undo_requires_permission_and_an_explicit_confirmation()
        {
            var vm = Read("ViewModels", "SheetStore", "SheetStoreHistory",
                          "HistoryViewModel.cs");

            Assert.Contains("AppPermissions.SheetStoreEdit", vm);
            Assert.Contains("MessageBoxButton.YesNo", vm);

            // Destructive-looking action must not default to Yes.
            Assert.Contains("MessageBoxResult.No", vm);

            // A refusal from the repository is a business answer, shown
            // verbatim — not swallowed into a generic crash dialog.
            Assert.Contains("catch (InvalidOperationException ex)", vm);
        }

        private static int CountOf(string haystack, string needle)
        {
            int n = 0, i = 0;
            while ((i = haystack.IndexOf(needle, i, StringComparison.Ordinal)) >= 0)
            {
                n++;
                i += needle.Length;
            }
            return n;
        }
    }
}

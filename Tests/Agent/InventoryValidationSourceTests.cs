// ═══════════════════════════════════════════════════════════════════════
//  SOURCE PINS — Inventory blocking validation + slide-out editor
//  Added 2026-09-06.
//
//  Owner report: "if mistakenly enter 0 total buy it still saving when an
//  industry erp will not let it happen" and "sometimes full data is not
//  visible what we are editing".
//
//  THE BUG THESE LOCK DOWN
//
//  BuyDialogViewModel declared `CanConfirm => Quantity > 0` and never
//  wired it into the command. CONFIRM stayed live at 0; BuyAsync then
//  re-checked, fell through, and returned SILENTLY — dialog closed, no
//  stock, no message. A failure that looks like success.
//
//  Compounding it, an int bound with UpdateSourceTrigger=PropertyChanged
//  cannot observe an empty box: conversion fails, the setter never runs,
//  the stale value survives behind a blank control. Hence raw-text
//  binding with explicit parsing everywhere in this module.
//
//  Tests/** is Compile Remove'd in the csproj, so these document and lock
//  the contract without entering the WPF build.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.IO;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    public class InventoryValidationSourceTests
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

        // ── 1. The reported bug: confirm is gated ──────────────────────
        [Fact]
        public void Buy_confirm_command_is_gated_on_validity()
        {
            var vm = Read("ViewModels", "SheetStore", "SheetStoreBuy", "BuyDialogViewModel.cs");

            Assert.Contains("_ => CanConfirm", vm);
            Assert.Contains("public bool CanConfirm => IsValid", vm);
        }

        // ── 2. Numeric entry binds RAW TEXT, never the int ─────────────
        //
        // This is the fix that makes an empty box observable at all.
        [Fact]
        public void Quantity_fields_bind_text_not_the_parsed_number()
        {
            var buyView = Read("Views", "SheetStore", "SheetStoreBuy", "BuyDialogView.xaml");
            var useView = Read("Views", "SheetStore", "SheetStoreUse", "UseDialogView.xaml");

            Assert.Contains("Binding QuantityText", buyView);
            Assert.Contains("Binding QuantityText", useView);

            // The old int binding must be gone from both.
            Assert.DoesNotContain("Binding Quantity,", buyView);
            Assert.DoesNotContain("Binding Quantity,", useView);
        }

        // ── 3. Zero is refused by name, and never silently rewritten ───
        [Fact]
        public void Zero_is_rejected_with_its_own_message_and_never_clamped()
        {
            var base_ = Read("ViewModels", "Validation", "ValidatableViewModelBase.cs");

            Assert.Contains("0 is not a valid", base_);

            // The old silent clamp must not come back in either dialog.
            // Checked against CODE only — the header comments in both files
            // quote the old clamp while explaining why it was removed, so a
            // naive substring search matches the documentation and fails.
            var buy = StripComments(
                Read("ViewModels", "SheetStore", "SheetStoreBuy", "BuyDialogViewModel.cs"));
            var use = StripComments(
                Read("ViewModels", "SheetStore", "SheetStoreUse", "UseDialogViewModel.cs"));

            Assert.DoesNotContain("value < 1) value = 1", buy);
            Assert.DoesNotContain("value < 1) value = 1", use);
            Assert.DoesNotContain("value > Sheet.Balance) value = Sheet.Balance", use);
        }

        // ── 4. Empty and non-numeric are distinguished ─────────────────
        [Fact]
        public void Empty_and_non_numeric_get_their_own_messages()
        {
            var b = Read("ViewModels", "Validation", "ValidatableViewModelBase.cs");

            Assert.Contains("Enter a", b);                      // empty
            Assert.Contains("must be a whole number", b);        // non-numeric
            Assert.Contains("cannot be negative", b);            // negative
        }

        // ── 5. Consumption is bounded by real stock, with the number ───
        [Fact]
        public void Use_bounds_quantity_by_balance_and_names_the_limit()
        {
            var vm = Read("ViewModels", "SheetStore", "SheetStoreUse", "UseDialogViewModel.cs");

            Assert.Contains("max: Sheet.Balance", vm);
            Assert.Contains("Only {Sheet.Balance} sheet(s) available.", vm);
        }

        // ── 6. The silent-failure path is gone ─────────────────────────
        //
        // Reaching the handler with invalid input now REPORTS instead of
        // quietly returning.
        [Fact]
        public void Handlers_report_instead_of_silently_dropping_bad_input()
        {
            var vm = Read("ViewModels", "SheetStore", "SheetStoreViewModel.Part2.cs");

            Assert.Contains("Purchase not recorded", vm);
            Assert.Contains("Consumption not recorded", vm);
            Assert.Contains("!vm.IsValid", vm);
        }

        // ── 7. Add/Edit blocks save and validates every field at once ──
        [Fact]
        public void AddEdit_gates_save_and_reports_all_fields_together()
        {
            var vm = Read("ViewModels", "SheetStore", "AddEditSheetViewModel.cs");

            Assert.Contains("new RelayCommand(_ => Save(), _ => IsValid)", vm);

            // Re-validates inside Save too — CanExecute is an affordance,
            // not a boundary.
            Assert.Contains("Revalidate();", vm);
            Assert.Contains("if (!IsValid) return;", vm);

            // Not fail-fast: no early `return` between field parses, so
            // every field reports at once.
            Assert.Contains("ParseRequiredText(Category", vm);
            Assert.Contains("ParseQuantity(ThicknessText", vm);
            Assert.Contains("ParseQuantity(WidthText", vm);
            Assert.Contains("ParseQuantity(HeightText", vm);
        }

        // ── 8. Editing cannot strand recorded consumption ──────────────
        [Fact]
        public void Stock_cannot_be_edited_below_what_is_already_used()
        {
            var vm = Read("ViewModels", "SheetStore", "AddEditSheetViewModel.cs");

            Assert.Contains("value < _existing.Used", vm);
            Assert.Contains("already recorded as used", vm);
        }

        // ── 9. Opening stock of 0 stays legal ──────────────────────────
        //
        // You routinely create an item before its delivery lands. The
        // quantity rule rejects 0 by design, so opening stock must NOT go
        // through it.
        [Fact]
        public void Opening_stock_of_zero_is_still_allowed()
        {
            var vm = Read("ViewModels", "SheetStore", "AddEditSheetViewModel.cs");

            Assert.Contains("ParseOpeningStock", vm);
            Assert.Contains("0 is allowed", vm);
        }

        // ── 10. Below-cost pricing warns, never blocks ─────────────────
        [Fact]
        public void Selling_below_cost_warns_but_does_not_block()
        {
            var vm = Read("ViewModels", "SheetStore", "AddEditSheetViewModel.cs");

            Assert.Contains("HasMarginWarning", vm);

            // Must not participate in IsValid — clearance and sample
            // stock genuinely trade at a loss.
            Assert.DoesNotContain("SetError(nameof(SellPriceText), MarginWarning)", vm);
        }

        // ── 11. Editor panel shares ONE persistence path with the dialog ─
        //
        // Two code paths writing the inventory ledger would drift.
        [Fact]
        public void Editor_panel_reuses_the_dialog_persistence_methods()
        {
            var editor = Read("ViewModels", "SheetStore", "SheetStoreViewModel.Editor.cs");
            var part1 = Read("ViewModels", "SheetStore", "SheetStoreViewModel.Part1.cs");

            Assert.Contains("PersistNewSheetAsync", editor);
            Assert.Contains("PersistEditedSheetAsync", editor);

            Assert.Contains("internal async Task PersistNewSheetAsync", part1);
            Assert.Contains("internal async Task PersistEditedSheetAsync", part1);
        }

        // ── 12. The audit diff snapshots BEFORE the dialog mutates ─────
        //
        // AddEditSheetViewModel sets `Result = _existing`, i.e. it mutates
        // the instance it was handed. Reading "old" values after
        // ShowDialog returns yields the NEW values and an empty diff.
        [Fact]
        public void Edit_diff_snapshots_original_values_before_mutation()
        {
            var part1 = Read("ViewModels", "SheetStore", "SheetStoreViewModel.Part1.cs");

            int snap = part1.IndexOf("var before = SnapshotForDiff(sheet);", StringComparison.Ordinal);
            int show = part1.IndexOf("var vm = new AddEditSheetViewModel(sheet);", StringComparison.Ordinal);

            Assert.True(snap > 0, "snapshot call not found");
            Assert.True(show > 0, "dialog construction not found");
            Assert.True(snap < show,
                "the pre-edit snapshot must be taken BEFORE the form mutates the instance");
        }

        // ── 13. The editor edits a COPY, never the grid's live row ─────
        [Fact]
        public void Editor_panel_binds_a_clone_so_cancel_leaves_no_trace()
        {
            var editor = Read("ViewModels", "SheetStore", "SheetStoreViewModel.Editor.cs");

            Assert.Contains("Clone(sheet)", editor);
            Assert.Contains("Used = s.Used", editor);   // needed by the
                                                        // "stock >= used" rule
        }

        // ── 14. Only one panel may own the shared column ───────────────
        [Fact]
        public void Editor_and_stats_panels_are_mutually_exclusive()
        {
            var editor = Read("ViewModels", "SheetStore", "SheetStoreViewModel.Editor.cs");
            Assert.Contains("IsRowSelected && !IsEditorOpen", editor);

            var code = Read("Views", "SheetStore", "SheetStoreView.xaml.cs");
            Assert.Contains("IsEditorOpen", code);
            Assert.Contains("SyncRightColumn", code);
        }

        // ── 15. Buy/Use stay modal; only the long form became a panel ──
        [Fact]
        public void Buy_and_use_remain_dialogs()
        {
            var vm = Read("ViewModels", "SheetStore", "SheetStoreViewModel.Part2.cs");

            Assert.Contains("ShowDialog<BuyDialogView>", vm);
            Assert.Contains("ShowDialog<UseDialogView>", vm);
        }

        /// <summary>
        /// Drops // line comments so a pin cannot be satisfied — or
        /// broken — by prose that merely QUOTES the code it forbids.
        /// </summary>
        private static string StripComments(string source)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var line in source.Split('\n'))
            {
                int i = line.IndexOf("//", StringComparison.Ordinal);
                sb.AppendLine(i >= 0 ? line.Substring(0, i) : line);
            }
            return sb.ToString();
        }
    }
}

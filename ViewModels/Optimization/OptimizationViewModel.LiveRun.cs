// ═══════════════════════════════════════════════════════════════════════
//  FILE: ViewModels/Optimization/OptimizationViewModel.LiveRun.cs
//  PROJECT: ProGlassAutomation
//
//  WHY THIS EXISTS
//  ---------------------------------------------------------------------
//  Step 6 can read SHEET QTY and optimization % from a finished run, but
//  until now the only way to produce one was to leave the proforma, open the
//  OPTIMIZATION tab, tick a Job Order, import its Step 4 sheets and run. A
//  quote still being edited has no confirmed Job Order at all, so the feed
//  stayed empty and Step 6 kept falling back to hand-typed numbers — which
//  reads as "read from optimization does nothing".
//
//  This gives Step 5 its own QUICK / OPTIMIZE buttons: the run is driven from
//  the LIVE grid, published to the same feed, and stamped with the proforma it
//  belongs to.
//
//  DELIBERATELY A NEW PARTIAL, NOT A REFACTOR
//  The Job Order path in OptimizationViewModel.Jobs.cs works and is covered by
//  the benchmark pins. Rather than extract a shared builder out of it — which
//  would rewrite a hundred lines of load-bearing code that cannot be compiled
//  here — this mirrors that derivation for a live invoice and says so at each
//  step. The mapping it must not drift from is named in the comments.
//
//  WHAT IS REUSED RATHER THAN REIMPLEMENTED
//  ProductionDistributionEngine.Distribute / ResolveBuildType /
//  ResolvePaneOccurrences (the same pane routing and per-pane composition the
//  production floor uses: W1/H1 = outer, W2/H2 = inner), NewPieceRow,
//  StockEntryVm, RefreshDerived and Run — so grouping, packing, costing,
//  publishing and persistence are all the existing, tested code paths.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ProGlassAutomation.Models.Production;
using ProGlassAutomation.Models.ProformaInvoice.SavedInvoice;
using ProGlassAutomation.Services.Optimization;
using ProGlassAutomation.Services.Production;
using ProGlassAutomation.Services.ProformaInvoice;

namespace ProGlassAutomation.ViewModels.Optimization
{
    public partial class OptimizationViewModel
    {
        /// <summary>
        /// Identity of the document a run is being produced for — the proforma
        /// id from <c>ActiveInvoiceContext</c> when Step 5 drives the run.
        /// </summary>
        /// <remarks>
        /// Empty for a run driven from the OPTIMIZATION tab, which works off
        /// confirmed Job Orders and has no single proforma identity. An empty
        /// stamp means "unstamped", and <see cref="Services.Optimization.OptimizationResultFeed.IsForDocument"/>
        /// accepts those exactly as before, so nothing that worked stops working.
        ///
        /// Set BEFORE the run: both the live publish and the persisted
        /// latest-run row read it when the run finishes.
        /// </remarks>
        public string DocumentKey { get; set; } = string.Empty;

        /// <summary>
        /// Step 4 material must be valid before Distribute / Run / TryBuildInputs.
        /// Unresolved or invalid identity is an invalid input state, not a leftover price.
        /// </summary>
        public bool TryAuthorizeJobInvoiceForOptimization(
            ProformaInvoiceSnapshot? invoice, out string message)
        {
            var verdict = Step4MaterialIntegrity.EvaluateSpecGroups(invoice?.SpecGroups);
            if (verdict.IsValid)
            {
                message = string.Empty;
                return true;
            }

            message = "Cannot optimize: " + verdict.Reason;
            return false;
        }

        /// <summary>Effort the Step 5 QUICK button runs at.</summary>
        private const string Step5QuickEffort = "1";

        /// <summary>Effort the Step 5 OPTIMIZE button runs at.</summary>
        private const string Step5OptimizeEffort = "5";

        /// <summary>
        /// Builds the cutting list and stock pool straight from the live Step 5
        /// grid and starts a run, so Step 6 can read real figures without the
        /// quote ever having been confirmed as a Job Order.
        /// </summary>
        /// <param name="specGroups">Live Step 5 spec groups, snapshot form. The
        /// caller builds these because it owns the grid; sheet sizes and prices
        /// must be included or there is no rack to cut against.</param>
        /// <param name="stage">"QUICK" or "OPTIMIZE".</param>
        /// <param name="message">Operator-facing outcome, suitable for a status
        /// line. Set on every return path, success included.</param>
        /// <returns>True when a run was actually started.</returns>
        public bool RunFromStep5(
            IReadOnlyList<SpecGroupSnapshot>? specGroups, string stage, out string message)
        {
            message = string.Empty;

            // ── DIAGNOSTIC LOGGING (Rule 18) ────────────────────────────────
            try
            {
                Services.Logging.AppLogService.Info(
                    $"[OPT-STEP5] RunFromStep5 requested stage={stage} doc={DocumentKey} groups={specGroups?.Count ?? 0}",
                    new Services.Logging.LogContext { Module = "Optimization", Operation = "RunFromStep5" });
            }
            catch { }

            if (IsRunning)
            {
                message = "An optimization run is already in progress — wait for it to finish.";
                return false;
            }

            var groups = (specGroups ?? Array.Empty<SpecGroupSnapshot>())
                .Where(g => g != null)
                .OrderBy(g => g!.GroupNumber)
                .ToList();

            if (groups.Count == 0)
            {
                message = "Step 5 has no spec groups yet — add a spec and enter sizes first.";
                return false;
            }

            if (!TryAuthorizeJobInvoiceForOptimization(
                    new ProformaInvoiceSnapshot { SpecGroups = groups },
                    out message))
            {
                // JO Run() still refuses unresolved material. Step 5 live run
                // still starts when the grid has sizes and Step 4 left sheet
                // W×H — MaterialIsValid is often null on restored drafts.
                bool hasCuttable = groups.Any(g => g.ItemRows != null && g.ItemRows.Any(r =>
                    r != null && r.Qty > 0
                    && ((r.W1 > 0 && r.H1 > 0) || (r.W2 > 0 && r.H2 > 0))));
                bool hasSheet = groups.Any(g =>
                    (g.OuterSheetWidthMm > 0 && g.OuterSheetHeightMm > 0)
                    || (g.InnerSheetWidthMm > 0 && g.InnerSheetHeightMm > 0));
                if (!hasCuttable || !hasSheet)
                    return false;
                message = string.Empty;
            }

            // A minimal invoice snapshot: Distribute only needs SpecGroups, and
            // every other field it reads (Company / Client / Project /
            // OtherCharges) is null-guarded and only feeds plan metadata.
            var invoice = new ProformaInvoiceSnapshot();
            invoice.SpecGroups.AddRange(groups);
            if (!string.IsNullOrWhiteSpace(DocumentKey) && invoice.Company != null)
                invoice.Company.ProformaId = DocumentKey;

            var plan = ProductionDistributionEngine.Distribute(DocumentKey, invoice);

            try
            {
                Services.Logging.AppLogService.Info(
                    $"[OPT-STEP5] Distribute result: {plan.Items.Count} items, {plan.Warnings.Count} warnings, doc={DocumentKey}",
                    new Services.Logging.LogContext { Module = "Optimization", Operation = "RunFromStep5" });
            }
            catch { }

            // Same filter as the Job Order path: only cutting-stage items with
            // real dimensions become pieces.
            var cut = plan.Items
                .Where(i => i != null && i.Stage == ProductionStage.Cutting
                    && i.WidthMm > 0 && i.HeightMm > 0)
                .ToList();

            // Phase 3: build the new list FIRST and clear the current one
            // only after it validates. The old code cleared Pieces/Stocks up
            // front — when Run() then refused the inputs, the operator was
            // left with an empty module and a status message.
            var newPieces = new List<PieceEntryVm>();
            var newStocks = new List<StockEntryVm>();

            int piecesAdded = 0;
            int racksAdded = 0;
            var rackSeen = new HashSet<(int W, int H, string C)>();

            // How many racks ended up carrying a real inventory count rather than
            // the "effectively unlimited" default. Announced below; applying a
            // balance silently is how an operator ends up with a refused group and
            // no idea the number came from the Sheet Store rather than from them.
            int balancesApplied = 0;
            var compositions = new List<string>();
            // ── sheet-size provenance (2026-09-17) ────────────────────────────────────
            // Step 4 is the AUTHORITY for sheet size, and for SGU / DGU / LAM it
            // supplies one. Two builds cannot: DGU+LAM's Step 4 panel has no
            // sheet-size fields at all (ApplyStep4PaneAndSheetMetadata writes 0),
            // and a legacy or malformed build type lands in its default branch,
            // which also writes 0. Until now those groups contributed no rack, so
            // the run either refused outright or — worse, and the bug actually
            // reported — cut their panes against whatever rack another spec
            // happened to bring, i.e. the WRONG sheet size. wantedByGroup is what
            // needs cutting, rackedKeys is what Step 4 already covered; the gap is
            // cross-checked against the Sheet Store after the loop.
            var wantedByGroup = new Dictionary<int, HashSet<string>>();
            var rackedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var sheetNotes = new List<string>();
            // One identity per (spec group, composition) so a rack is matched to
            // the parts that actually cut from it, not to the group as a whole.
            string compKey(int groupNumber, string? composition) =>
                groupNumber.ToString(CultureInfo.InvariantCulture) + "|"
                + (composition ?? string.Empty).Trim();

            foreach (var group in groups)
            {
                // Build type and per-pane composition come from the SAME engine
                // the Job Order path uses, so a Step 5 run and a Job Order run
                // over identical data produce identical cutting groups — and
                // therefore identical pane identity for Step 6 to join on.
                string buildType = ProductionDistributionEngine.ResolveBuildType(group, invoice);
                var occurrences = ProductionDistributionEngine.ResolvePaneOccurrences(buildType, group);

                string specLabel = "Spec " +
                    group.GroupNumber.ToString(CultureInfo.InvariantCulture);

                // Mirrors the local function in OptimizationViewModel.Jobs.cs:
                // a rack is tagged with the composition of the pane that cuts
                // from it, so pieces route to their own rack instead of every
                // pane competing for every sheet.
                string ComposeForSlot(PaneSlot slot)
                {
                    var o = occurrences.FirstOrDefault(x => x.Slot == slot);
                    if (o == null) return string.Empty;
                    // Phase 2: canonical key — rack tag must match the
                    // cutting list rows byte-for-byte.
                    return CompositionKey.Normalize(
                        string.IsNullOrWhiteSpace(o.Composition)
                            ? (group.SelectedSpecName ?? string.Empty)
                            : o.Composition);
                }

                string ComposeForPosition(string position)
                {
                    var o = occurrences.FirstOrDefault(x =>
                        string.Equals(x.Position, position, StringComparison.OrdinalIgnoreCase));
                    if (o == null) return string.Empty;
                    return CompositionKey.Normalize(
                        string.IsNullOrWhiteSpace(o.Composition)
                            ? (group.SelectedSpecName ?? string.Empty)
                            : o.Composition);
                }

                foreach (var occ in occurrences)
                {
                    var paneItems = cut
                        .Where(i => i.SpecGroupNumber == group.GroupNumber
                            && string.Equals(i.Position, occ.Position,
                                StringComparison.OrdinalIgnoreCase))
                        .ToList();
                    if (paneItems.Count == 0) continue;

                    // Phase 2: canonical composition (same key as the Job
                    // Order import path) — a Step 5 run and a Job Order run
                    // over identical data must produce identical groups.
                    string composition = CompositionKey.Normalize(
                        string.IsNullOrWhiteSpace(occ.Composition)
                            ? group.SelectedSpecName ?? string.Empty
                            : occ.Composition);

                    if (!string.IsNullOrWhiteSpace(composition)
                        && !compositions.Contains(composition, StringComparer.OrdinalIgnoreCase))
                    {
                        compositions.Add(composition);
                    }

                    if (!string.IsNullOrWhiteSpace(composition))
                    {
                        if (!wantedByGroup.TryGetValue(group.GroupNumber, out var wanted))
                        {
                            wanted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                            wantedByGroup[group.GroupNumber] = wanted;
                        }
                        wanted.Add(composition);
                    }

                    // Merge identical sizes so one row carries the total qty,
                    // and keep the Step 5 glass refs as the piece label so the
                    // 2D view and cut plan stay traceable back to the grid.
                    foreach (var sizeGroup in paneItems
                        .GroupBy(i => new { i.WidthMm, i.HeightMm })
                        .OrderBy(g => g.Key.WidthMm).ThenBy(g => g.Key.HeightMm))
                    {
                        int qty = sizeGroup.Sum(i => i.Qty);
                        if (qty <= 0) continue;

                        string label = string.Join(", ",
                            sizeGroup.Select(i => i.GlassRef)
                                     .Where(r => !string.IsNullOrWhiteSpace(r))
                                     .Distinct());

                        var row = NewPieceRow(
                            composition,
                            string.IsNullOrWhiteSpace(label) ? "—" : label,
                            sizeGroup.Key.WidthMm.ToString(CultureInfo.InvariantCulture),
                            sizeGroup.Key.HeightMm.ToString(CultureInfo.InvariantCulture),
                            qty.ToString(CultureInfo.InvariantCulture));
                        row.Position = occ.Position;
                        row.ProformaId = DocumentKey ?? string.Empty;
                        row.SpecGroupNumber = group.GroupNumber;
                        row.Color = occ.Pane?.Color ?? string.Empty;
                        row.ThicknessMm = occ.ThicknessMm;
                        row.Processing = occ.Treatment ?? string.Empty;
                        row.MaterialKey = OptimizationMaterialIdentity.BuildKey(
                            string.Empty, occ.Pane?.Color, occ.ThicknessMm, occ.Treatment, string.Empty);
                        if (occ.Slot == PaneSlot.Pane2)
                        {
                            row.W2 = sizeGroup.Key.WidthMm;
                            row.H2 = sizeGroup.Key.HeightMm;
                        }
                        else
                        {
                            row.W1 = sizeGroup.Key.WidthMm;
                            row.H1 = sizeGroup.Key.HeightMm;
                        }
                        row.EnsureMaterialKey();
                        newPieces.Add(row);
                        piecesAdded += qty;
                    }
                }

                // The two racks this spec was quoted on, tagged with the Step 5
                // pane identity (spec number + side) that Step 6 and Step 9 join
                // on. OuterSheet* → "Outer" (W1/H1), InnerSheet* → "Inner"
                // (W2/H2) — the routing the whole chain already agrees on.
                if (group.OuterSheetWidthMm > 0 && group.OuterSheetHeightMm > 0)
                {
                    string outerComp = ComposeForSlot(PaneSlot.Pane1);
                    var outerOcc = occurrences.FirstOrDefault(x => x.Slot == PaneSlot.Pane1);
                    int outerThk = outerOcc != null && outerOcc.ThicknessMm > 0
                        ? outerOcc.ThicknessMm : 0;
                    string outerKey = OptimizationMaterialIdentity.BuildKey(
                        string.Empty, outerOcc?.Pane?.Color ?? group.OuterGlassColor,
                        outerThk, outerOcc?.Treatment, string.Empty);
                    int? outerBalance = outerThk > 0
                        ? LookupStoreBalance(group.OuterGlassColor,
                            outerThk, group.OuterSheetWidthMm, group.OuterSheetHeightMm)
                        : null;
                    if (outerBalance.HasValue) balancesApplied++;
                    racksAdded += AddStep5Rack(rackSeen, newStocks,
                        group.OuterSheetWidthMm, group.OuterSheetHeightMm,
                        outerComp,
                        specLabel + " outer", group.OuterSheetPricePerSqm,
                        group.GroupNumber, "Outer", outerBalance,
                        group.OuterGlassColor, outerThk, outerKey);
                    rackedKeys.Add(compKey(group.GroupNumber, outerComp));
                    CrossCheckStep4Sheet(group.GroupNumber, "outer", group.OuterGlassColor,
                        group.OuterSheetWidthMm, group.OuterSheetHeightMm, sheetNotes, outerThk);
                }

                if (group.InnerSheetWidthMm > 0 && group.InnerSheetHeightMm > 0)
                {
                    string innerComp = ComposeForSlot(PaneSlot.Pane2);
                    var innerOcc = occurrences.FirstOrDefault(x => x.Slot == PaneSlot.Pane2);
                    int innerThk = innerOcc != null && innerOcc.ThicknessMm > 0
                        ? innerOcc.ThicknessMm : 0;
                    string innerKey = OptimizationMaterialIdentity.BuildKey(
                        string.Empty, innerOcc?.Pane?.Color ?? group.InnerGlassColor,
                        innerThk, innerOcc?.Treatment, string.Empty);
                    int? innerBalance = innerThk > 0
                        ? LookupStoreBalance(group.InnerGlassColor,
                            innerThk, group.InnerSheetWidthMm, group.InnerSheetHeightMm)
                        : null;
                    if (innerBalance.HasValue) balancesApplied++;
                    racksAdded += AddStep5Rack(rackSeen, newStocks,
                        group.InnerSheetWidthMm, group.InnerSheetHeightMm,
                        innerComp,
                        specLabel + " inner", group.InnerSheetPricePerSqm,
                        group.GroupNumber, "Inner", innerBalance,
                        group.InnerGlassColor, innerThk, innerKey);
                    rackedKeys.Add(compKey(group.GroupNumber, innerComp));
                    CrossCheckStep4Sheet(group.GroupNumber, "inner", group.InnerGlassColor,
                        group.InnerSheetWidthMm, group.InnerSheetHeightMm, sheetNotes, innerThk);
                }

                if (group.ThirdSheetWidthMm > 0 && group.ThirdSheetHeightMm > 0)
                {
                    string thirdComp = ComposeForPosition("Lam Inner");
                    var thirdOcc = occurrences.FirstOrDefault(x =>
                        string.Equals(x.Position, "Lam Inner", StringComparison.OrdinalIgnoreCase));
                    // Independent of SETTINGS and of the other two panes.
                    int thirdThk = OptimizationMaterialIdentity.DguLamThirdRackThicknessMm(
                        thirdOcc != null ? thirdOcc.ThicknessMm : 0);
                    string thirdColor = !string.IsNullOrWhiteSpace(group.ThirdGlassColor)
                        ? group.ThirdGlassColor
                        : (thirdOcc?.Pane?.Color ?? string.Empty);
                    string thirdKey = OptimizationMaterialIdentity.BuildKey(
                        string.Empty, thirdColor, thirdThk, thirdOcc?.Treatment, string.Empty);
                    int? thirdBalance = thirdThk > 0 && !string.IsNullOrWhiteSpace(thirdColor)
                        ? LookupStoreBalance(thirdColor, thirdThk,
                            group.ThirdSheetWidthMm, group.ThirdSheetHeightMm)
                        : null;
                    if (thirdBalance.HasValue) balancesApplied++;
                    racksAdded += AddStep5Rack(rackSeen, newStocks,
                        group.ThirdSheetWidthMm, group.ThirdSheetHeightMm,
                        thirdComp,
                        specLabel + " lam inner", group.ThirdSheetPricePerSqm,
                        group.GroupNumber, "LamInner", thirdBalance,
                        thirdColor, thirdThk, thirdKey);
                    rackedKeys.Add(compKey(group.GroupNumber, thirdComp));
                    CrossCheckStep4Sheet(group.GroupNumber, "lam inner",
                        thirdColor,
                        group.ThirdSheetWidthMm, group.ThirdSheetHeightMm, sheetNotes, thirdThk);
                }
            }

            // ── Sheet Store cross-check fallback ─────────────────────────────────
            // Only for compositions Step 4 left without a sheet. Never overrides
            // a size Step 4 supplied: this fills holes, it does not second-guess.
            // The lookup is sync and cache-only (see SheetStorePricingService's
            // header note), so it cannot stall the UI thread on the database;
            // when inventory cannot answer, the honest refusal below still runs.
            int storeRacks = 0;
            foreach (var group in groups)
            {
                if (!wantedByGroup.TryGetValue(group.GroupNumber, out var wanted)) continue;
                foreach (var comp in wanted)
                {
                    if (rackedKeys.Contains(compKey(group.GroupNumber, comp))) continue;
                    string colour = !string.IsNullOrWhiteSpace(group.OuterGlassColor)
                        ? group.OuterGlassColor
                        : group.InnerGlassColor;
                    // Balance-aware overload: picks the largest sheet that is
                    // actually IN STOCK. The 4-argument one is untouched and still
                    // answers the weaker "does inventory hold this size at all?"
                    // question the cross-check note asks.
                    int paneThk = ParseThicknessMm(comp);
                    if (paneThk <= 0) continue;
                    if (!Services.ProformaInvoice.SheetStorePricingService.TryResolveStockSheet(
                            colour, paneThk, out int sheetW, out int sheetH,
                            out int sheetBalance))
                    {
                        continue;
                    }
                    storeRacks += AddStep5Rack(rackSeen, newStocks, sheetW, sheetH, comp,
                        "Sheet Store cross-check", group.OuterSheetPricePerSqm,
                        group.GroupNumber, "Outer", sheetBalance,
                        colour, paneThk,
                        OptimizationMaterialIdentity.BuildKey(string.Empty, colour, paneThk, string.Empty, string.Empty));
                    balancesApplied++;
                    rackedKeys.Add(compKey(group.GroupNumber, comp));
                    sheetNotes.Add(string.Format(CultureInfo.InvariantCulture,
                        "Spec {0} had no sheet size from Step 4 — cut against the largest "
                        + "{1} sheet in the Sheet Store ({2} × {3}, {4} on hand); confirm it on Step 4.",
                        group.GroupNumber,
                        string.IsNullOrWhiteSpace(colour) ? "stocked" : colour,
                        sheetW, sheetH, sheetBalance));
                }
            }
            racksAdded += storeRacks;

            if (balancesApplied > 0)
            {
                sheetNotes.Add(string.Format(CultureInfo.InvariantCulture,
                    "{0} rack(s) now carry their real Sheet Store balance instead of "
                    + "unlimited glass — check the Available column on STOCKS before "
                    + "running, and top it up there if the count is stale.",
                    balancesApplied));
            }

            // ── DIAGNOSTIC LOGGING ────────────────────────────────────────
            try
            {
                Services.Logging.AppLogService.Info(
                    $"[OPT-STEP5] built newPieces={newPieces.Count} newStocks={newStocks.Count} piecesAdded={piecesAdded} racksAdded={racksAdded} storeRacks={storeRacks} wantedGroups={wantedByGroup.Count}",
                    new Services.Logging.LogContext { Module = "Optimization", Operation = "RunFromStep5" });
            }
            catch { }

            if (newPieces.Count == 0)
            {
                message = "No cutting rows found on Step 5 — enter pane sizes and quantities first.";
                try
                {
                    Services.Logging.AppLogService.Warning(
                        $"[OPT-STEP5] no pieces after build: groups={groups.Count} cutItems={cut.Count} planWarnings={string.Join(" | ", plan.Warnings.Take(3))}",
                        new Services.Logging.LogContext { Module = "Optimization", Operation = "RunFromStep5" });
                }
                catch { }
                return false;
            }

            if (newStocks.Count == 0)
            {
                message = "The Step 5 specs have no sheet sizes, so there is no rack to cut against — "
                    + "set the outer/inner sheet size on Step 4 first. "
                    + "The Sheet Store had no stocked size for these glass colours either.";
                try
                {
                    Services.Logging.AppLogService.Warning(
                        $"[OPT-STEP5] no stocks after build: groups={groups.Count} wantedByGroup={wantedByGroup.Count} rackedKeys={rackedKeys.Count} doc={DocumentKey}",
                        new Services.Logging.LogContext { Module = "Optimization", Operation = "RunFromStep5" });
                }
                catch { }
                return false;
            }

            // Validated — and only now is the current pool replaced.
            Pieces.Clear();
            foreach (var p in newPieces) Pieces.Add(p);
            Stocks.Clear();
            foreach (var s in newStocks) Stocks.Add(s);

            // Step 5 has no SETTINGS tab, so the two buttons carry fixed depths:
            // QUICK is one deterministic pass, OPTIMIZE the deepest search.
            SelectedEffortLevel = string.Equals(stage, "OPTIMIZE", StringComparison.OrdinalIgnoreCase)
                ? Step5OptimizeEffort
                : Step5QuickEffort;

            RefreshDerived();

            // Run() is the same entry point the OPTIMIZATION tab's buttons use,
            // so validation, grouping, packing, progress, costing, the live
            // publish and the persisted latest-run row all behave identically.
            Run(string.Equals(stage, "OPTIMIZE", StringComparison.OrdinalIgnoreCase)
                ? "OPTIMIZE"
                : "QUICK");

            message = string.Format(CultureInfo.InvariantCulture,
                "{0} started from Step 5 · {1} piece(s) in {2} cutting group(s) against {3} rack(s).",
                string.Equals(stage, "OPTIMIZE", StringComparison.OrdinalIgnoreCase)
                    ? "OPTIMIZE" : "QUICK",
                piecesAdded,
                compositions.Count,
                racksAdded);
            // Provenance is part of the answer: an operator who sees a rack they
            // did not pick on Step 4 must be told it came from the Sheet Store,
            // not left to wonder why the sheet count moved.
            if (sheetNotes.Count > 0)
                message += " " + string.Join(" ", sheetNotes);

            // Distribution warnings were previously discarded here too: a Step 5
            // row with no quantity silently becomes 1 piece, a row without W2/H2
            // loses its inner pane. The operator must see that on the status
            // line, not discover it as a wrong sheet count later.
            if (plan.Warnings.Count > 0)
            {
                var distinct = plan.Warnings.Distinct().ToList();
                var shown = distinct.Take(3);
                message += " Import warnings: " + string.Join(" | ", shown)
                    + (distinct.Count > 3
                        ? string.Format(CultureInfo.InvariantCulture, " (+{0} more)", distinct.Count - 3)
                        : string.Empty);
            }
            return true;
        }

        /// <summary>
        /// Cross-checks a sheet size Step 4 supplied against Sheet Store
        /// inventory and, when they disagree, adds a NOTE.
        /// </summary>
        /// <remarks>
        /// Deliberately never an override: Step 4 is the authority for what this
        /// quote is cut from, and inventory legitimately holds sizes a supplier
        /// has stopped listing. It also stays silent when the store cannot answer
        /// at all (snapshot not primed yet, or that colour is not stocked), so a
        /// cold cache can never manufacture a false warning.
        /// </remarks>
        private static void CrossCheckStep4Sheet(
            int specNumber, string side, string? colour, int widthMm, int heightMm,
            List<string> notes, int thicknessMm = 0)
        {
            if (widthMm <= 0 || heightMm <= 0) return;
            if (string.IsNullOrWhiteSpace(colour) || thicknessMm <= 0) return;
            // "Can the store say anything about this colour?" False means unknown
            // — cold snapshot or an unstocked colour — not "wrong size".
            if (!Services.ProformaInvoice.SheetStorePricingService
                    .TryResolveStockSheet(colour, thicknessMm, out int stockedW, out int stockedH))
            {
                return;
            }
            if (Services.ProformaInvoice.SheetStorePricingService
                    .IsStockedSize(colour, thicknessMm, widthMm, heightMm))
            {
                return;
            }
            notes.Add(string.Format(CultureInfo.InvariantCulture,
                "Spec {0} {1}: the Step 4 sheet {2} × {3} is not a size the Sheet Store "
                + "stocks for {4} (largest stocked is {5} × {6}) — Step 4 still governs the run.",
                specNumber, side, widthMm, heightMm, colour, stockedW, stockedH));
        }

        /// <summary>
        /// Adds one Step 4 rack to the stock pool, skipping a size × composition
        /// already present so two specs sharing a sheet do not create two
        /// competing racks for it. Returns 1 when a row was added, else 0.
        /// </summary>
        /// <remarks>
        /// Mirrors <c>SyncStocksFromSelectedJobs</c> in
        /// OptimizationViewModel.Stocks.cs, including leaving
        /// <c>AvailableText</c> untouched: an empty value is what makes the pool
        /// usage-tracked rather than capped, and the Job Order path relies on
        /// exactly that.
        /// </remarks>
        /// <summary>
        /// PHASE 10: the Sheet Store's own count for a rack, or <c>null</c> when
        /// the store cannot say.
        ///
        /// <c>null</c> and never <c>0</c> is the whole point. 0 in
        /// <see cref="StockSize.MaxSheets"/> is the <c>UnlimitedSheets</c>
        /// sentinel — infinite glass — so answering "cannot say" with 0 would hand
        /// the engine unlimited stock for a rack the store could not price. A null
        /// leaves <see cref="StockEntryVm"/>'s default in place, which is exactly
        /// the pre-Phase-10 behaviour: the run plans as it always did, and the
        /// operator is not told a number inventory never gave.
        /// </summary>
        private static int? LookupStoreBalance(
            string? colour, int thicknessMm, int widthMm, int heightMm) =>
            Services.ProformaInvoice.SheetStorePricingService.TryGetStockBalance(
                colour, thicknessMm, widthMm, heightMm, out int balance)
                ? balance
                : (int?)null;

        private int AddStep5Rack(
            HashSet<(int W, int H, string C)> seen,
            List<StockEntryVm> target,
            int widthMm, int heightMm, string composition, string source,
            decimal pricePerSqm, int specNumber, string side,
            int? storeBalance = null,
            string? color = null,
            int thicknessMm = 0,
            string? materialKey = null)
        {
            if (widthMm <= 0 || heightMm <= 0) return 0;

            string comp = (composition ?? string.Empty).Trim();
            if (!seen.Add((widthMm, heightMm, comp))) return 0;

            var row = new StockEntryVm(
                widthMm.ToString(CultureInfo.InvariantCulture),
                heightMm.ToString(CultureInfo.InvariantCulture),
                comp,
                "Step 5 · " + source)
            {
                IsUsedInRun = true,
                PricePerSqm = pricePerSqm,
                SpecNumber = specNumber,
                Side = side,
                Color = color ?? string.Empty,
                ThicknessMm = thicknessMm,
                MaterialKey = materialKey ?? string.Empty,
                SheetStoreSku = OptimizationMaterialIdentity.StockSku(color, thicknessMm, widthMm, heightMm)
            };

            // PHASE 10: until now this row was built without an Available value,
            // so StockEntryVm's default applied and the engine planned against
            // INFINITE glass — the Optimization module was the one consumer of
            // Sheet Store inventory that ignored the balance. Both Production
            // consumers filter Balance > 0 and pass the real count; this now does
            // too. Only a number inventory actually gave is written: a null keeps
            // the default rather than inventing a cap, and a genuine 0 is written
            // as 0 because 0 sheets means 0 sheets — TryBuildInputs already leaves
            // a zero-balance rack out of the run and tells the operator.
            if (storeBalance.HasValue)
                row.AvailableText = storeBalance.Value.ToString(CultureInfo.InvariantCulture);

            target.Add(row);
            return 1;
        }
    }
}

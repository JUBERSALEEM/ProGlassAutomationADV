// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Production/ProductionDistributionEngine.cs
// PROJECT: ProGlassAutomation
// MODULE: Production — distribution engine (Phase 1, 2026-09-11)
//
// Turns one ProformaInvoiceSnapshot (usually the immutable copy embedded
// in a released Job Order) into a deterministic ProductionJobPlan:
// every spec row fanned out into stage work items for the eight
// production modules.
//
// ROUTING RULES (owner-confirmed 2026-09-11, see the plan doc):
//   • SGU      → W1/H1 only; Cutting always; Tempering only when the pane
//                treatment is FT / Heat Strengthened / Heat Soaked.
//                Annealed SGU goes Cutting → ready for dispatch.
//   • DGU      → W1/H1 (outer) + W2/H2 (inner); per-pane cutting and
//                tempering; Pairing then Doubling/DGU assembly.
//   • LAM      → same pane split; Doubling runs in LAMINATION mode (PVB).
//   • DGU+LAM  → W1/H1 mono outer + W2/H2 laminated pair (two panes cut
//                at the same size); Pairing + hybrid assembly.
//   • Step 6   → active FAN_HOLES/HOLES/CUTOUT/COUNTERSUNK_HOLES feed the
//                Holes & Cutouts queue; POLISH/POLISH_MATCHED_LAM and
//                EDGE_GRINDING feed Polish with the charged LM;
//                MITRING feeds Mitring; ARGON/SILICON/U-INSERT/OVERLAP/
//                SHUTTER_BONDING attach as notes on the assembly item.
//
//   SCOPE LOCK (2026-09-11): only the four Step 4 build families exist —
//   SGU / DGU / LAM / DGU+LAM. Triple glazing is NOT part of the product
//   today and is deliberately not special-cased; any spec name degrades
//   to the closest supported family with a warning, never to invention.
//
// PURE AND DETERMINISTIC — no WPF, no DB, no clock. Same snapshot in,
// byte-identical plan out. Never throws on an odd spec name: it degrades
// to the honest fallback and records a warning.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.Linq;
using ProGlassAutomation.Models.Production;
using ProGlassAutomation.Models.ProformaInvoice.SavedInvoice;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Utilities;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step9ProfitLoss;

namespace ProGlassAutomation.Services.Production
{
    public static class ProductionDistributionEngine
    {
        // Step 6 template keys that create Holes & Cutouts work.
        private static readonly HashSet<string> HoleChargeKeys = new(StringComparer.OrdinalIgnoreCase)
        {
            "FAN_HOLES", "HOLES", "CUTOUT", "COUNTERSUNK_HOLES"
        };

        // Step 6 template keys that decorate the assembly (Doubling) item.
        private static readonly Dictionary<string, string> AssemblyChargeNotes =
            new(StringComparer.OrdinalIgnoreCase)
        {
            ["ARGON_GAS"] = "ARGON GAS FILL",
            ["SILICON_BED"] = "SILICON BED SEAL",
            ["SILICON_BED_SQM"] = "SILICON BED SEAL",
            ["U_INSERT"] = "U-INSERT SEALING",
            ["OVERLAP"] = "STEPPED OVERLAP EDGES",
            ["OVERLAP_SQM"] = "STEPPED OVERLAP EDGES",
            ["SHUTTER_BONDING"] = "SHUTTER BONDING"
        };

        /// <summary>Human label for a stage — shared by all eight module UIs.</summary>
        public static string StageLabel(ProductionStage stage) => stage switch
        {
            ProductionStage.Cutting => "Cutting",
            ProductionStage.HolesCutouts => "Holes & Cutouts",
            ProductionStage.Polish => "Polish",
            ProductionStage.EdgeDeletion => "Edge Deletion",
            ProductionStage.Mitring => "Mitring",
            ProductionStage.Tempering => "Tempering",
            ProductionStage.Pairing => "Pairing",
            ProductionStage.DoublingLamination => "Doubling / Lamination",
            _ => stage.ToString()
        };

        /// <summary>
        /// Distributes one invoice snapshot into a full job plan.
        /// <paramref name="jobRef"/> is the Job Order id (or the ProformaId
        /// when releasing directly from a PI).
        /// </summary>
        public static ProductionJobPlan Distribute(
            string jobRef, ProformaInvoiceSnapshot snapshot)
        {
            var plan = new ProductionJobPlan
            {
                JobRef = jobRef ?? "",
                SourceProformaId = snapshot?.Company?.ProformaId ?? "",
                CustomerName = snapshot?.Client?.CustomerName ?? "",
                ProjectName = snapshot?.Project?.ProjectIdentifierName ?? "",
                Salesman = snapshot?.Client?.SalesRepresentative ?? ""
            };

            if (snapshot == null)
            {
                plan.Warnings.Add("No invoice snapshot supplied — nothing to distribute.");
                return plan;
            }

            var groups = (snapshot.SpecGroups ?? new List<SpecGroupSnapshot>())
                .Where(g => g != null)
                .OrderBy(g => g.GroupNumber)
                .ToList();

            if (groups.Count == 0)
            {
                plan.Warnings.Add("The invoice has no specification groups (Step 5 is empty).");
                return plan;
            }

            // Step 6 pre-scan: assembly notes per spec + charge rows.
            var assemblyNotes = CollectAssemblyNotes(snapshot, plan, groups);
            var charges = (snapshot.OtherCharges?.PredefinedCharges ?? new List<PredefinedChargeSnapshot>())
                .Where(c => c != null && c.IsActive)
                .ToList();

            // Pane occurrences are resolved once per spec and shared by the
            // per-spec distribution and the charge distribution (charges now
            // fan out per pane too, so piece quantities flow through them).
            var occurrenceCache = new Dictionary<int, IReadOnlyList<PaneOccurrence>>();

            foreach (var group in groups)
            {
                DistributeGroup(plan, snapshot, group, assemblyNotes, occurrenceCache);
            }

            DistributeChargeItems(plan, snapshot, groups, charges, occurrenceCache);

            plan.Items = plan.Items
                .OrderBy(i => i.SpecGroupNumber)
                .ThenBy(i => i.SerialNumber)
                .ThenBy(i => (int)i.Stage)
                .ThenBy(i => i.Position, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return plan;
        }

        // ────────────────────────────────────────────────────────────────
        //  BUILD TYPE
        // ────────────────────────────────────────────────────────────────

        /// <summary>
        /// Resolves the physical build for a spec group. Precedence:
        ///   1. The operator's Step 5 glass type (<see cref="SpecGroupSnapshot.SelectedGlassType"/>)
        ///      — the explicit build chosen at Step 4/5 handoff, and the
        ///      ground truth for what the cutting list actually cuts. Without
        ///      this, a spec whose NAME doesn't parse to two panes (e.g. a
        ///      free-typed "6mm clear DGU" name) collapses to SGU and its
        ///      INNER pane — sizes, pieces and Step 4 sheet — silently
        ///      disappears from the optimization import.
        ///   2. Step 9's persisted classification (it saw the same name at
        ///      costing time).
        ///   3. The spec-name grammar.
        /// Never throws — an unreadable name degrades to SGU with a warning.
        /// </summary>
        public static string ResolveBuildType(
            SpecGroupSnapshot group, ProformaInvoiceSnapshot? snapshot)
        {
            string? fromGlassType = NormalizeGlassTypeToBuildType(group.SelectedGlassType);
            if (fromGlassType != null) return fromGlassType;

            var target = snapshot?.ProfitLoss?.SpecTargets?
                .FirstOrDefault(t => t != null && t.SpecNumber == group.GroupNumber
                    && !string.IsNullOrWhiteSpace(t.BuildType));

            if (target != null && Enum.TryParse<GlassBuildTypeName>(
                    target.BuildType, ignoreCase: true, out var fromTarget))
            {
                return fromTarget.ToString();
            }

            return InferBuildTypeFromName(group.SelectedSpecName);
        }

        /// <summary>
        /// Maps a Step 5 <c>SelectedGlassType</c> value ("SGU", "DGU",
        /// "LAM", "DGU+LAM") onto the internal build-type string. Returns
        /// null when the value is blank or unsupported so callers fall back
        /// to the next classification source.
        /// </summary>
        private static string? NormalizeGlassTypeToBuildType(string? glassType)
        {
            if (string.IsNullOrWhiteSpace(glassType)) return null;
            string t = glassType.Trim().Replace("+", string.Empty)
                .Replace(" ", string.Empty).ToLowerInvariant();
            return t switch
            {
                "sgu" => "Sgu",
                "dgu" => "Dgu",
                "lam" => "Lam",
                "dgulam" => "DguLam",
                _ => null
            };
        }

        /// <summary>Local mirror of GlassBuildType names (string contract).</summary>
        private enum GlassBuildTypeName { Sgu, Dgu, Lam, DguLam }

        /// <summary>
        /// Grammar-based inference over the four supported build families
        /// (SGU / DGU / LAM / DGU+LAM). Two panes + interlayer = LAM;
        /// three or more panes = DGU+LAM hybrid; anything else with a
        /// second pane = DGU; single pane = SGU.
        /// </summary>
        public static string InferBuildTypeFromName(string? specName)
        {
            try
            {
                int panes = PnLSpecParser.GlassMatches(specName).Count;
                bool hasInterlayer = PnLSpecParser.Interlayer(specName).Success;

                if (panes <= 1) return "Sgu";
                if (panes == 2) return hasInterlayer ? "Lam" : "Dgu";
                return hasInterlayer ? "DguLam" : "Dgu";
            }
            catch (Exception ex)
            {
                AppLogService.Debug($"[ProductionDistribution] build-type inference failed: {ex.Message}");
                return "Sgu";
            }
        }

        // ────────────────────────────────────────────────────────────────
        //  PER-SPEC DISTRIBUTION
        // ────────────────────────────────────────────────────────────────

        private static void DistributeGroup(
            ProductionJobPlan plan,
            ProformaInvoiceSnapshot snapshot,
            SpecGroupSnapshot group,
            Dictionary<int, List<string>> assemblyNotes,
            Dictionary<int, IReadOnlyList<PaneOccurrence>> occurrenceCache)
        {
            string buildType = ResolveBuildType(group, snapshot);
            var occurrences = ResolvePaneOccurrences(buildType, group, plan.Warnings);
            occurrenceCache[group.GroupNumber] = occurrences;

            // Phase 2: an explicit Step 5 glass type that CONTRADICTS the spec
            // name silently adds or removes whole panes — an SGU spec typed
            // "DGU" grows a full-quantity inner pane from W1×H1 (equal-size
            // fallback), i.e. glass the job order never ordered. The explicit
            // selection still wins (the operator may know better than the
            // name grammar), but the mismatch must be visible.
            string inferred = InferBuildTypeFromName(group.SelectedSpecName);
            if (!string.Equals(inferred, buildType, StringComparison.Ordinal))
                plan.Warnings.Add(
                    $"Spec {group.GroupNumber}: glass type '{(group.SelectedGlassType ?? string.Empty).Trim()}' " +
                    $"selected but the spec name suggests {inferred} — check Step 5 " +
                    $"(a wrong glass type adds or removes whole panes from the cutting list).");

            var rows = (group.ItemRows ?? new List<GlassItemRowSnapshot>())
                .Where(r => r != null && !IsEmptyRow(r))
                .OrderBy(r => r.SerialNumber)
                .ToList();

            if (rows.Count == 0) return; // empty group contributes nothing

            bool multiPane = occurrences.Count > 1;
            var notesForSpec = assemblyNotes.TryGetValue(group.GroupNumber, out var n)
                ? n : new List<string>();

            foreach (var row in rows)
            {
                foreach (var occ in occurrences)
                {
                    int w = occ.Slot == PaneSlot.Pane1 ? row.W1 : row.W2;
                    int h = occ.Slot == PaneSlot.Pane1 ? row.H1 : row.H2;

                    // STEP 4/5 SIZE RULE (industry semantics):
                    //  • W2/H2 filled in → OVERLAP glass: the pane is cut
                    //    to exactly W2×H2.
                    //  • W2/H2 blank on a multi-pane build → EQUAL-SIZE
                    //    unit: the pane inherits W1×H1 (only one size is
                    //    entered when both panes cut the same).
                    if ((w <= 0 || h <= 0) && multiPane &&
                        occ.Slot == PaneSlot.Pane2 && row.W1 > 0 && row.H1 > 0)
                    {
                        w = row.W1;
                        h = row.H1;
                    }

                    if (w <= 0 || h <= 0)
                    {
                        plan.Warnings.Add(
                            $"Spec {group.GroupNumber}: row {row.SerialNumber} ({SafeRef(row)}) has no " +
                            $"{(occ.Slot == PaneSlot.Pane1 ? "W1/H1" : "W2/H2")} size — the {occ.Position} pane was skipped.");
                        continue;
                    }

                    if (row.Qty <= 0)
                    {
                        plan.Warnings.Add(
                            $"Spec {group.GroupNumber}: row {row.SerialNumber} ({SafeRef(row)}) has no quantity — " +
                            "produced as 1 piece. Fix the Step 5 qty and re-release.");
                    }
                    int qty = row.Qty <= 0 ? 1 : row.Qty;
                    decimal unitSqm = SqmCalculator.CalculatePanel(w, h);
                    decimal totalSqm = Round3(unitSqm * qty);
                    int perimeterFactor = row.PerimeterFactor > 0 ? row.PerimeterFactor : 2;
                    decimal totalLm = Round3(((decimal)w + h) / 1000m * perimeterFactor * qty);

                    var cutting = BaseItem(plan, group, row, buildType, occ, w, h, qty,
                        unitSqm, totalSqm, totalLm, ProductionStage.Cutting, predecessor: "");
                    plan.Items.Add(cutting);

                    if (occ.Pane != null && occ.Pane.RequiresTempering)
                    {
                        var tempering = BaseItem(plan, group, row, buildType, occ, w, h, qty,
                            unitSqm, totalSqm, totalLm, ProductionStage.Tempering,
                            predecessor: StageLabel(ProductionStage.Cutting));
                        tempering.Notes = $"{occ.Pane.Treatment.ToUpperInvariant()} CYCLE";
                        plan.Items.Add(tempering);
                    }
                }

                if (multiPane)
                {
                    int qty = row.Qty <= 0 ? 1 : row.Qty;

                    var pairing = BaseItem(plan, group, row, buildType,
                        new PaneOccurrence { Position = "Unit", Slot = PaneSlot.Pane1 },
                        row.W1, row.H1, qty, 0m, 0m, 0m,
                        ProductionStage.Pairing,
                        predecessor: StageLabel(ProductionStage.Cutting));
                    pairing.Notes = $"{occurrences.Count}-PANE UNIT";
                    plan.Items.Add(pairing);

                    var assembly = BaseItem(plan, group, row, buildType,
                        new PaneOccurrence { Position = "Unit", Slot = PaneSlot.Pane1 },
                        row.W1, row.H1, qty, 0m, 0m, 0m,
                        ProductionStage.DoublingLamination,
                        predecessor: StageLabel(ProductionStage.Pairing));
                    assembly.Notes = BuildAssemblyNotes(group, buildType, notesForSpec);
                    plan.Items.Add(assembly);
                }
            }

            // SGU sanity: the grid occasionally carries stray W2/H2 on
            // single-pane specs — surface it once per spec.
            if (!multiPane && rows.Any(r => r.W2 > 0 || r.H2 > 0))
            {
                plan.Warnings.Add(
                    $"Spec {group.GroupNumber} is a single glass unit (SGU) but some rows " +
                    "carry W2/H2 — those dimensions are ignored for production.");
            }
        }

        private static bool IsEmptyRow(GlassItemRowSnapshot row) =>
            string.IsNullOrWhiteSpace(row.GlassRef)
            && row.W1 <= 0 && row.H1 <= 0 && row.W2 <= 0 && row.H2 <= 0;

        private static string SafeRef(GlassItemRowSnapshot row) =>
            string.IsNullOrWhiteSpace(row.GlassRef) ? "(no ref)" : row.GlassRef;

        private static decimal Round3(decimal value) =>
            Math.Round(value, 3, MidpointRounding.AwayFromZero);

        // ────────────────────────────────────────────────────────────────
        //  PANE OCCURRENCES (position ↔ dimension pair)
        // ────────────────────────────────────────────────────────────────

        /// <summary>
        /// One physical cut: label + which dimension pair feeds it. Public so
        /// the Optimization module reuses the SAME pane routing as the floor —
        /// position, glass composition and treatment can never drift apart.
        /// </summary>
        public sealed class PaneOccurrence
        {
            public string Position { get; init; } = "";
            public PaneSlot Slot { get; init; }
            public PnLPane? Pane { get; init; }

            /// <summary>Per-pane glass composition, e.g.
            /// "6mm hp neutral 70 low e FT glass". Empty when the spec name
            /// did not parse (callers fall back to the raw spec name).</summary>
            public string Composition => Pane == null ? string.Empty : PaneDisplayName(Pane);

            /// <summary>Pane glass thickness in mm (0 when unparsed).</summary>
            public int ThicknessMm => Pane?.ThicknessMm ?? 0;

            /// <summary>FT / Annealed / Heat Strengthened / Heat Soaked / "".</summary>
            public string Treatment => Pane?.Treatment ?? string.Empty;

            public bool RequiresTempering => Pane?.RequiresTempering ?? false;
        }

        /// <summary>
        /// Canonical per-pane glass label — the merge key the optimizer groups
        /// by, so two specs sharing the same outer pane (e.g. a low-e pane on
        /// a clear DGU and an obscure DGU) collapse into ONE cutting group.
        /// </summary>
        public static string PaneDisplayName(PnLPane pane)
        {
            string color = (pane.Color ?? string.Empty).Trim();
            string treat = (pane.Treatment ?? string.Empty).Trim();
            string name = $"{pane.ThicknessMm}mm {color}".Trim();
            if (treat.Length > 0) name += " " + treat;
            name += " glass";
            return name;
        }

        /// <summary>
        /// Maps a build type to its physical cuts, pairing the parsed panes
        /// (with treatments) to the grid's dimension pairs. When the name
        /// does not parse, occurrences degrade to treatment-less slots so
        /// the job still flows — with a warning.
        /// </summary>
        public static IReadOnlyList<PaneOccurrence> ResolvePaneOccurrences(
            string buildType, SpecGroupSnapshot group, IList<string>? warnings = null)
        {
            var panes = new List<PnLPane>();
            try { panes = PnLSpecParser.ResolveAllPanes(group.SelectedSpecName).ToList(); }
            catch (Exception ex)
            {
                AppLogService.Debug($"[ProductionDistribution] pane parse failed: {ex.Message}");
            }

            if (panes.Count == 0 && buildType != "Sgu")
            {
                warnings?.Add(
                    $"Spec {group.GroupNumber} ('{group.SelectedSpecName ?? ""}') could not be parsed — " +
                    "panes are routed without treatment info (assume ANNEALED unless told otherwise).");
            }

            PnLPane? At(int i) => i < panes.Count ? panes[i] : null;

            switch (buildType)
            {
                case "Dgu":
                    // Outer = first pane, Inner = LAST pane (the grammar's
                    // own rule) — so even an unusual multi-token name keeps
                    // the correct two physical cuts on the supported grid.
                    return new List<PaneOccurrence>
                    {
                        new() { Position = "Outer", Slot = PaneSlot.Pane1, Pane = At(0) },
                        new() { Position = "Inner", Slot = PaneSlot.Pane2, Pane = At(panes.Count - 1) }
                    };

                case "Lam":
                    return new List<PaneOccurrence>
                    {
                        new() { Position = "Lam Outer", Slot = PaneSlot.Pane1, Pane = At(0) },
                        new() { Position = "Lam Inner", Slot = PaneSlot.Pane2, Pane = At(panes.Count - 1) }
                    };

                case "DguLam":
                    return new List<PaneOccurrence>
                    {
                        new() { Position = "Outer",     Slot = PaneSlot.Pane1, Pane = At(0) },
                        new() { Position = "Lam Outer", Slot = PaneSlot.Pane2, Pane = At(1) },
                        new() { Position = "Lam Inner", Slot = PaneSlot.Pane2, Pane = At(2) }
                    };

                default: // Sgu
                    return new List<PaneOccurrence>
                    {
                        new() { Position = "Glass", Slot = PaneSlot.Pane1, Pane = At(0) }
                    };
            }
        }

        // ────────────────────────────────────────────────────────────────
        //  STEP 6 CHARGE DISTRIBUTION
        // ────────────────────────────────────────────────────────────────

        private static Dictionary<int, List<string>> CollectAssemblyNotes(
            ProformaInvoiceSnapshot snapshot,
            ProductionJobPlan plan,
            List<SpecGroupSnapshot> groups)
        {
            var result = new Dictionary<int, List<string>>();
            var charges = snapshot.OtherCharges?.PredefinedCharges
                ?? new List<PredefinedChargeSnapshot>();

            foreach (var charge in charges)
            {
                if (charge == null || !charge.IsActive) continue;
                if (!AssemblyChargeNotes.TryGetValue(charge.TemplateKey ?? "", out var note))
                    continue;

                foreach (int specNo in TargetSpecs(charge, groups))
                {
                    if (!result.TryGetValue(specNo, out var list))
                    {
                        list = new List<string>();
                        result[specNo] = list;
                    }
                    if (!list.Contains(note)) list.Add(note);
                }
            }

            return result;
        }

        private static void DistributeChargeItems(
            ProductionJobPlan plan,
            ProformaInvoiceSnapshot snapshot,
            List<SpecGroupSnapshot> groups,
            List<PredefinedChargeSnapshot> charges,
            Dictionary<int, IReadOnlyList<PaneOccurrence>> occurrenceCache)
        {
            foreach (var charge in charges)
            {
                string key = charge.TemplateKey ?? "";

                ProductionStage? stage =
                    HoleChargeKeys.Contains(key) ? ProductionStage.HolesCutouts
                    : key is "POLISH" or "POLISH_MATCHED_LAM" or "EDGE_GRINDING" ? ProductionStage.Polish
                    : key == "MITRING" ? ProductionStage.Mitring
                    : null;

                if (stage == null)
                {
                    if (key == "SANDBLASTING")
                    {
                        plan.Warnings.Add(
                            "Sandblasting is charged on this job but has no dedicated production " +
                            "module yet — track it manually on the floor.");
                    }
                    continue; // DELIVERY / U_INSERT / argon etc. are commercial or assembly notes
                }

                var targets = TargetSpecs(charge, groups).ToList();
                if (targets.Count == 0)
                {
                    plan.Warnings.Add(
                        $"Step 6 charge '{ChargeDisplayName(charge)}' is active but targets no spec group.");
                    continue;
                }

                // PIECE FLOW: charge work fans out per pane, exactly like
                // cutting/tempering — so cut pieces physically walk through
                // Holes / Polish / Mitring on their way to tempering.
                foreach (int specNo in targets)
                {
                    var group = groups.First(g => g.GroupNumber == specNo);
                    string buildType = ResolveBuildType(group, snapshot);
                    string metricUnit = stage == ProductionStage.HolesCutouts ? "QTY" : "LM";

                    if (!occurrenceCache.TryGetValue(specNo, out var occurrences) || occurrences.Count == 0)
                        continue;

                    var rows = (group.ItemRows ?? new List<GlassItemRowSnapshot>())
                        .Where(r => r != null && !IsEmptyRow(r))
                        .OrderBy(r => r.SerialNumber)
                        .ToList();

                    foreach (var row in rows)
                    {
                        foreach (var occ in occurrences)
                        {
                            int w = occ.Slot == PaneSlot.Pane1 ? row.W1 : row.W2;
                            int h = occ.Slot == PaneSlot.Pane1 ? row.H1 : row.H2;

                            // Same Step 4/5 rule as cutting: blank W2/H2 on
                            // a multi-pane build means the pane cuts to
                            // W1×H1 (equal-size unit).
                            if ((w <= 0 || h <= 0) && occ.Slot == PaneSlot.Pane2
                                && row.W1 > 0 && row.H1 > 0)
                            {
                                w = row.W1;
                                h = row.H1;
                            }
                            if (w <= 0 || h <= 0) continue; // same skip rule as cutting

                            int qty = row.Qty <= 0 ? 1 : row.Qty;
                            decimal unitSqm = SqmCalculator.CalculatePanel(w, h);
                            decimal totalSqm = Round3(unitSqm * qty);
                            int perimeterFactor = row.PerimeterFactor > 0 ? row.PerimeterFactor : 2;
                            decimal totalLm = stage == ProductionStage.HolesCutouts
                                ? 0m
                                : Round3(((decimal)w + h) / 1000m * perimeterFactor * qty);

                            var item = BaseItem(plan, group, row, buildType, occ, w, h, qty,
                                unitSqm, totalSqm, totalLm, stage.Value,
                                predecessor: StageLabel(ProductionStage.Cutting));

                            // Charge identity rides in the key — two charges
                            // in the same stage (e.g. FAN_HOLES + CUTOUT)
                            // must stay distinct items.
                            item.UniqueKey = BuildKey(plan.JobRef, specNo, row.SerialNumber,
                                occ.Position, stage.Value, chargeKey: key);
                            item.ChargeTemplateKey = key;
                            item.ChargeMetricQty = charge.MetricQty ?? 0m;
                            item.ChargeMetricUnit = metricUnit;
                            item.Notes = ChargeNote(charge, targets);

                            if (stage == ProductionStage.Polish)
                            {
                                item.PolishSubTypeLabel = key == "EDGE_GRINDING"
                                    ? "EDGE GRINDING (PRE-POLISH)"
                                    : PolishLabel(charge);
                            }

                            plan.Items.Add(item);
                        }
                    }
                }
            }
        }

        private static IEnumerable<int> TargetSpecs(
            PredefinedChargeSnapshot charge, List<SpecGroupSnapshot> groups)
        {
            var checkedSpecs = charge.CheckedSpecGroupNumbers ?? new List<int>();
            var all = groups.Select(g => g.GroupNumber).ToList();

            if (checkedSpecs.Count == 0) return all; // empty = all specs (Chat 20 contract)

            return checkedSpecs.Where(no => all.Contains(no)).OrderBy(no => no).Distinct();
        }

        private static string ChargeNote(
            PredefinedChargeSnapshot charge, List<int> targets)
        {
            string baseNote = ChargeDisplayName(charge);
            if (targets.Count > 1)
            {
                baseNote += $" — QTY SHARED ACROSS SPECS {string.Join(", ", targets)} (Step 6 level)";
            }
            return baseNote;
        }

        private static string ChargeDisplayName(PredefinedChargeSnapshot charge) =>
            (charge.TemplateKey ?? "").Replace('_', ' ');

        private static string PolishLabel(PredefinedChargeSnapshot charge)
        {
            string sub = charge.PolishSubType ?? "";
            string label = string.IsNullOrWhiteSpace(sub) ? "POLISH" : sub;

            if (charge.TemplateKey == "POLISH_MATCHED_LAM")
                label = $"SAME-GLASS LAM 2× · {label}";

            if (!string.IsNullOrWhiteSpace(charge.CustomPolishDescription))
                label += $" · {charge.CustomPolishDescription}";

            return label;
        }

        private static string BuildAssemblyNotes(
            SpecGroupSnapshot group, string buildType, List<string> chargeNotes)
        {
            var notes = new List<string>();
            string name = group.SelectedSpecName ?? "";

            try
            {
                var spacer = PnLSpecParser.Spacer(name);
                if (spacer.Success)
                    notes.Add($"SPACER {spacer.Groups["mm"].Value}MM");

                var interlayer = PnLSpecParser.Interlayer(name);
                if (interlayer.Success)
                {
                    string colour = (interlayer.Groups["color"].Value ?? "").Trim();
                    notes.Add(
                        $"{interlayer.Groups["type"].Value.ToUpperInvariant()} " +
                        $"{interlayer.Groups["mm"].Value}MM" +
                        (colour.Length > 0 ? $" {colour.ToUpperInvariant()}" : ""));
                }
            }
            catch (Exception ex)
            {
                AppLogService.Debug($"[ProductionDistribution] assembly note parse failed: {ex.Message}");
            }

            notes.AddRange(chargeNotes);

            string mode = buildType switch
            {
                "Lam" => "LAMINATION",
                "DguLam" => "DGU + LAMINATION (HYBRID)",
                _ => "DGU ASSEMBLY"
            };

            return notes.Count == 0 ? mode : $"{mode} · {string.Join(" · ", notes)}";
        }

        // ────────────────────────────────────────────────────────────────
        //  WORK ITEM FACTORY
        // ────────────────────────────────────────────────────────────────

        private static ProductionWorkItem BaseItem(
            ProductionJobPlan plan,
            SpecGroupSnapshot group,
            GlassItemRowSnapshot row,
            string buildType,
            PaneOccurrence occ,
            int widthMm, int heightMm, int qty,
            decimal unitSqm, decimal totalSqm, decimal totalLm,
            ProductionStage stage, string predecessor)
        {
            return new ProductionWorkItem
            {
                UniqueKey = BuildKey(plan.JobRef, group.GroupNumber, row.SerialNumber,
                    occ.Position, stage, chargeKey: ""),
                JobRef = plan.JobRef,
                SourceProformaId = plan.SourceProformaId,
                CustomerName = plan.CustomerName,
                ProjectName = plan.ProjectName,
                Salesman = plan.Salesman,
                SpecGroupNumber = group.GroupNumber,
                SpecName = group.SelectedSpecName ?? "",
                BuildType = buildType,
                SerialNumber = row.SerialNumber,
                GlassRef = row.GlassRef ?? "",
                Position = occ.Position,
                WidthMm = widthMm,
                HeightMm = heightMm,
                Qty = qty,
                UnitSqm = unitSqm,
                TotalSqm = totalSqm,
                TotalLm = totalLm,
                Treatment = occ.Pane?.Treatment ?? "",
                Stage = stage,
                Status = WorkItemStatus.Pending,
                PredecessorStage = predecessor
            };
        }

        /// <summary>
        /// Deterministic identity — the unique index in production.db.
        /// A re-release produces the SAME keys, so statuses survive.
        /// </summary>
        private static string BuildKey(
            string jobRef, int specNo, int serial,
            string position, ProductionStage stage, string chargeKey)
        {
            string key = $"{jobRef}|{specNo}|{serial}|{position}|{(int)stage}";
            return string.IsNullOrEmpty(chargeKey) ? key : $"{key}|CHG:{chargeKey}";
        }
    }
}

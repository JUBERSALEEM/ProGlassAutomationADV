// ═══════════════════════════════════════════════════════════════════════
// PORTED from the WPF application (read 2026-09-27):
//   Models/Production/ProductionModels.cs (ProductionStage + labels,
//     WorkItemStatus — append-only, 0..2 frozen, PaneSlot).
//   Services/Production/DguDimensionSemantics.cs (TryResolveInnerSize —
//     incomplete pairs are skipped with a warning, never invented).
//   Services/Production/ProductionDistributionEngine.cs (Distribute,
//     ResolveBuildType, ResolvePaneOccurrences, PaneDisplayName, charge
//     distribution, BuildAssemblyNotes, BuildKey — the routing rules
//     owner-confirmed 2026-09-11: SGU/DGU/LAM/DGU+LAM only, Tempering
//     only for FT/HS/HS panes, Step 6 charges fan out per pane).
//   Models/Production/SavedProduction/SavedProductionPhase14Entities.cs
//     (ProductionEntryType, QcResult).
// The WPF files are the source of truth; behaviour preserved verbatim.
// PURE AND DETERMINISTIC — no store, no clock. Same inputs in, identical
// plan out. Never throws on an odd spec name: it degrades to the honest
// fallback and records a warning.
// ═══════════════════════════════════════════════════════════════════════

namespace ProGlass.Saas.Domain.Proforma;

/// <summary>The eight production modules (WPF ProductionStage — int
/// codes are the stable contract, names are the stored keys).</summary>
public static class ProductionStages
{
    public const int Cutting = 1;
    public const int HolesCutouts = 2;
    public const int Polish = 3;
    public const int EdgeDeletion = 4;
    public const int Mitring = 5;
    public const int Tempering = 6;
    public const int Pairing = 7;
    public const int DoublingLamination = 8;

    public static readonly IReadOnlyList<string> All = new[]
    {
        "Cutting", "HolesCutouts", "Polish", "EdgeDeletion",
        "Mitring", "Tempering", "Pairing", "DoublingLamination"
    };

    /// <summary>Human label for a stage — shared by all eight module UIs
    /// (WPF StageLabel, verbatim).</summary>
    public static string StageLabel(string stage) => stage switch
    {
        "Cutting" => "Cutting",
        "HolesCutouts" => "Holes & Cutouts",
        "Polish" => "Polish",
        "EdgeDeletion" => "Edge Deletion",
        "Mitring" => "Mitring",
        "Tempering" => "Tempering",
        "Pairing" => "Pairing",
        "DoublingLamination" => "Doubling / Lamination",
        _ => stage
    };

    public static int Code(string stage) => stage switch
    {
        "Cutting" => Cutting,
        "HolesCutouts" => HolesCutouts,
        "Polish" => Polish,
        "EdgeDeletion" => EdgeDeletion,
        "Mitring" => Mitring,
        "Tempering" => Tempering,
        "Pairing" => Pairing,
        "DoublingLamination" => DoublingLamination,
        _ => 0
    };
}

/// <summary>Work-item lifecycle on the factory floor (WPF WorkItemStatus
/// — append-only; 0..2 frozen, new values appended).</summary>
public static class WorkItemStatuses
{
    public const string Pending = "Pending";
    public const string InProgress = "InProgress";
    public const string Completed = "Completed";
    public const string Paused = "Paused";
    public const string Rework = "Rework";
    public const string Rejected = "Rejected";
    public const string Cancelled = "Cancelled";
    public const string QcPending = "QcPending";
    public const string QcFailed = "QcFailed";

    public static readonly IReadOnlyList<string> All = new[]
    {
        Pending, InProgress, Completed, Paused, Rework,
        Rejected, Cancelled, QcPending, QcFailed
    };
}

/// <summary>Production entry types (WPF ProductionEntryType).</summary>
public static class ProductionEntryTypes
{
    public const string Normal = "Normal";
    public const string Breakage = "Breakage";
    public const string Rework = "Rework";
    public const string Foc = "Foc";
    public const string QcPassed = "QcPassed";
    public const string QcFailed = "QcFailed";
    public const string Cancelled = "Cancelled";
    public const string Stage = "Stage";
}

/// <summary>QC outcomes (WPF QcResult).</summary>
public static class QcResults
{
    public const string Pending = "Pending";
    public const string Passed = "Passed";
    public const string Failed = "Failed";
    public const string Rework = "Rework";
}

/// <summary>
/// Resolves the inner (pane 2) cutting size from W1/H1 and W2/H2 (WPF
/// DguDimensionSemantics, verbatim). False for incomplete pairs — the
/// caller must skip the pane and warn, not invent a size.
/// </summary>
public static class DguDimensionSemantics
{
    public static bool TryResolveInnerSize(
        int w1, int h1, int w2, int h2,
        out int widthMm, out int heightMm)
    {
        widthMm = 0;
        heightMm = 0;

        if (w2 == 0 && h2 == 0)
        {
            if (w1 > 0 && h1 > 0)
            {
                widthMm = w1;
                heightMm = h1;
                return true;   // EqualSize
            }
            return false;      // Incomplete
        }

        if (w2 > 0 && h2 > 0)
        {
            widthMm = w2;
            heightMm = h2;
            return true;       // Overlap
        }

        // One axis present, the other zero: 0×500 and 1000×0 are invalid.
        return false;
    }
}

// ── Engine inputs (the SaaS-normalized equivalents of the WPF snapshot
//    models — spec groups from the source PI, Step 6 charge rows) ─────

/// <summary>One Step 5 grid row feeding the engine (GlassItemRow).</summary>
public sealed record ProductionRowInput(
    int SerialNumber,
    string GlassRef,
    int W1, int H1, int W2, int H2,
    int Qty,
    int PerimeterFactor);

/// <summary>One spec group (SpecGroupSnapshot, normalized).</summary>
public sealed record ProductionSpecInput(
    int GroupNumber,
    string SpecName,
    string GlassType,
    IReadOnlyList<ProductionRowInput> Rows);

/// <summary>One active Step 6 predefined charge (PredefinedChargeSnapshot,
/// normalized).</summary>
public sealed record ProductionChargeInput(
    string TemplateKey,
    decimal MetricQty,
    string PolishSubType,
    string CustomPolishDescription,
    IReadOnlyList<int> CheckedSpecGroupNumbers);

/// <summary>One planned stage work item (ProductionWorkItem).</summary>
public sealed record ProductionWorkItemPlan(
    string UniqueKey,
    string JobRef,
    int SpecGroupNumber,
    string SpecName,
    string BuildType,
    int SerialNumber,
    string GlassRef,
    string Position,
    int WidthMm,
    int HeightMm,
    int Qty,
    decimal UnitSqm,
    decimal TotalSqm,
    decimal TotalLm,
    string Treatment,
    string Stage,
    string Status,
    string PredecessorStage,
    string ChargeTemplateKey = "",
    decimal ChargeMetricQty = 0m,
    string ChargeMetricUnit = "",
    string PolishSubTypeLabel = "",
    string Notes = "");

/// <summary>The deterministic job plan (ProductionJobPlan).</summary>
public sealed record ProductionJobPlan(
    string JobRef,
    string CustomerName,
    string ProjectName,
    string Salesman,
    List<ProductionWorkItemPlan> Items,
    List<string> Warnings);

/// <summary>
/// Turns one job order's spec grid + Step 6 charges into a deterministic
/// ProductionJobPlan: every spec row fanned out into stage work items for
/// the eight production modules (WPF ProductionDistributionEngine).
/// </summary>
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

    /// <summary>
    /// Distributes one job's grid + charges into a full plan.
    /// <paramref name="jobRef"/> is the Job Order number.
    /// </summary>
    public static ProductionJobPlan Distribute(
        string jobRef,
        string customerName,
        string projectName,
        string salesman,
        IReadOnlyList<ProductionSpecInput> specs,
        IReadOnlyList<ProductionChargeInput> charges)
    {
        var plan = new ProductionJobPlan(
            jobRef ?? "", customerName ?? "", projectName ?? "", salesman ?? "",
            new List<ProductionWorkItemPlan>(), new List<string>());

        var groups = (specs ?? Array.Empty<ProductionSpecInput>())
            .Where(g => g is not null)
            .OrderBy(g => g.GroupNumber)
            .ToList();

        if (groups.Count == 0)
        {
            plan.Warnings.Add(
                "The invoice has no specification groups (Step 5 is empty).");
            return plan;
        }

        // Step 6 pre-scan: assembly notes per spec.
        var assemblyNotes = CollectAssemblyNotes(charges, groups, plan.Warnings);
        var activeCharges = (charges ?? Array.Empty<ProductionChargeInput>())
            .Where(c => c is not null)
            .ToList();

        var occurrenceCache =
            new Dictionary<int, IReadOnlyList<PaneOccurrence>>();

        foreach (var group in groups)
        {
            DistributeGroup(plan, group, assemblyNotes, occurrenceCache);
        }

        DistributeChargeItems(plan, groups, activeCharges, occurrenceCache);

        plan.Items = plan.Items
            .OrderBy(i => i.SpecGroupNumber)
            .ThenBy(i => i.SerialNumber)
            .ThenBy(ProductionStages.Code(i.Stage))
            .ThenBy(i => i.Position, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return plan;
    }

    // ── build type ──────────────────────────────────────────────────

    /// <summary>
    /// Resolves the physical build for a spec group. Precedence: the
    /// operator's Step 5 glass type (the explicit build chosen at the
    /// Step 4/5 handoff — the ground truth for what the cutting list
    /// actually cuts), then the spec-name grammar. Never throws — an
    /// unreadable name degrades to SGU with a warning.
    /// </summary>
    public static string ResolveBuildType(ProductionSpecInput group)
    {
        var fromGlassType = NormalizeGlassTypeToBuildType(group.GlassType);
        if (fromGlassType is not null)
        {
            return fromGlassType;
        }

        return InferBuildTypeFromName(group.SpecName);
    }

    /// <summary>
    /// Maps a Step 5 glass type ("SGU", "DGU", "LAM", "DGU+LAM") onto the
    /// internal build-type string. Null when blank or unsupported.
    /// </summary>
    private static string? NormalizeGlassTypeToBuildType(string? glassType)
    {
        if (string.IsNullOrWhiteSpace(glassType))
        {
            return null;
        }

        var t = glassType.Trim().Replace("+", "").Replace(" ", "")
            .ToLowerInvariant();
        return t switch
        {
            "sgu" => "Sgu",
            "dgu" => "Dgu",
            "lam" => "Lam",
            "dgulam" => "DguLam",
            _ => null
        };
    }

    /// <summary>
    /// Grammar-based inference over the four supported build families
    /// (SCOPE LOCK: no triple glazing). Two panes + interlayer = LAM;
    /// three or more panes = DGU+LAM hybrid; anything else with a second
    /// pane = DGU; single pane = SGU.
    /// </summary>
    public static string InferBuildTypeFromName(string? specName)
    {
        var panes = SpecGrammar.ResolveAllPanes(specName).Count;
        var hasInterlayer = SpecGrammar.Interlayer(specName).Success;

        if (panes <= 1)
        {
            return "Sgu";
        }

        if (panes == 2)
        {
            return hasInterlayer ? "Lam" : "Dgu";
        }

        return hasInterlayer ? "DguLam" : "Dgu";
    }

    // ── per-spec distribution ───────────────────────────────────────

    private static void DistributeGroup(
        ProductionJobPlan plan,
        ProductionSpecInput group,
        Dictionary<int, List<string>> assemblyNotes,
        Dictionary<int, IReadOnlyList<PaneOccurrence>> occurrenceCache)
    {
        var buildType = ResolveBuildType(group);
        var occurrences = ResolvePaneOccurrences(
            buildType, group, plan.Warnings);
        occurrenceCache[group.GroupNumber] = occurrences;

        // An explicit Step 5 glass type that CONTRADICTS the spec name
        // silently adds or removes whole panes — the explicit selection
        // still wins, but the mismatch must be visible.
        var inferred = InferBuildTypeFromName(group.SpecName);
        if (!string.Equals(inferred, buildType, StringComparison.Ordinal))
        {
            plan.Warnings.Add(
                $"Spec {group.GroupNumber}: glass type " +
                $"'{(group.GlassType ?? "").Trim()}' selected but the spec " +
                $"name suggests {inferred} — check Step 5 (a wrong glass " +
                "type adds or removes whole panes from the cutting list).");
        }

        var rows = (group.Rows ?? Array.Empty<ProductionRowInput>())
            .Where(r => r is not null && !IsEmptyRow(r))
            .OrderBy(r => r.SerialNumber)
            .ToList();

        if (rows.Count == 0)
        {
            return;   // empty group contributes nothing
        }

        var multiPane = occurrences.Count > 1;
        var notesForSpec = assemblyNotes.TryGetValue(
            group.GroupNumber, out var n) ? n : new List<string>();

        foreach (var row in rows)
        {
            foreach (var occ in occurrences)
            {
                int w;
                int h;
                if (occ.Slot == PaneSlot.Pane1)
                {
                    w = row.W1;
                    h = row.H1;
                }
                else if (!DguDimensionSemantics.TryResolveInnerSize(
                             row.W1, row.H1, row.W2, row.H2, out w, out h))
                {
                    plan.Warnings.Add(
                        $"Spec {group.GroupNumber}: row {row.SerialNumber} " +
                        $"({SafeRef(row)}) has an incomplete W2/H2 size " +
                        $"({row.W2}×{row.H2}) — the {occ.Position} pane was skipped.");
                    continue;
                }

                if (w <= 0 || h <= 0)
                {
                    plan.Warnings.Add(
                        $"Spec {group.GroupNumber}: row {row.SerialNumber} " +
                        $"({SafeRef(row)}) has no " +
                        $"{(occ.Slot == PaneSlot.Pane1 ? "W1/H1" : "W2/H2")} " +
                        "size — the pane was skipped.");
                    continue;
                }

                // Legacy 0→1 path is ONLY persisted snapshot compat.
                if (row.Qty <= 0)
                {
                    plan.Warnings.Add(
                        $"Spec {group.GroupNumber}: row {row.SerialNumber} " +
                        $"({SafeRef(row)}) has no quantity — produced as 1 " +
                        "piece. Fix the Step 5 qty and re-release.");
                }
                var qty = row.Qty <= 0 ? 1 : row.Qty;
                var unitSqm = Pricing.SqmCalculator.CalculatePanel(w, h);
                var totalSqm = Round3(unitSqm * qty);
                var perimeterFactor = row.PerimeterFactor > 0
                    ? row.PerimeterFactor : 2;
                var totalLm = Round3(
                    ((decimal)w + h) / 1000m * perimeterFactor * qty);

                plan.Items.Add(BaseItem(plan.JobRef, group, row, buildType,
                    occ, w, h, qty, unitSqm, totalSqm, totalLm,
                    "Cutting", predecessor: ""));

                if (occ.RequiresTempering)
                {
                    var tempering = BaseItem(plan.JobRef, group, row, buildType,
                        occ, w, h, qty, unitSqm, totalSqm, totalLm,
                        "Tempering", predecessor: ProductionStages.StageLabel("Cutting"));
                    tempering = tempering with
                    {
                        Notes = $"{occ.Treatment.ToUpperInvariant()} CYCLE"
                    };
                    plan.Items.Add(tempering);
                }
            }

            if (multiPane)
            {
                var qty = row.Qty <= 0 ? 1 : row.Qty;

                var pairing = BaseItem(plan.JobRef, group, row, buildType,
                    new PaneOccurrence("Unit", PaneSlot.Pane1, null),
                    row.W1, row.H1, qty, 0m, 0m, 0m,
                    "Pairing", predecessor: ProductionStages.StageLabel("Cutting"));
                pairing = pairing with { Notes = $"{occurrences.Count}-PANE UNIT" };
                plan.Items.Add(pairing);

                var assembly = BaseItem(plan.JobRef, group, row, buildType,
                    new PaneOccurrence("Unit", PaneSlot.Pane1, null),
                    row.W1, row.H1, qty, 0m, 0m, 0m,
                    "DoublingLamination",
                    predecessor: ProductionStages.StageLabel("Pairing"));
                assembly = assembly with
                {
                    Notes = BuildAssemblyNotes(group, buildType, notesForSpec)
                };
                plan.Items.Add(assembly);
            }
        }

        // SGU sanity: stray W2/H2 on single-pane specs.
        if (!multiPane && rows.Any(r => r.W2 > 0 || r.H2 > 0))
        {
            plan.Warnings.Add(
                $"Spec {group.GroupNumber} is a single glass unit (SGU) but " +
                "some rows carry W2/H2 — those dimensions are ignored for " +
                "production.");
        }
    }

    private static bool IsEmptyRow(ProductionRowInput row) =>
        string.IsNullOrWhiteSpace(row.GlassRef)
        && row.W1 <= 0 && row.H1 <= 0 && row.W2 <= 0 && row.H2 <= 0;

    private static string SafeRef(ProductionRowInput row) =>
        string.IsNullOrWhiteSpace(row.GlassRef) ? "(no ref)" : row.GlassRef;

    private static decimal Round3(decimal value) =>
        Math.Round(value, 3, MidpointRounding.AwayFromZero);

    // ── pane occurrences ────────────────────────────────────────────

    /// <summary>Which Step 5 dimension pair a pane occupies.</summary>
    public enum PaneSlot
    {
        Pane1 = 1,   // W1/H1 — every build's first pane
        Pane2 = 2    // W2/H2 — DGU/LAM inner pane; DGU+LAM laminated pair
    }

    /// <summary>
    /// One physical cut: label + which dimension pair feeds it. Public so
    /// the Optimization module reuses the SAME pane routing as the floor
    /// — position, glass composition and treatment can never drift apart.
    /// </summary>
    public sealed record PaneOccurrence(
        string Position, PaneSlot Slot, SpecPane? Pane)
    {
        /// <summary>Per-pane glass composition, e.g.
        /// "6mm hp neutral 70 low e FT glass". Empty when the spec name
        /// did not parse.</summary>
        public string Composition => Pane is null
            ? string.Empty : PaneDisplayName(Pane);

        /// <summary>FT / Annealed / Heat Strengthened / Heat Soaked / "".</summary>
        public string Treatment => Pane?.Treatment ?? string.Empty;

        public bool RequiresTempering => Pane?.RequiresTempering ?? false;
    }

    /// <summary>
    /// Canonical per-pane glass label — the merge key the optimizer
    /// groups by, so two specs sharing the same outer pane collapse into
    /// ONE cutting group.
    /// </summary>
    public static string PaneDisplayName(SpecPane pane)
    {
        var color = (pane.Color ?? "").Trim();
        var treat = (pane.Treatment ?? "").Trim();
        var name = $"{pane.ThicknessMm}mm {color}".Trim();
        if (treat.Length > 0)
        {
            name += " " + treat;
        }

        return name + " glass";
    }

    /// <summary>
    /// Maps a build type to its physical cuts, pairing the parsed panes
    /// (with treatments) to the grid's dimension pairs. When the name
    /// does not parse, occurrences degrade to treatment-less slots so the
    /// job still flows — with a warning.
    /// </summary>
    public static IReadOnlyList<PaneOccurrence> ResolvePaneOccurrences(
        string buildType, ProductionSpecInput group, IList<string>? warnings = null)
    {
        var panes = new List<SpecPane>();
        try
        {
            panes = SpecGrammar.ResolveAllPanes(group.SpecName).ToList();
        }
        catch
        {
            // parse failure degrades below
        }

        if (panes.Count == 0 && buildType != "Sgu")
        {
            warnings?.Add(
                $"Spec {group.GroupNumber} ('{group.SpecName ?? ""}') could " +
                "not be parsed — panes are routed without treatment info " +
                "(assume ANNEALED unless told otherwise).");
        }

        SpecPane? At(int i) => i < panes.Count ? panes[i] : null;

        switch (buildType)
        {
            case "Dgu":
                // Outer = first pane, Inner = LAST pane (the grammar's own
                // rule) — so even an unusual multi-token name keeps the
                // correct two physical cuts on the supported grid.
                return new List<PaneOccurrence>
                {
                    new("Outer", PaneSlot.Pane1, At(0)),
                    new("Inner", PaneSlot.Pane2, At(panes.Count - 1))
                };

            case "Lam":
                return new List<PaneOccurrence>
                {
                    new("Lam Outer", PaneSlot.Pane1, At(0)),
                    new("Lam Inner", PaneSlot.Pane2, At(panes.Count - 1))
                };

            case "DguLam":
                return new List<PaneOccurrence>
                {
                    new("Outer", PaneSlot.Pane1, At(0)),
                    new("Lam Outer", PaneSlot.Pane2, At(1)),
                    new("Lam Inner", PaneSlot.Pane2, At(2))
                };

            default: // Sgu
                return new List<PaneOccurrence>
                {
                    new("Glass", PaneSlot.Pane1, At(0))
                };
        }
    }

    // ── Step 6 charge distribution ──────────────────────────────────

    private static Dictionary<int, List<string>> CollectAssemblyNotes(
        IReadOnlyList<ProductionChargeInput> charges,
        List<ProductionSpecInput> groups,
        IList<string> warnings)
    {
        var result = new Dictionary<int, List<string>>();
        foreach (var charge in charges ?? Array.Empty<ProductionChargeInput>())
        {
            if (charge is null
                || !AssemblyChargeNotes.TryGetValue(
                    charge.TemplateKey ?? "", out var note))
            {
                continue;
            }

            foreach (var specNo in TargetSpecs(charge, groups))
            {
                if (!result.TryGetValue(specNo, out var list))
                {
                    list = new List<string>();
                    result[specNo] = list;
                }

                if (!list.Contains(note))
                {
                    list.Add(note);
                }
            }
        }

        return result;
    }

    private static void DistributeChargeItems(
        ProductionJobPlan plan,
        List<ProductionSpecInput> groups,
        IReadOnlyList<ProductionChargeInput> charges,
        Dictionary<int, IReadOnlyList<PaneOccurrence>> occurrenceCache)
    {
        foreach (var charge in charges ?? Array.Empty<ProductionChargeInput>())
        {
            var key = charge.TemplateKey ?? "";

            var stage =
                HoleChargeKeys.Contains(key) ? "HolesCutouts"
                : key is "POLISH" or "POLISH_MATCHED_LAM" or "EDGE_GRINDING"
                    ? "Polish"
                : key == "MITRING" ? "Mitring"
                : null;

            if (stage is null)
            {
                if (key == "SANDBLASTING")
                {
                    plan.Warnings.Add(
                        "Sandblasting is charged on this job but has no " +
                        "dedicated production module yet — track it " +
                        "manually on the floor.");
                }
                continue;   // DELIVERY / U_INSERT / argon etc. are notes
            }

            var targets = TargetSpecs(charge, groups).ToList();
            if (targets.Count == 0)
            {
                plan.Warnings.Add(
                    $"Step 6 charge '{ChargeDisplayName(key)}' is active " +
                    "but targets no spec group.");
                continue;
            }

            // PIECE FLOW: charge work fans out per pane, exactly like
            // cutting/tempering — so cut pieces physically walk through
            // Holes / Polish / Mitring on their way to tempering.
            foreach (var specNo in targets)
            {
                var group = groups.First(g => g.GroupNumber == specNo);
                var buildType = ResolveBuildType(group);
                var metricUnit = stage == "HolesCutouts" ? "QTY" : "LM";

                if (!occurrenceCache.TryGetValue(specNo, out var occurrences)
                    || occurrences.Count == 0)
                {
                    continue;
                }

                var rows = (group.Rows ?? Array.Empty<ProductionRowInput>())
                    .Where(r => r is not null && !IsEmptyRow(r))
                    .OrderBy(r => r.SerialNumber)
                    .ToList();

                foreach (var row in rows)
                {
                    foreach (var occ in occurrences)
                    {
                        int w;
                        int h;
                        if (occ.Slot == PaneSlot.Pane1)
                        {
                            w = row.W1;
                            h = row.H1;
                        }
                        else if (!DguDimensionSemantics.TryResolveInnerSize(
                                     row.W1, row.H1, row.W2, row.H2, out w, out h))
                        {
                            // Incomplete W2/H2 (0×500): cutting already
                            // skipped this pane. Do not invent W1×H1 for
                            // holes/polish/mitring.
                            continue;
                        }

                        if (w <= 0 || h <= 0)
                        {
                            continue;
                        }

                        var qty = row.Qty <= 0 ? 1 : row.Qty;
                        var unitSqm = Pricing.SqmCalculator.CalculatePanel(w, h);
                        var totalSqm = Round3(unitSqm * qty);
                        var perimeterFactor = row.PerimeterFactor > 0
                            ? row.PerimeterFactor : 2;
                        var totalLm = stage == "HolesCutouts"
                            ? 0m
                            : Round3(((decimal)w + h) / 1000m * perimeterFactor * qty);

                        // Charge identity rides in the key — two charges
                        // in the same stage (e.g. FAN_HOLES + CUTOUT) must
                        // stay distinct items.
                        var item = BaseItem(plan.JobRef, group, row, buildType,
                            occ, w, h, qty, unitSqm, totalSqm, totalLm, stage,
                            predecessor: ProductionStages.StageLabel("Cutting"))
                            with
                            {
                                ChargeTemplateKey = key,
                                ChargeMetricQty = charge.MetricQty,
                                ChargeMetricUnit = metricUnit,
                                Notes = ChargeNote(key, targets)
                            };

                        if (stage == "Polish")
                        {
                            item = item with
                            {
                                PolishSubTypeLabel = key == "EDGE_GRINDING"
                                    ? "EDGE GRINDING (PRE-POLISH)"
                                    : PolishLabel(charge)
                            };
                        }

                        plan.Items.Add(item);
                    }
                }
            }
        }
    }

    private static IEnumerable<int> TargetSpecs(
        ProductionChargeInput charge, List<ProductionSpecInput> groups)
    {
        var checkedSpecs = charge.CheckedSpecGroupNumbers
            ?? Array.Empty<int>();
        var all = groups.Select(g => g.GroupNumber).ToList();

        if (checkedSpecs.Count == 0)
        {
            return all;   // empty = all specs (Chat 20 contract)
        }

        return checkedSpecs.Where(no => all.Contains(no))
            .OrderBy(no => no).Distinct();
    }

    private static string ChargeNote(string key, List<int> targets)
    {
        var baseNote = ChargeDisplayName(key);
        if (targets.Count > 1)
        {
            baseNote +=
                $" — QTY SHARED ACROSS SPECS {string.Join(", ", targets)} " +
                "(Step 6 level)";
        }

        return baseNote;
    }

    private static string ChargeDisplayName(string key) =>
        (key ?? "").Replace('_', ' ');

    private static string PolishLabel(ProductionChargeInput charge)
    {
        var sub = charge.PolishSubType ?? "";
        var label = string.IsNullOrWhiteSpace(sub) ? "POLISH" : sub;

        if (charge.TemplateKey == "POLISH_MATCHED_LAM")
        {
            label = $"SAME-GLASS LAM 2× · {label}";
        }

        if (!string.IsNullOrWhiteSpace(charge.CustomPolishDescription))
        {
            label += $" · {charge.CustomPolishDescription}";
        }

        return label;
    }

    private static string BuildAssemblyNotes(
        ProductionSpecInput group, string buildType, List<string> chargeNotes)
    {
        var notes = new List<string>();
        var name = group.SpecName ?? "";

        var spacer = SpecGrammar.Spacer(name);
        if (spacer.Success)
        {
            notes.Add($"SPACER {spacer.Groups["mm"].Value}MM");
        }

        var interlayer = SpecGrammar.Interlayer(name);
        if (interlayer.Success)
        {
            var colour = (interlayer.Groups["color"].Value ?? "").Trim();
            notes.Add(
                $"{interlayer.Groups["type"].Value.ToUpperInvariant()} " +
                $"{interlayer.Groups["mm"].Value}MM"
                + (colour.Length > 0 ? $" {colour.ToUpperInvariant()}" : ""));
        }

        notes.AddRange(chargeNotes);

        var mode = buildType switch
        {
            "Lam" => "LAMINATION",
            "DguLam" => "DGU + LAMINATION (HYBRID)",
            _ => "DGU ASSEMBLY"
        };

        return notes.Count == 0 ? mode : $"{mode} · {string.Join(" · ", notes)}";
    }

    // ── work item factory ───────────────────────────────────────────

    private static ProductionWorkItemPlan BaseItem(
        string jobRef,
        ProductionSpecInput group,
        ProductionRowInput row,
        string buildType,
        PaneOccurrence occ,
        int widthMm, int heightMm, int qty,
        decimal unitSqm, decimal totalSqm, decimal totalLm,
        string stage, string predecessor) =>
        new(
            UniqueKey: BuildKey(jobRef, group.GroupNumber, row.SerialNumber,
                occ.Position, stage, chargeKey: ""),
            JobRef: jobRef,
            SpecGroupNumber: group.GroupNumber,
            SpecName: group.SpecName ?? "",
            BuildType: buildType,
            SerialNumber: row.SerialNumber,
            GlassRef: row.GlassRef ?? "",
            Position: occ.Position,
            WidthMm: widthMm,
            HeightMm: heightMm,
            Qty: qty,
            UnitSqm: unitSqm,
            TotalSqm: totalSqm,
            TotalLm: totalLm,
            Treatment: occ.Treatment,
            Stage: stage,
            Status: WorkItemStatuses.Pending,
            PredecessorStage: predecessor);

    /// <summary>
    /// Deterministic identity — a re-release produces the SAME keys, so
    /// statuses survive.
    /// </summary>
    public static string BuildKey(
        string jobRef, int specNo, int serial, string position,
        string stage, string chargeKey)
    {
        var key =
            $"{jobRef}|{specNo}|{serial}|{position}|{ProductionStages.Code(stage)}";
        return string.IsNullOrEmpty(chargeKey) ? key : $"{key}|CHG:{chargeKey}";
    }
}

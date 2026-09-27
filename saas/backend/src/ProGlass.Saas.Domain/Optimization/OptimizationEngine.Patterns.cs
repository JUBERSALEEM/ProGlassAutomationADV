// ═══════════════════════════════════════════════════════════════════════
// PORTED from the WPF application (read 2026-09-27), Phase 25c — the
// repeated-pattern family (the quantity-aware industrial engine):
//   Services/Optimization/GuillotinePacker.RepeatedPatterns.cs (264
//     lines — PackIndustrialPatterns, RunRepeatedPatterns, the type
//     grouping, the pricing loop, the LP rounds, pattern expansion,
//     the leftover strip tail)
//   Services/Optimization/GuillotinePacker.RepeatPricing.cs (152 —
//     RepeatPlacement/RepeatOrientation/RepeatPattern/RepeatStrip,
//     PriceRepeatPatterns: the exact-integer-mm strip DP + across DP)
//   Services/Optimization/GuillotinePacker.RepeatMaster.cs (313 —
//     RepeatMaster: the managed revised simplex over equality demand
//     rows + finite-rack slack rows with artificial shortage columns;
//     PatternFits/ConsumePattern; IntegerRepeatPlan with greedy
//     completion, diversification trials, reopen passes and tie-seed
//     alternatives; RepairIntegerPatterns: the dual-bounded DFS)
//   Services/Optimization/GuillotinePacker.RepeatInteger.cs (134 —
//     RepeatBranch, BranchRepeatMaster/Core: the bounded integer
//     master over pattern MULTIPLICITIES)
//   Services/Optimization/GuillotinePacker.RepeatTail.cs (71 —
//     FinishRepeatRemainder: run the small-candidate engines on the
//     integer remainder and reabsorb their sheets as patterns)
//
// NOT ported (documented): PieceCandidateIndex.cs has no callers in
// the WPF (dead code, like GuillotinePacker.Smart.cs — see mapping
// §1.6c); Remnants.cs was already ported with the Phase 11 leftover
// derivation.
//
// SaaS adaptations (mechanical, documented): PlacedPiece is a
// positional record; CultureInvariant note formatting kept as the
// WPF's default interpolation.
// ═══════════════════════════════════════════════════════════════════════

using System.Diagnostics;

namespace ProGlass.Saas.Domain.Optimization;

public static partial class GuillotinePacker
{
    private static OptimizationResult PackIndustrialPatterns(IReadOnlyList<CutPiece> pieces,
        IReadOnlyList<StockSize> stocks, MarginProfile margins, RotationPolicy rotation, int effort,
        CancellationToken token, IProgress<OptimizeProgress>? progress)
    {
        token.ThrowIfCancellationRequested();
        progress?.Report(OptimizeProgress.Of("repeat", 0, 100, "Aggregating whole-order repeated sizes"));
        var best = CheckCandidate(RunRepeatedPatterns(pieces, stocks, margins, rotation, 1, token), pieces, margins, token);
        foreach (bool transpose in new[] { false, true })
        {
            progress?.Report(OptimizeProgress.Of("capacity", transpose ? 65 : 45, 100, "Comparing scalable strip floor"));
            best = Better(best, CheckCandidate(RunCapacityStrips(pieces, stocks, margins, rotation, transpose, token), pieces, margins, token))!;
        }
        best = ImprovePatterns(best, pieces, stocks, margins, rotation, 1, Math.Clamp(effort, 1, 5), token);
        if (effort > 1)
        {
            progress?.Report(OptimizeProgress.Of("repeat", 80, 100, "Deepening whole-order pattern counts"));
            best = Better(best, CheckCandidate(RunRepeatedPatterns(pieces, stocks, margins, rotation, effort, token), pieces, margins, token))!;
        }
        best.ScaleNotes.Add("Industrial quantity route: count-based pattern master, row/column strip floors and bounded weak-sheet repair. Whole-order quadratic shelf/beam passes are not run at this scale; no panes are truncated.");
        progress?.Report(OptimizeProgress.Of("done", 100, 100, $"{best.SheetsUsed:N0} sheets; {best.Unplaced.Count:N0} unplaced"));
        return best;
    }

    private static bool RepeatPatternSeparationsValid(RepeatPlacement[] parts, MarginProfile margins)
    {
        for (int i = 0; i < parts.Length; i++)
            for (int j = i + 1; j < parts.Length; j++)
            {
                var a = parts[i]; var b = parts[j];
                bool x = a.X < b.X + b.W && b.X < a.X + a.W;
                bool y = a.Y < b.Y + b.H && b.Y < a.Y + a.H;
                if (x && y) return false;
                if (!x && !y) continue;
                int gap = x ? Math.Max(b.Y - a.Y - a.H, a.Y - b.Y - b.H)
                    : Math.Max(b.X - a.X - a.W, a.X - b.X - b.W);
                if (gap < margins.KerfMm || (gap > margins.KerfMm && gap - margins.KerfMm < margins.MinBreakoutMm)) return false;
            }
        return true;
    }

    private const int RepeatTypeWindow = 128;
    private const int RepeatColumnLimit = 8192;

    private sealed class RepeatType
    {
        public int W, H, Thickness, Next;
        public bool Rotate;
        public string Color = "", Material = "";
        public List<CutPiece> Pieces = new();
        public int Remaining => Pieces.Count - Next;
    }

    /// <summary>
    /// Quantity-aware cutting stock: price guillotine patterns, balance their
    /// repeat counts against whole-order demand, then expand original pane
    /// identities exactly once. Search complexity is governed by distinct
    /// sizes, not expanded order quantities. No fixture/pattern catalogue.
    /// </summary>
    internal static OptimizationResult RunRepeatedPatterns(IReadOnlyList<CutPiece> pieces,
        IReadOnlyList<StockSize> stocks, MarginProfile margins, RotationPolicy rotation,
        int effort, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var result = new OptimizationResult { Rotation = rotation, Strategy = "REPEATED PATTERN MASTER" };
        if (pieces.Count == 0) return result;
        var watch = Stopwatch.StartNew();
        var budget = TimeSpan.FromSeconds(20 + Math.Clamp(effort, 1, 5) * 10);
        bool Expired() => watch.Elapsed >= budget;
        string Normalize(string s) => (s ?? "").Trim().ToUpperInvariant();
        var groups = new Dictionary<(int, int, bool, int, string, string), RepeatType>();
        foreach (var p in pieces)
        {
            token.ThrowIfCancellationRequested();
            var key = (p.WidthMm, p.HeightMm, rotation == RotationPolicy.Free90 && p.AllowRotate,
                p.ThicknessMm, Normalize(p.Color), Normalize(p.MaterialKey));
            if (!groups.TryGetValue(key, out var t))
            {
                t = new RepeatType { W = key.Item1, H = key.Item2, Rotate = key.Item3,
                    Thickness = key.Item4, Color = key.Item5, Material = key.Item6 };
                groups.Add(key, t);
            }
            t.Pieces.Add(p);
        }
        bool priceWindows = pieces.Count > groups.Count &&
            (groups.Count <= RepeatTypeWindow || (long)pieces.Count >= 2L * groups.Count);
        var used = new int[stocks.Count]; int windows = 0, columnCount = 0;
        bool limited = false;
        foreach (var material in groups.Values.GroupBy(t => (t.Thickness, t.Color, t.Material)))
        {
            if (!priceWindows) break;
            var sorted = material.OrderByDescending(t => t.Pieces.Count).ThenBy(t => t.W).ThenBy(t => t.H)
                .ThenBy(t => t.Rotate).ToList();
            for (int offset = 0; offset < sorted.Count && !Expired(); offset += RepeatTypeWindow)
            {
                token.ThrowIfCancellationRequested();
                var types = sorted.Skip(offset).Take(RepeatTypeWindow).OrderBy(t => t.W).ThenBy(t => t.H)
                    .ThenBy(t => t.Rotate).ToList();
                var rackIds = Enumerable.Range(0, stocks.Count).Where(i =>
                    stocks[i].MaxSheets >= 0 && (stocks[i].MaxSheets == 0 || used[i] < stocks[i].MaxSheets)
                    && (stocks[i].ThicknessMm == 0 || types[0].Thickness == 0 || stocks[i].ThicknessMm == types[0].Thickness)
                    && (string.IsNullOrWhiteSpace(stocks[i].Color) || types[0].Color.Length == 0 || Normalize(stocks[i].Color) == types[0].Color)
                    && (string.IsNullOrWhiteSpace(stocks[i].MaterialKey) || types[0].Material.Length == 0 || Normalize(stocks[i].MaterialKey) == types[0].Material))
                    .OrderBy(i => (long)stocks[i].WidthMm * stocks[i].HeightMm).ThenBy(i => i).Take(32).ToArray();
                if (rackIds.Length == 0) continue;
                var demand = types.Select(t => t.Remaining).ToArray();
                var available = rackIds.Select(i => stocks[i].MaxSheets == 0 ? int.MaxValue : stocks[i].MaxSheets - used[i]).ToArray();
                var master = new RepeatMaster(demand, available, (double)pieces.Count + 1, token, Expired);
                var patterns = new List<RepeatPattern>(); var seen = new HashSet<string>(StringComparer.Ordinal);
                void Add(RepeatPattern p)
                {
                    if (patterns.Count >= RepeatColumnLimit) { limited = true; return; }
                    // Small quantities can use a subset of a priced pattern.
                    // Removing panes enlarges waste, never shrinks separation.
                    var taken = new int[types.Count];
                    p.Parts = p.Parts.Where(part => taken[part.Type]++ < demand[part.Type]).ToArray();
                    if (p.Parts.Length == 0 || !RepeatPatternSeparationsValid(p.Parts, margins)) return;
                    p.Counts = p.Parts.GroupBy(part => part.Type).Select(g => (g.Key, g.Count())).OrderBy(c => c.Key).ToArray();
                    string key = p.Stock + "|" + string.Join(",", p.Counts.Select(c => c.Type + ":" + c.Count));
                    if (!seen.Add(key)) return;
                    p.Area = p.Parts.Sum(part => (long)part.W * part.H);
                    master.Add(p, patterns.Count); patterns.Add(p);
                }
                // Single-pane columns provide a feasible completion whenever
                // rack supply permits; orientation/stock/material are explicit.
                for (int s = 0; s < rackIds.Length; s++)
                {
                    var st = stocks[rackIds[s]];
                    long uw = (long)st.WidthMm - margins.LeftMm - margins.RightMm;
                    long uh = (long)st.HeightMm - margins.TopMm - margins.BottomMm;
                    for (int t = 0; t < types.Count; t++)
                    {
                        var type = types[t]; if (type.W <= 0 || type.H <= 0) continue;
                        if (type.W <= uw && type.H <= uh)
                            Add(new RepeatPattern { Stock = s, Parts = new[] { new RepeatPlacement(t, 0, 0, type.W, type.H) } });
                        else if (type.Rotate && type.H <= uw && type.W <= uh)
                            Add(new RepeatPattern { Stock = s, Parts = new[] { new RepeatPlacement(t, 0, 0, type.H, type.W) } });
                    }
                }
                void Price(double[] values)
                {
                    // A common cut uses one kerf; a separated waste strip
                    // uses two kerfs plus the configured minimum breakout.
                    long separated = (long)margins.MinBreakoutMm + 2L * margins.KerfMm;
                    if (margins.KerfMm < 0 || margins.MinBreakoutMm < 0 || separated > RepeatAxisLimit) return;
                    foreach (int gap in new[] { (int)separated, margins.KerfMm }.Distinct())
                        for (int s = 0; s < rackIds.Length && !Expired(); s++)
                            foreach (bool transpose in new[] { false, true })
                                PriceRepeatPatterns(types, stocks[rackIds[s]], s, margins, values, gap, transpose, Add, token, Expired);
                }
                Price(types.Select(t => (double)t.W * t.H / 1000000d).ToArray());
                Price(Enumerable.Repeat(1d, types.Count).ToArray());
                int rounds = 0;
                for (; rounds < 48 + effort * 8 && !Expired() && patterns.Count < RepeatColumnLimit; rounds++)
                {
                    master.Solve(); int old = patterns.Count;
                    Price(master.Prices);
                    if (old == patterns.Count) break;
                }
                master.Solve();
                // Integer remainders need partially used versions of priced
                // patterns: a pattern requiring twelve identical panes must
                // not strand the last five onto five singleton sheets.
                var residual = (int[])demand.Clone(); var residualRacks = (int[])available.Clone();
                var values = master.PatternValues(patterns.Count);
                for (int i = 0; i < patterns.Count; i++)
                {
                    int count = (int)Math.Min(int.MaxValue, Math.Floor(values[i] + 1e-7));
                    count = Math.Min(count, PatternFits(patterns[i], residual, residualRacks));
                    ConsumePattern(patterns[i], count, residual, residualRacks);
                }
                foreach (var p in patterns.ToArray())
                {
                    token.ThrowIfCancellationRequested(); if (Expired()) break;
                    var remaining = (int[])residual.Clone();
                    var subset = p.Parts.Where(part => remaining[part.Type]-- > 0).ToArray();
                    if (subset.Length > 0 && subset.Length < p.Parts.Length)
                        Add(new RepeatPattern { Stock = p.Stock, Parts = subset });
                }
                master.Solve();
                var repeats = IntegerRepeatPlan(patterns, master, demand, available, token, Expired, effort);
                repeats = FinishRepeatRemainder(types, patterns, master, demand, available, stocks, rackIds,
                    margins, rotation, repeats, token, Expired);
                result.ScaleNotes.Add($"Pattern window {windows + 1}: restricted LP objective {master.Objective:F3} (includes shortage penalties); integer pattern repeats {repeats.Sum():N0} (not a global optimum certificate).");
                for (int i = 0; i < repeats.Length; i++)
                {
                    var pattern = patterns[i]; int rack = rackIds[pattern.Stock];
                    for (int repeat = 0; repeat < repeats[i]; repeat++)
                    {
                        token.ThrowIfCancellationRequested();
                        var sheet = new SheetLayout { SheetNo = result.Sheets.Count + 1, Stock = stocks[rack],
                            UsableX = margins.LeftMm, UsableY = margins.TopMm,
                            UsableWidth = stocks[rack].WidthMm - margins.LeftMm - margins.RightMm,
                            UsableHeight = stocks[rack].HeightMm - margins.TopMm - margins.BottomMm };
                        foreach (var p in pattern.Parts)
                        {
                            var type = types[p.Type]; var piece = type.Pieces[type.Next++];
                            sheet.Pieces.Add(new PlacedPiece(piece, margins.LeftMm + p.X, margins.TopMm + p.Y,
                                p.W, p.H, p.W != type.W || p.H != type.H));
                        }
                        result.Sheets.Add(sheet); used[rack]++;
                    }
                }
                windows++; columnCount += patterns.Count;
                limited |= master.Limited || rounds >= 48 + effort * 8;
            }
        }
        // Every untouched pane survives search limits. Repair with the
        // scalable strip constructor using ONLY remaining inventory.
        foreach (var material in groups.Values.GroupBy(t => (t.Thickness, t.Color, t.Material)))
        {
            token.ThrowIfCancellationRequested();
            var left = material.SelectMany(t => t.Pieces.Skip(t.Next)).ToList();
            if (left.Count == 0) continue;
            var allowed = new List<StockSize>(); var original = new Dictionary<StockSize, int>();
            foreach (int i in Enumerable.Range(0, stocks.Count))
            {
                var st = stocks[i]; var type = material.First();
                if (st.MaxSheets < 0 || (st.MaxSheets > 0 && used[i] >= st.MaxSheets)
                    || (st.ThicknessMm > 0 && type.Thickness > 0 && st.ThicknessMm != type.Thickness)
                    || (!string.IsNullOrWhiteSpace(st.Color) && type.Color.Length > 0 && Normalize(st.Color) != type.Color)
                    || (!string.IsNullOrWhiteSpace(st.MaterialKey) && type.Material.Length > 0 && Normalize(st.MaterialKey) != type.Material)) continue;
                var copy = st.WithMaxSheets(st.MaxSheets == 0 ? 0 : st.MaxSheets - used[i]);
                allowed.Add(copy); original.Add(copy, i);
            }
            if (allowed.Count == 0) { result.Unplaced.AddRange(left); continue; }
            var tail = RunCapacityStrips(left, allowed, margins, rotation, false, token);
            foreach (var sheet in tail.Sheets)
            {
                int i = original[sheet.Stock]; used[i]++;
                var copy = new SheetLayout { SheetNo = result.Sheets.Count + 1, Stock = stocks[i],
                    UsableX = sheet.UsableX, UsableY = sheet.UsableY, UsableWidth = sheet.UsableWidth, UsableHeight = sheet.UsableHeight };
                copy.Pieces.AddRange(sheet.Pieces); result.Sheets.Add(copy);
            }
            result.Unplaced.AddRange(tail.Unplaced);
        }
        if (!priceWindows) result.ScaleNotes.Add("High-diversity input: repeated-pattern pricing was bypassed because most sizes occur once; scalable strip and existing candidate searches remain available.");
        result.ScaleNotes.Add($"Quantity-aware search: {groups.Count:N0} distinct size/rotation/material types; {windows} type windows; {columnCount:N0} generated patterns. Original identities preserved.");
        if (limited || Expired()) result.ScaleNotes.Add("Pattern search work/time limit reached; completed integer patterns retained and all remaining panes accounted for. Not a proof of optimality.");
        return result;
    }

    // ── RepeatPricing.cs — the pattern pricer ───────────────────────

    // Pricing works in integer millimetres, not a raster approximation.
    // These are search/memory limits, never order-quantity limits.
    private const int RepeatAxisLimit = 16384;
    private const int RepeatPatternPanes = 256;
    private readonly record struct RepeatPlacement(int Type, int X, int Y, int W, int H);
    private readonly record struct RepeatOrientation(int Type, int U, int V);
    private sealed class RepeatPattern
    {
        public int Stock;
        public RepeatPlacement[] Parts = Array.Empty<RepeatPlacement>();
        public (int Type, int Count)[] Counts = Array.Empty<(int, int)>();
        public long Area;
    }
    private sealed class RepeatStrip
    {
        public int Width;
        public double Value;
        public List<RepeatPlacement> Parts = new();
    }

    private static void PriceRepeatPatterns(List<RepeatType> types, StockSize stock, int stockIndex,
        MarginProfile margins, double[] prices, int gap, bool transpose,
        Action<RepeatPattern> emit, CancellationToken token, Func<bool> expired)
    {
        long uw = (long)stock.WidthMm - margins.LeftMm - margins.RightMm;
        long uh = (long)stock.HeightMm - margins.TopMm - margins.BottomMm;
        long lu = transpose ? uh : uw, lv = transpose ? uw : uh;
        if (lu <= 0 || lv <= 0 || lu + gap > RepeatAxisLimit || lv + gap > RepeatAxisLimit) return;
        int U = (int)lu, V = (int)lv, edge = checked(margins.KerfMm + margins.MinBreakoutMm);
        bool Edge(int remainder) => remainder == 0 || remainder >= edge;
        var options = new List<RepeatOrientation>();
        for (int i = 0; i < types.Count; i++)
        {
            var t = types[i];
            if (prices[i] <= 1e-10 || t.W <= 0 || t.H <= 0) continue;
            for (int turn = 0; turn < (t.Rotate && t.W != t.H ? 2 : 1); turn++)
            {
                int w = turn == 0 ? t.W : t.H, h = turn == 0 ? t.H : t.W;
                int u = transpose ? h : w, v = transpose ? w : h;
                if (u <= U && v <= V) options.Add(new RepeatOrientation(i, u, v));
            }
        }
        var strips = new List<RepeatStrip>();
        foreach (int width in options.Select(o => o.U).Distinct().OrderBy(w => w))
        {
            token.ThrowIfCancellationRequested();
            if (expired()) return;
            var eligible = options.Where(o => o.U <= width && Edge(width - o.U)).ToArray();
            int cap = V + gap;
            var dp = new double[cap + 1];
            var pick = Enumerable.Repeat(-1, cap + 1).ToArray();
            var count = new int[cap + 1];
            for (int c = 1; c <= cap; c++)
            {
                if ((c & 255) == 0) token.ThrowIfCancellationRequested();
                dp[c] = dp[c - 1]; count[c] = count[c - 1];
                for (int j = 0; j < eligible.Length; j++)
                {
                    var o = eligible[j]; int cost = o.V + gap;
                    if (cost > c || count[c - cost] >= RepeatPatternPanes) continue;
                    double value = dp[c - cost] + prices[o.Type];
                    if (value <= dp[c] + 1e-10) continue;
                    dp[c] = value; pick[c] = j; count[c] = count[c - cost] + 1;
                }
            }
            RepeatStrip Build(int capacity)
            {
                var s = new RepeatStrip { Width = width }; int offset = 0;
                for (int c = capacity; c > 0;)
                {
                    int j = pick[c]; if (j < 0) { c--; continue; }
                    var o = eligible[j];
                    s.Parts.Add(new RepeatPlacement(o.Type, 0, offset, o.U, o.V));
                    offset += o.V + gap; c -= o.V + gap; s.Value += prices[o.Type];
                }
                return s;
            }
            var strip = Build(cap);
            if (strip.Parts.Count > 0)
            {
                var last = strip.Parts[strip.Parts.Count - 1];
                if (!Edge(V - last.Y - last.H)) strip = Build(Math.Max(0, cap - edge));
                if (strip.Parts.Count > 0) strips.Add(strip);
            }
        }
        int capacityU = U + gap;
        var across = new double[capacityU + 1];
        var choice = Enumerable.Repeat(-1, capacityU + 1).ToArray();
        var sizes = new int[capacityU + 1];
        for (int c = 1; c <= capacityU; c++)
        {
            if ((c & 255) == 0) { token.ThrowIfCancellationRequested(); if (expired()) return; }
            across[c] = across[c - 1]; sizes[c] = sizes[c - 1];
            for (int j = 0; j < strips.Count; j++)
            {
                var s = strips[j]; int cost = s.Width + gap;
                if (cost > c || sizes[c - cost] + s.Parts.Count > RepeatPatternPanes) continue;
                double value = across[c - cost] + s.Value;
                if (value <= across[c] + 1e-10) continue;
                across[c] = value; choice[c] = j; sizes[c] = sizes[c - cost] + s.Parts.Count;
            }
        }
        List<int> Reconstruct(int cap)
        {
            var ids = new List<int>();
            for (int c = cap; c > 0;)
            { int j = choice[c]; if (j < 0) { c--; continue; } ids.Add(j); c -= strips[j].Width + gap; }
            return ids;
        }
        void Emit(List<int> ids)
        {
            int used = ids.Sum(i => strips[i].Width + gap) - gap;
            if (ids.Count == 0 || used > U || !Edge(U - used)
                || ids.Sum(i => strips[i].Parts.Count) > RepeatPatternPanes) return;
            var parts = new List<RepeatPlacement>(); int offset = 0;
            foreach (int id in ids)
            {
                var s = strips[id];
                foreach (var p in s.Parts)
                    parts.Add(transpose
                        ? new RepeatPlacement(p.Type, p.Y, p.X + offset, p.H, p.W)
                        : p with { X = p.X + offset });
                offset += s.Width + gap;
            }
            emit(new RepeatPattern { Stock = stockIndex, Parts = parts.ToArray() });
        }
        Emit(Reconstruct(capacityU)); Emit(Reconstruct(Math.Max(0, capacityU - edge)));
        for (int j = 0; j < strips.Count; j++)
        {
            token.ThrowIfCancellationRequested(); if (expired()) return;
            int cost = strips[j].Width + gap;
            foreach (int reserve in new[] { 0, edge })
            {
                if (cost > capacityU - reserve) continue;
                var ids = Reconstruct(capacityU - reserve - cost); ids.Insert(0, j); Emit(ids);
            }
            int times = Math.Min(capacityU / cost, RepeatPatternPanes / strips[j].Parts.Count);
            while (times > 0 && !Edge(U - (times * cost - gap))) times--;
            Emit(Enumerable.Repeat(j, times).ToList());
        }
    }

    // ── RepeatMaster.cs — the restricted master + integer plans ────

    // Managed restricted master: equality demand rows and finite-rack slack
    // rows. No native solver/runtime/package is required on the shop PC.
    // Artificial demand columns represent UNPLACED, at a lexicographic
    // penalty greater than placing every input pane on its own sheet.
    private sealed class RepeatMaster
    {
        private sealed class Column
        {
            public (int Row, double Value)[] Cells = Array.Empty<(int, double)>();
            public double Cost;
            public int Pattern = -1;
        }
        private readonly int _rows;
        private readonly int _tieSeed;
        private readonly double[] _rhs;
        private readonly List<Column> _columns = new();
        private readonly int[] _basis;
        private readonly double[,] _inverse;
        private readonly Dictionary<int, int> _stockRows = new();
        private readonly Dictionary<int, int> _boundRows = new();
        private readonly CancellationToken _token;
        private readonly Func<bool> _expired;
        public double[] Prices { get; private set; }
        public bool Limited { get; private set; }
        public double Objective => BasicValues().Select((v, i) => Math.Max(0, v) * _columns[_basis[i]].Cost).Sum();

        public RepeatMaster(int[] demand, int[] available, double shortagePenalty,
            CancellationToken token, Func<bool> expired, Dictionary<int, int>? upper = null, int tieSeed = 0)
        {
            _token = token; _expired = expired; _tieSeed = tieSeed;
            for (int i = 0; i < available.Length; i++)
                if (available[i] != int.MaxValue) _stockRows.Add(i, demand.Length + _stockRows.Count);
            if (upper != null)
                foreach (var p in upper.OrderBy(p => p.Key)) _boundRows.Add(p.Key, demand.Length + _stockRows.Count + _boundRows.Count);
            _rows = demand.Length + _stockRows.Count + _boundRows.Count;
            _rhs = new double[_rows]; _basis = new int[_rows]; _inverse = new double[_rows, _rows];
            for (int i = 0; i < demand.Length; i++) _rhs[i] = demand[i];
            foreach (var p in _stockRows) _rhs[p.Value] = available[p.Key];
            foreach (var p in _boundRows) _rhs[p.Value] = upper![p.Key];
            for (int i = 0; i < _rows; i++)
            {
                _basis[i] = i; _inverse[i, i] = 1;
                _columns.Add(new Column { Cells = new[] { (i, 1d) }, Cost = i < demand.Length ? shortagePenalty : 0 });
            }
            Prices = new double[_rows];
        }
        public void Add(RepeatPattern pattern, int index)
        {
            var cells = pattern.Counts.Select(p => (p.Type, (double)p.Count)).ToList();
            if (_stockRows.TryGetValue(pattern.Stock, out int r)) cells.Add((r, 1));
            if (_boundRows.TryGetValue(index, out int bound)) cells.Add((bound, 1));
            _columns.Add(new Column { Cells = cells.ToArray(), Cost = 1, Pattern = index });
        }
        private double[] BasicValues()
        {
            var result = new double[_rows];
            for (int i = 0; i < _rows; i++)
                for (int j = 0; j < _rows; j++) result[i] += _inverse[i, j] * _rhs[j];
            return result;
        }
        private void Duals()
        {
            Array.Clear(Prices, 0, Prices.Length);
            for (int i = 0; i < _rows; i++)
            {
                double cost = _columns[_basis[i]].Cost;
                for (int j = 0; j < _rows; j++) Prices[j] += cost * _inverse[i, j];
            }
        }
        public void Solve()
        {
            for (int step = 0; step < 6000; step++)
            {
                _token.ThrowIfCancellationRequested();
                if (_expired()) { Limited = true; break; }
                Duals();
                var basic = new HashSet<int>(_basis);
                int enter = -1; double reduced = -1e-7;
                for (int j = 0; j < _columns.Count; j++)
                {
                    if (basic.Contains(j)) continue;
                    var col = _columns[j]; double rc = col.Cost;
                    foreach (var cell in col.Cells) rc -= Prices[cell.Row] * cell.Value;
                    uint Priority(int index) => unchecked((uint)(index + 1) * 2654435761u ^ (uint)_tieSeed * 2246822519u);
                    if (rc < reduced - 1e-9 || (_tieSeed != 0 && enter >= 0 && Math.Abs(rc - reduced) <= 1e-9
                        && Priority(j) < Priority(enter))) { reduced = rc; enter = j; }
                }
                if (enter < 0) return;
                var values = BasicValues(); var direction = new double[_rows];
                int leave = -1; double ratio = double.PositiveInfinity;
                for (int i = 0; i < _rows; i++)
                {
                    foreach (var c in _columns[enter].Cells) direction[i] += _inverse[i, c.Row] * c.Value;
                    if (direction[i] <= 1e-9) continue;
                    double r = Math.Max(0, values[i]) / direction[i];
                    if (r < ratio - 1e-8 || (Math.Abs(r - ratio) <= 1e-8 && (leave < 0 || _basis[i] < _basis[leave])))
                    { ratio = r; leave = i; }
                }
                // Numerical trouble is a candidate failure, not permission
                // to invent quantities. The integer assembler rechecks every
                // count and remaining rack capacity before using any column.
                if (leave < 0) { Limited = true; break; }
                double pivot = direction[leave];
                for (int j = 0; j < _rows; j++) _inverse[leave, j] /= pivot;
                for (int i = 0; i < _rows; i++)
                {
                    if (i == leave) continue;
                    double factor = direction[i];
                    for (int j = 0; j < _rows; j++) _inverse[i, j] -= factor * _inverse[leave, j];
                }
                _basis[leave] = enter;
                if (step == 5999) Limited = true;
            }
            Duals();
        }
        public double[] PatternValues(int count)
        {
            var output = new double[count]; var values = BasicValues();
            for (int i = 0; i < _rows; i++)
            {
                int p = _columns[_basis[i]].Pattern;
                if (p >= 0 && double.IsFinite(values[i])) output[p] = Math.Max(0, values[i]);
            }
            return output;
        }
    }

    private static int PatternFits(RepeatPattern p, int[] remaining, int[] stock)
    {
        int times = stock[p.Stock];
        foreach (var c in p.Counts) times = Math.Min(times, remaining[c.Type] / c.Count);
        return times;
    }
    private static void ConsumePattern(RepeatPattern p, int times, int[] remaining, int[] stock)
    {
        foreach (var c in p.Counts) remaining[c.Type] -= c.Count * times;
        if (stock[p.Stock] != int.MaxValue) stock[p.Stock] -= times;
    }

    private static int[] IntegerRepeatPlan(List<RepeatPattern> patterns, RepeatMaster master,
        int[] demand, int[] available, CancellationToken token, Func<bool> expired, int effort, bool diversify = true)
    {
        var counts = new int[patterns.Count]; var remaining = (int[])demand.Clone(); var stock = (int[])available.Clone();
        var fractional = master.PatternValues(patterns.Count);
        for (int i = 0; i < patterns.Count; i++)
        {
            int times = (int)Math.Min(int.MaxValue, Math.Floor(fractional[i] + 1e-7));
            times = Math.Min(times, PatternFits(patterns[i], remaining, stock));
            if (times <= 0) continue;
            counts[i] += times; ConsumePattern(patterns[i], times, remaining, stock);
        }
        var weights = master.Prices.Take(demand.Length).Select(v => Math.Max(0, v)).ToArray();
        // Greedy integer completion is always a feasible incumbent. Exact
        // quantity/rack tests, not fractional solver tolerances, authorize it.
        var patternValues = patterns.Select(p => p.Counts.Sum(c => weights[c.Type] * c.Count)).ToArray();
        int[] Complete(int[] needs, int[] racks, int trial = 0)
        {
            var eligible = Enumerable.Range(0, patterns.Count).Where(i => PatternFits(patterns[i], needs, racks) > 0).ToArray();
            var priorities = (double[])patternValues.Clone();
            if (trial > 0)
                foreach (int i in eligible)
                {
                    uint hash = unchecked((uint)(i + 1) * 2654435761u ^ (uint)trial * 2246822519u);
                    hash ^= hash >> 16; hash *= 3266489917u; hash ^= hash >> 13;
                    double noise = (hash & 65535) / 65535d - 0.5;
                    priorities[i] *= 1 + noise * (0.1 + (trial % 4) * 0.15);
                }
            var result = new int[patterns.Count];
            while (true)
            {
                token.ThrowIfCancellationRequested();
                int best = -1; double score = -1, fraction = -1; long area = -1;
                foreach (int i in eligible)
                {
                    var p = patterns[i]; if (PatternFits(p, needs, racks) <= 0) continue;
                    double value = priorities[i];
                    double f = fractional[i] - Math.Floor(fractional[i]);
                    if (value > score + 1e-8 || (Math.Abs(value - score) <= 1e-8 &&
                        (f > fraction + 1e-8 || (Math.Abs(f - fraction) <= 1e-8 && p.Area > area))))
                    { best = i; score = value; fraction = f; area = p.Area; }
                }
                if (best < 0) break;
                int times = PatternFits(patterns[best], needs, racks);
                result[best] += times; ConsumePattern(patterns[best], times, needs, racks);
            }
            return result;
        }
        var residual = (int[])remaining.Clone(); var residualStock = (int[])stock.Clone();
        var completion = Complete(remaining, stock);
        for (int trial = 1; trial <= 96 + effort * 32 && !expired(); trial++)
        {
            var needs = (int[])residual.Clone(); var racks = (int[])residualStock.Clone();
            var other = Complete(needs, racks, trial);
            if (needs.Sum() < remaining.Sum() || (needs.Sum() == remaining.Sum() && other.Sum() < completion.Sum()))
            { completion = other; remaining = needs; stock = racks; }
        }
        if (remaining.All(x => x == 0))
            completion = RepairIntegerPatterns(patterns, residual, residualStock, completion, weights,
                token, expired, 12000 + effort * 4000);
        if (effort >= 3 && diversify)
            completion = BranchRepeatMaster(patterns, residual, residualStock, completion, token, expired, effort);
        for (int i = 0; i < counts.Length; i++) counts[i] += completion[i];

        // Reopen a few repeated patterns, not individual million-order panes.
        // Different cuts of the integer solution can resolve rounding waste.
        for (int pass = 0; pass < 2 + Math.Min(effort, 5); pass++)
        {
            token.ThrowIfCancellationRequested(); if (expired()) break;
            var repairNeeds = new int[demand.Length]; var repairStock = (int[])available.Clone();
            for (int i = 0; i < counts.Length; i++)
                if (repairStock[patterns[i].Stock] != int.MaxValue) repairStock[patterns[i].Stock] -= counts[i];
            var removed = new int[counts.Length]; int total = 0;
            var order = Enumerable.Range(0, counts.Length).Where(i => counts[i] > 0)
                .OrderBy(i => pass == 0 ? patterns[i].Area : (long)((i * 1103515245L + pass * 12345L) & 0x7fffffff));
            foreach (int i in order)
            {
                if (total >= 16 || repairNeeds.Sum() + patterns[i].Parts.Length > 160) break;
                removed[i]++; total++;
                foreach (var c in patterns[i].Counts) repairNeeds[c.Type] += c.Count;
                if (repairStock[patterns[i].Stock] != int.MaxValue) repairStock[patterns[i].Stock]++;
            }
            var repaired = RepairIntegerPatterns(patterns, repairNeeds, repairStock, removed, weights,
                token, expired, 6000 + effort * 2000);
            if (repaired.Sum() >= total) continue;
            for (int i = 0; i < counts.Length; i++) counts[i] += repaired[i] - removed[i];
        }
        if (diversify)
        {
            double Score(int[] plan)
            {
                var left = (int[])demand.Clone(); var racks = (int[])available.Clone();
                for (int i = 0; i < plan.Length; i++)
                {
                    if (plan[i] > PatternFits(patterns[i], left, racks)) return double.PositiveInfinity;
                    ConsumePattern(patterns[i], plan[i], left, racks);
                }
                return plan.Sum() + ((double)demand.Sum() + 1) * left.Sum();
            }
            double score = Score(counts);
            for (int seed = 1; seed <= 8 + effort * 4 && !expired(); seed++)
            {
                // Degenerate LP optima may round very differently. Change
                // only pivot tie order, never dimensions, demand or cost.
                var alternative = new RepeatMaster(demand, available, (double)demand.Sum() + 1, token, expired, tieSeed: seed);
                for (int i = 0; i < patterns.Count; i++) alternative.Add(patterns[i], i);
                alternative.Solve();
                var other = IntegerRepeatPlan(patterns, alternative, demand, available, token, expired, effort, false);
                double candidateScore = Score(other);
                if (candidateScore < score) { counts = other; score = candidateScore; }
                if (!alternative.Limited && Math.Ceiling(alternative.Objective - 1e-6) >= score) break;
            }
            if (effort >= 3) counts = BranchRepeatMaster(patterns, demand, available, counts, token, expired, effort);
        }
        return counts;
    }

    private static int[] RepairIntegerPatterns(List<RepeatPattern> patterns, int[] demand, int[] available,
        int[] incumbent, double[] weights, CancellationToken token, Func<bool> expired, int nodeLimit)
    {
        if (demand.Sum() > 256 || expired()) return incumbent;
        var allowed = Enumerable.Range(0, patterns.Count).Where(i => PatternFits(patterns[i], demand, available) > 0).ToArray();
        if (allowed.Length == 0) return incumbent;
        var byType = new List<int>[demand.Length];
        for (int t = 0; t < demand.Length; t++) byType[t] = new List<int>();
        foreach (int i in allowed) foreach (var c in patterns[i].Counts) byType[c.Type].Add(i);
        double maxValue = allowed.Max(i => patterns[i].Counts.Sum(c => weights[c.Type] * c.Count));
        var needs = (int[])demand.Clone(); var racks = (int[])available.Clone();
        var path = new int[patterns.Count]; var best = (int[])incumbent.Clone(); int bestCount = best.Sum(), nodes = 0;
        var seen = new Dictionary<string, int>();
        void Search(int depth)
        {
            token.ThrowIfCancellationRequested();
            if (++nodes > nodeLimit || expired() || depth >= bestCount) return;
            double value = 0; int total = 0;
            for (int t = 0; t < needs.Length; t++) { value += weights[t] * needs[t]; total += needs[t]; }
            if (total == 0) { bestCount = depth; best = (int[])path.Clone(); return; }
            // Normalize the dual weights against EVERY allowed pattern: a
            // valid lower bound even if pricing or simplex hit a work limit.
            if (maxValue > 0 && depth + Math.Ceiling(value / maxValue - 1e-7) >= bestCount) return;
            string key = string.Join(",", needs) + "|" + string.Join(",", racks);
            if (seen.TryGetValue(key, out int oldDepth) && oldDepth <= depth) return;
            seen[key] = depth;
            List<int>? choices = null;
            for (int t = 0; t < needs.Length; t++)
            {
                if (needs[t] == 0) continue;
                var fit = byType[t].Where(i => PatternFits(patterns[i], needs, racks) > 0).ToList();
                if (fit.Count == 0) return;
                if (choices == null || fit.Count < choices.Count) choices = fit;
            }
            foreach (int i in choices!.OrderByDescending(i => patterns[i].Counts.Sum(c => weights[c.Type] * c.Count))
                .ThenByDescending(i => patterns[i].Area).ThenBy(i => i))
            {
                if (nodes >= nodeLimit || expired()) break;
                ConsumePattern(patterns[i], 1, needs, racks); path[i]++;
                Search(depth + 1);
                path[i]--; ConsumePattern(patterns[i], -1, needs, racks);
            }
        }
        Search(0);
        return best;
    }

    // ── RepeatInteger.cs — the bounded integer master ───────────────

    private sealed class RepeatBranch
    {
        public Dictionary<int, int> Lower = new();
        public Dictionary<int, int> Upper = new();
        public int Depth;
        public double Bound;
    }

    private static int[] BranchRepeatMaster(List<RepeatPattern> patterns, int[] demand, int[] available,
        int[] incumbent, CancellationToken token, Func<bool> expired, int effort)
    {
        if (expired()) return incumbent;
        var active = Enumerable.Range(0, demand.Length).Where(i => demand[i] > 0).ToArray();
        var ids = Enumerable.Range(0, patterns.Count).Where(i => PatternFits(patterns[i], demand, available) > 0).ToArray();
        if (active.Length == 0 || ids.Length == 0) return incumbent;
        var map = active.Select((t, i) => (t, i)).ToDictionary(p => p.t, p => p.i);
        var pool = ids.Select(i => new RepeatPattern { Stock = patterns[i].Stock, Area = patterns[i].Area,
            Counts = patterns[i].Counts.Select(c => (map[c.Type], c.Count)).ToArray() }).ToList();
        var reduced = BranchRepeatMasterCore(pool, active.Select(i => demand[i]).ToArray(), available,
            ids.Select(i => incumbent[i]).ToArray(), token, expired, effort);
        var result = new int[patterns.Count];
        for (int i = 0; i < ids.Length; i++) result[ids[i]] = reduced[i];
        return result;
    }

    // Bounded integer master over pattern MULTIPLICITIES. A branch fixes a
    // count of a reusable pattern; it never branches once per expanded pane.
    // Each node starts with a feasible artificial/slack basis. Search limits
    // keep the incumbent, not a fractional/overproduced solution.
    private static int[] BranchRepeatMasterCore(List<RepeatPattern> patterns, int[] demand, int[] available,
        int[] incumbent, CancellationToken token, Func<bool> expired, int effort)
    {
        if (expired() || patterns.Count == 0) return incumbent;
        double penalty = (double)demand.Sum() + 1;
        double Score(int[] candidate)
        {
            var needs = (int[])demand.Clone(); var racks = (int[])available.Clone();
            for (int i = 0; i < candidate.Length; i++)
            {
                if (candidate[i] < 0 || candidate[i] > PatternFits(patterns[i], needs, racks)) return double.PositiveInfinity;
                ConsumePattern(patterns[i], candidate[i], needs, racks);
            }
            return candidate.Sum() + penalty * needs.Sum();
        }
        var best = incumbent; double bestScore = Score(best);
        var pending = new PriorityQueue<RepeatBranch, (int, double, int)>(); int serial = 0;
        pending.Enqueue(new RepeatBranch(), (0, 0, serial++));
        int nodes = 0, nodeLimit = 64 + Math.Clamp(effort, 1, 5) * 64;
        while (pending.Count > 0 && nodes++ < nodeLimit && !expired())
        {
            token.ThrowIfCancellationRequested();
            pending.TryDequeue(out var node, out var priority);
            if (Math.Ceiling(node.Bound - 1e-6) >= bestScore) continue;
            var needs = (int[])demand.Clone(); var racks = (int[])available.Clone();
            var forced = new int[patterns.Count]; bool feasible = true;
            foreach (var p in node.Lower)
            {
                if (p.Value > PatternFits(patterns[p.Key], needs, racks)) { feasible = false; break; }
                forced[p.Key] = p.Value; ConsumePattern(patterns[p.Key], p.Value, needs, racks);
            }
            if (!feasible) continue;
            var upper = new Dictionary<int, int>();
            foreach (var p in node.Upper)
            {
                int bound = p.Value - forced[p.Key];
                if (bound < 0) { feasible = false; break; }
                upper.Add(p.Key, bound);
            }
            if (!feasible) continue;
            var lp = new RepeatMaster(needs, racks, penalty, token, expired, upper);
            for (int i = 0; i < patterns.Count; i++) lp.Add(patterns[i], i);
            lp.Solve();
            if (lp.Limited) continue; // unfinished primal is NOT a lower bound
            double lowerBound = forced.Sum() + lp.Objective;
            if (Math.Ceiling(lowerBound - 1e-6) >= bestScore) continue;
            var values = lp.PatternValues(patterns.Count);
            var candidate = (int[])forced.Clone();
            for (int i = 0; i < values.Length; i++)
            {
                int times = (int)Math.Min(int.MaxValue, Math.Floor(values[i] + 1e-7));
                times = Math.Min(times, PatternFits(patterns[i], needs, racks));
                candidate[i] += times; ConsumePattern(patterns[i], times, needs, racks);
            }
            // Recover a feasible integer incumbent at every visited node.
            while (true)
            {
                token.ThrowIfCancellationRequested();
                int chosen = -1; double score = -1, fraction = -1; long area = -1;
                for (int i = 0; i < patterns.Count; i++)
                {
                    if (node.Upper.TryGetValue(i, out int cap) && candidate[i] >= cap) continue;
                    if (PatternFits(patterns[i], needs, racks) <= 0) continue;
                    double value = patterns[i].Counts.Sum(c => Math.Max(0, lp.Prices[c.Type]) * c.Count);
                    double f = values[i] - Math.Floor(values[i]);
                    if (value > score + 1e-8 || (Math.Abs(value - score) <= 1e-8 &&
                        (f > fraction + 1e-8 || (Math.Abs(f - fraction) <= 1e-8 && patterns[i].Area > area))))
                    { chosen = i; score = value; fraction = f; area = patterns[i].Area; }
                }
                if (chosen < 0) break;
                int times = PatternFits(patterns[chosen], needs, racks);
                if (node.Upper.TryGetValue(chosen, out int cap2)) times = Math.Min(times, cap2 - candidate[chosen]);
                candidate[chosen] += times; ConsumePattern(patterns[chosen], times, needs, racks);
            }
            double candidateScore = Score(candidate);
            if (candidateScore < bestScore) { bestScore = candidateScore; best = candidate; }
            if (Math.Ceiling(lowerBound - 1e-6) >= bestScore || node.Depth >= 96) continue;
            int branch = -1; double fractionality = 0;
            for (int i = 0; i < values.Length; i++)
            {
                double f = values[i] - Math.Floor(values[i]); double distance = f > 1e-6 && f < 1 - 1e-6 ? f : 0;
                if (distance > fractionality + 1e-7) { fractionality = distance; branch = i; }
            }
            if (branch < 0) continue;
            int down = (int)Math.Floor(values[branch] + forced[branch]);
            var left = new RepeatBranch { Lower = new Dictionary<int, int>(node.Lower), Upper = new Dictionary<int, int>(node.Upper), Depth = node.Depth + 1, Bound = lowerBound };
            left.Upper[branch] = down;
            var right = new RepeatBranch { Lower = new Dictionary<int, int>(node.Lower), Upper = new Dictionary<int, int>(node.Upper), Depth = node.Depth + 1, Bound = lowerBound };
            right.Lower[branch] = down + 1;
            pending.Enqueue(right, (-right.Depth, Math.Ceiling(lowerBound - 1e-6), serial++));
            pending.Enqueue(left, (-left.Depth, Math.Ceiling(lowerBound - 1e-6), serial++));
        }
        return best;
    }

    // ── RepeatTail.cs — the integer remainder ───────────────────────

    // Retain the bulk LP repeats, but also try the existing geometry engines
    // on their small integer remainder. The entire order is never expanded
    // into beam states, and leftover panes are not forced into singleton cuts.
    private static int[] FinishRepeatRemainder(List<RepeatType> types, List<RepeatPattern> patterns,
        RepeatMaster master, int[] demand, int[] available, IReadOnlyList<StockSize> stocks,
        int[] rackIds, MarginProfile margins, RotationPolicy rotation, int[] incumbent,
        CancellationToken token, Func<bool> expired)
    {
        if (expired()) return incumbent;
        var remaining = (int[])demand.Clone(); var racks = (int[])available.Clone();
        var floor = new int[patterns.Count]; var values = master.PatternValues(patterns.Count);
        for (int i = 0; i < patterns.Count; i++)
        {
            int times = (int)Math.Min(int.MaxValue, Math.Floor(values[i] + 1e-7));
            times = Math.Min(times, PatternFits(patterns[i], remaining, racks));
            floor[i] = times; ConsumePattern(patterns[i], times, remaining, racks);
        }
        if (remaining.Sum() == 0 || remaining.Sum() > 2048) return incumbent;
        var input = new List<CutPiece>(); var typeMap = new Dictionary<CutPiece, int>();
        for (int t = 0; t < types.Count; t++)
            foreach (var p in types[t].Pieces.Skip(types[t].Next + demand[t] - remaining[t]).Take(remaining[t]))
            { input.Add(p); typeMap.Add(p, t); }
        var pool = new List<StockSize>(); var stockMap = new Dictionary<StockSize, int>();
        for (int s = 0; s < racks.Length; s++)
        {
            if (racks[s] <= 0) continue;
            var stock = stocks[rackIds[s]].WithMaxSheets(racks[s] == int.MaxValue ? 0 : racks[s]);
            pool.Add(stock); stockMap.Add(stock, s);
        }
        if (pool.Count == 0) return incumbent;
        OptimizationResult? best = null;
        void Consider(OptimizationResult candidate)
        {
            if (expired()) return;
            best = Better(best, CheckCandidate(candidate, input, margins, token));
        }
        Consider(RunCapacityStrips(input, pool, margins, rotation, false, token));
        Consider(RunCapacityStrips(input, pool, margins, rotation, true, token));
        if (!expired()) Consider(PackSmart(input, pool, margins, rotation,
            SplitMode.HorizontalFirst, 2, token, candidateWindowSize: 128));
        if (input.Count <= PatternPartWindow)
            foreach (int width in new[] { 4, 8 })
                if (!expired()) Consider(RunBestSplitBeam(input, pool, margins, rotation, width, token,
                    maxExpansions: PatternPartWindow * width, consolidationSweeps: 3));
        if (best == null || best.HasValidationFailure || best.Unplaced.Count != 0) return incumbent;
        long covered = incumbent.Select((n, i) => (long)n * patterns[i].Parts.Length).Sum();
        if (covered == demand.Sum() && best.SheetsUsed + floor.Sum() >= incumbent.Sum()) return incumbent;
        int originalCount = patterns.Count;
        foreach (var sheet in best.Sheets)
        {
            var parts = sheet.Pieces.Select(p => new RepeatPlacement(typeMap[p.Piece],
                p.X - margins.LeftMm, p.Y - margins.TopMm, p.WidthMm, p.HeightMm)).ToArray();
            patterns.Add(new RepeatPattern { Stock = stockMap[sheet.Stock], Parts = parts,
                Counts = parts.GroupBy(p => p.Type).Select(g => (g.Key, g.Count())).ToArray(),
                Area = parts.Sum(p => (long)p.W * p.H) });
        }
        Array.Resize(ref floor, patterns.Count);
        for (int i = originalCount; i < patterns.Count; i++) floor[i] = 1;
        return floor;
    }
}

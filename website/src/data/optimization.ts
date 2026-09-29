/**
 * Optimization module facts.
 *
 * Every value here is read out of the engine and its view-model:
 *   Services/Optimization/OptimizationModels.cs
 *   Services/Optimization/GuillotinePacker*.cs
 *   ViewModels/Optimization/OptimizationViewModel.Margins.cs
 *   ViewModels/Optimization/OptimizationViewModel.CncExport.cs
 *   Views/Optimization/Tabs/*.xaml
 *
 * No percentage, speed-up or savings claim appears anywhere in this file,
 * because the repository does not make one.
 */

export type OptimizerTab = {
  id: string;
  /** Tab label as it appears in the workspace (Views/Optimization/OptimizationView.xaml). */
  label: string;
  summary: string;
  bullets: string[];
  evidence: string;
};

export const optimizerTabs: OptimizerTab[] = [
  {
    id: "jobs",
    label: "Jobs",
    summary: "Pull confirmed orders into the optimization batch and pick the pieces to cut.",
    bullets: [
      "Confirmed orders register with a job / customer / project filter",
      "Piece specification breakdown: glass composition, W × H, qty, label",
      "Add pieces manually or tick confirmed orders and let the batch build itself",
    ],
    evidence: "Views/Optimization/Tabs/JobsTabView.xaml",
  },
  {
    id: "stocks",
    label: "Stocks",
    summary: "The plates the engine is allowed to cut from, and what is left on each rack.",
    bullets: [
      "Sheet size, thickness, colour and origin source per rack row",
      "Available / used counts per plate, with a tick to include a rack in the run",
      "Import Step-4 sheets straight from the proforma, or add stock by hand",
    ],
    evidence: "Views/Optimization/Tabs/StocksTabView.xaml · Services/Optimization/OptimizationModels.cs (StockSize)",
  },
  {
    id: "sizes",
    label: "Sizes",
    summary: "The cut plan expressed as the saw will cut it, grouped by glass composition.",
    bullets: [
      "Merged glass specification groups with thickness and gross area",
      "Size rows: piece reference, size (W × H mm), pieces",
      "Manual cut list alongside the generated plan",
    ],
    evidence: "Views/Optimization/Tabs/SizesTabView.xaml",
  },
  {
    id: "cut-list",
    label: "Cut List",
    summary: "The piece list the run works from, including the outer/inner pane colours.",
    bullets: [
      "Cutting group, module, pieces, quantity used and OPT % per line",
      "Outer and inner sheet dimensions and colours, read from the specification",
      "Pieces the engine could not place are surfaced here, not hidden",
    ],
    evidence: "Views/Optimization/Tabs/CutListTabView.xaml",
  },
  {
    id: "two-d-view",
    label: "2D View",
    summary: "The nesting result, plate by plate, as the guillotine table sees it.",
    bullets: [
      "Placements with dimensions (W × H) and 90° rotation marks",
      "Cut tags and breakout indicators",
      "Zoom levels from fit-the-sheet to 100%",
    ],
    evidence: "Views/Optimization/Tabs/TwoDViewTabView.xaml",
  },
  {
    id: "summary",
    label: "Summary",
    summary: "What the run cost and how well the glass was used.",
    bullets: [
      "Per-sheet execution schedule, stock size, yield % and waste",
      "Overall yield efficiency and total offcut / scrap",
      "Engine verification verdict and run notes",
      "Unplaced pieces and capacity violations called out",
    ],
    evidence: "Views/Optimization/Tabs/SummaryTabView.xaml · OptimizationResult (UtilizationPct, Unplaced, OptimalityGap)",
  },
  {
    id: "report",
    label: "Report",
    summary: "Four printable sections plus machine-ready export.",
    bullets: [
      "1 · Industrial cut schedule",
      "2 · Nested sheets summary",
      "3 · Merged specifications summary",
      "4 · Stock plates allocation",
      "Export: CSV, Excel, generic DXF, Bottero .OPT, Intermac .ISO, Lisec .DAT",
    ],
    evidence: "Views/Optimization/Tabs/ReportTabView.xaml · ViewModels/Optimization/OptimizationViewModel.CncExport.cs",
  },
  {
    id: "settings",
    label: "Settings",
    summary: "Everything that decides how the table actually cuts the glass.",
    bullets: [
      "Safety edges LM / RM / TM / BM per thickness, plus minimum breakout",
      "Kerf (blade gap) and cutting complexity",
      "Optimize effort 1–5, plate pattern and offcut reuse",
    ],
    evidence: "Views/Optimization/Tabs/SettingsTabView.xaml · ViewModels/Optimization/OptimizationViewModel.Margins.cs",
  },
];

export type ThicknessProfile = {
  bucket: string;
  range: string;
  edges: string;
  kerf: string;
  breakout: string;
};

/**
 * MarginProfile defaults, keyed by ThicknessBucket
 * (Services/Optimization/OptimizationModels.cs).
 */
export const thicknessProfiles: ThicknessProfile[] = [
  {
    bucket: "Thin",
    range: "Up to 6 mm",
    edges: "15 mm on all four edges (LM · RM · TM · BM)",
    kerf: "3 mm",
    breakout: "15 mm minimum",
  },
  {
    bucket: "Medium",
    range: "7–8 mm",
    edges: "30 mm on all four edges",
    kerf: "3 mm",
    breakout: "30 mm minimum",
  },
  {
    bucket: "Thick",
    range: "9 mm and above",
    edges: "LM / RM / BM 50 mm · TM 0 mm (asymmetric — thick glass takes no trim on the top edge)",
    kerf: "4 mm",
    breakout: "50 mm, switchable to 40 mm",
  },
];

export const searchFamilies = [
  {
    name: "Auto",
    detail: "Picks stage 3, 4 or 5 from the piece count, stock mix and rack cap. It calls an existing stage — it does not change the plate pattern.",
  },
  {
    name: "Stage 1 — Quick",
    detail: "Area shelf, strips and bounded patterns. The fast portfolio, and what Run Quick uses.",
  },
  { name: "Stage 2", detail: "Three orderings, best-split, sheet-fill, wall-seed and smart placement." },
  { name: "Stage 3 — default", detail: "Full split grid, MaxRects packing and a width-4 beam search." },
  { name: "Stage 4", detail: "All six orderings, shuffles, vertical smart placement and a width-8 beam." },
  { name: "Stage 5", detail: "The widest shuffle set and MaxRects on every ordering. Expect minutes on a large job, not seconds — the app says so in its own hint text." },
] as const;

export const platePatterns = [
  {
    name: "Yield",
    tagline: "Fewest pieces left out, then fewest sheets",
    detail: "The default winner. A fuller first sheet does not replace it.",
  },
  {
    name: "Fill first plate",
    tagline: "Fills sheet 1 before opening another",
    detail: "Fills the leading plate harder without adding sheets or leaving more pieces out.",
  },
  {
    name: "Fewest cuts",
    tagline: "Less table time, same material",
    detail: "Between plans on the same sheet count, takes the one with fewer guillotine cuts.",
  },
  {
    name: "Minimise stock changes",
    tagline: "Fewer rack moves and setups",
    detail: "Between plans on the same sheet count, reaches for fewer stock sizes and switches between them less often.",
  },
  {
    name: "Remnant-first",
    tagline: "Leaves reusable offcuts, not slivers",
    detail: "Concentrates the leftover onto one sheet so waste is big enough to come off as usable stock. It chooses plans that leave reusable offcuts — it does not yet reuse them.",
  },
  {
    name: "Balanced spread",
    tagline: "Evenly loaded plates",
    detail: "Three even plates beat two full ones and a nearly empty one.",
  },
] as const;

export const cutComplexities = [
  { name: "Free guillotine", detail: "Unlimited guillotine depth, no trim cuts. The default, and the only policy under which existing calibration figures are exact." },
  { name: "2-stage (first X cut)", detail: "Two stages with the first cut running along the sheet width, producing stacked strips." },
  { name: "2-stage (first Y cut)", detail: "Two stages with the first cut running along the sheet height, producing side-by-side columns." },
  { name: "Simple (2-stage, direction auto)", detail: "Two stages with the first direction chosen per sheet (2SGP)." },
  { name: "3-stage", detail: "Three stages — the fullest of the staged family (3SGP)." },
  { name: "Complex — trims allowed (Z/W)", detail: "Unlimited depth plus permission to cut trims that release reusable remnants. Refused on laminated stock by design: the plies are not hand-broken at the table, so a trim cut would leave an unseparated edge." },
] as const;

export const enginePipeline = [
  { step: "Stock plates", detail: "Rack rows with size, thickness, colour and an optional balance cap" },
  { step: "Cutting list", detail: "Pieces with W × H, quantity and whether rotation is allowed" },
  { step: "Safety edges", detail: "Per-thickness LM / RM / TM / BM trims cut off before placement" },
  { step: "Kerf", detail: "The blade gap consumed between cuts — 3 mm thin/medium, 4 mm thick" },
  { step: "Minimum breakout", detail: "The narrowest offcut worth keeping; thinner strips are reported as trim waste" },
  { step: "Guillotine nesting", detail: "Pieces placed and split into free rectangles, with the cut sequence recorded as it goes" },
  { step: "Oracle check", detail: "Geometry and conservation verified before the plan can leave the module" },
  { step: "Utilization & waste", detail: "Placed area against sheet area, per sheet and weighted across the run" },
] as const;

export const verificationOracles = [
  {
    name: "Geometry",
    checks: "Margins honoured, rotation policy respected, dimensions intact, no overlaps, kerf consumed",
    evidence: "GuillotinePacker.Verify",
  },
  {
    name: "Conservation",
    checks: "Every input piece placed exactly once — input count equals placed plus unplaced, nothing missing and nothing duplicated",
    evidence: "GuillotinePacker.ValidateResult",
  },
] as const;

export const engineHonestyNotes = [
  "A plan that fails either oracle is still shown so the operator can inspect it, but it is never exported to a cut file and never fed to production work.",
  "Offcut reuse is off by default because it changes the plan. When it runs, every pane is re-checked after it moves and the whole attempt is rolled back if the plan stops verifying.",
  "Large inputs trade search breadth for termination, and every reduction is recorded on the result as a scale note rather than silently degrading the plan.",
  "The run records a weak lower bound on the sheet count (area only) and reports the optimality gap — so 'how close was this?' has an answer instead of a claim.",
] as const;

export const optimizationResultFields = [
  { key: "Sheets used", detail: "Number of plates the plan consumes" },
  { key: "Utilization", detail: "Placed glass area ÷ full sheet area, per sheet and weighted across the run" },
  { key: "Used area (SQM)", detail: "Summed area of placed pieces" },
  { key: "Stock area (SQM)", detail: "Summed area of the plates consumed" },
  { key: "Offcut / scrap", detail: "Leftover regions derived at the end of the run" },
  { key: "Unplaced pieces", detail: "Pieces the engine could not fit, listed rather than dropped" },
  { key: "Optimality gap", detail: "Sheets used minus the area-only lower bound, in sheets and as a percentage" },
  { key: "Verification", detail: "Geometry and conservation verdicts, plus what was actually checked" },
] as const;

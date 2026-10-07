/**
 * Feature groups used by /features and the homepage.
 * Each group's `evidence` names the repo location that supports it.
 */

export type FeatureGroup = {
  id: string;
  title: string;
  lead: string;
  items: { title: string; detail: string }[];
  evidence: string;
};

export const featureGroups: FeatureGroup[] = [
  {
    id: "estimation",
    title: "Estimation and pricing",
    lead:
      "The price of a glass job is decided by four inputs: what the build-up is, how big the pieces are, what the glass cost, and what you want to make on it. ProGlass holds all four in one place.",
    items: [
      { title: "SGU", detail: "Single-glazed build-up: thickness, colour, treatment, edge polish and panel extras." },
      {
        title: "DGU",
        detail:
          "Double-glazed units with a 6–32 mm air space, spacer system selection, argon, U-Insert, fanhole, cutouts and holes — each with a “per DWG” variant.",
      },
      { title: "LAM", detail: "Laminated build-up with PVB thickness, per-pane colour and treatment." },
      { title: "DGU + LAM", detail: "A laminated pane inside a double-glazed unit, priced as one build-up." },
      {
        title: "Step 5 specification grid",
        detail:
          "One block per specification: # · glass ref · W1 H1 W2 H2 · qty · SQM · total SQM · rate · value, on a block carrying glass type, specification, base rate and SURCHARGE %. Excel-like paste for large lists, and manual rate overrides that the auto-pricing leaves alone.",
      },
      {
        title: "Every panel bills at least 0.5 m²",
        detail:
          "SqmCalculator floors a panel at half a square metre, so a 400 × 300 mm vent light is 0.12 m² of glass and 0.50 m² of work — cut, arrised, toughened and handled as one piece. The rate is untouched: the floor is on the area, and Step 9 costs the same billable square metres so the two sides reconcile.",
      },
      {
        title: "A panel at 4 m² prices at base + the surcharge",
        detail:
          "The moment a row's billable area reaches 4 m² it takes the specification's SurplusRate — base rate × (1 + SURCHARGE %), 20% as the shop's standard for oversize work. Rows beside it stay on base, and the switch also fires on a change to the base rate or the percentage.",
      },
      {
        title: "Working the grid like a spreadsheet",
        detail:
          "Find and find-and-replace across the rows, paste REF / W1 / H1 / W2 / H2 / QTY straight from Excel or a BOQ, generate rows in bulk up to ten thousand, and delete a line from the row itself. Under it all sits the grand total summary: one row per specification — qty, total SQM, value — and the grand row across them, read-only, rolled up before Step 6 adds charges.",
      },
      {
        title: "QUICK, OPTIMIZE, VIEW RESULT",
        detail:
          "The grid's own action bar: QUICK makes the one fast pass Step 6 takes its sheet quantity from, OPTIMIZE searches the plans for fewest unplaced pieces then fewest sheets then highest utilisation, and VIEW RESULT opens the optimisation module on its summary.",
      },
      {
        title: "Charges, wastage and discount",
        detail: "Predefined charges, wastage rows and custom charges; wastage auto-imports the sheet colours used on the spec grid — seven colours produce seven rows.",
      },
    ],
    evidence: "Views/ProformaInvoice/Step4PricingEstimator/, ViewModels/ProformaInvoice/Step6OtherCharges/",
  },
  {
    id: "money",
    title: "Money that does not move",
    lead:
      "The single most important property of an ERP is that a number, once stored, stays what it was. ProGlass writes the calculation context next to the figure and reads it back.",
    items: [
      {
        title: "Persisted calculation context",
        detail: "Every money figure keeps the basis it was computed with. Reporting reads that figure rather than recomputing it.",
      },
      {
        title: "Staleness is flagged, not applied",
        detail: "When sheet-store prices move, job profitability marks the row. It never silently re-scores yesterday's quote at today's prices.",
      },
      {
        title: "One pricing engine",
        detail: "Proforma Step 9 is the only place money is computed. Its snapshot feeds profitability, statements and reports.",
      },
      {
        title: "Step 9 asks one question",
        detail:
          "Is the given price above target? Required target price is the base price plus the expected profit margin — 15% on a fresh install — and the price balance prints the difference. Revenue and glass cost sit under it: the cost is the same billable square metres at the Sheet Store purchase price, so the margin reconciles to the quote rather than to a second set of numbers.",
      },
      {
        title: "One VAT calculator",
        detail: "5% UAE VAT, VAT-from-subtotal and VAT-from-gross back-calculation, tolerance-checked to 1 fils (0.01 AED) — one rounding rule for the whole application.",
      },
      {
        title: "Unknown statuses pass through",
        detail:
          "Status vocabulary is normalised in one home per module, and a status the build does not recognise passes through untouched rather than being remapped to something plausible.",
      },
    ],
    evidence: "Services/Reporting/VatCalculator.cs, ViewModels/JobProfitability/, Services/ProformaInvoice/GlassPriceStamper.cs",
  },
  {
    id: "documents",
    title: "The document chain",
    lead: "Inquiry → proforma → job order → delivery note → tax invoice → statement. Each arrow is a conversion of the previous record, not a fresh entry.",
    items: [
      { title: "Atomic BOQ import", detail: "A row that cannot import stays on the inquiry as a note." },
      { title: "Geometry survives handoff", detail: "Sheet selections and air-spacer gaps cross from the proforma to the job order." },
      {
        title: "Multi-movement delivery",
        detail: "Partial deliveries accumulate; pending-piece maths is double-count-safe and lives in one place.",
      },
      {
        title: "Invoice families",
        detail: "Family + sequence numbering supports full, partial and incremental invoices, with version history on every document.",
      },
      { title: "Preserve-blank write-back", detail: "An invoice can enrich the company master but a missed field can never erase it." },
      {
        title: "Document Register",
        detail:
          "One linked view of every proforma, job order and delivery note, joined by the identifiers the documents already carry. Voided records stay reachable behind a filter, and the two fields editable in the cell — status and job-order priority — go through the native workflow's own transition rules.",
      },
    ],
    evidence:
      "Services/ProformaInvoice/, Services/JobOrder/Mapper/, Services/DeliveryNote/, Views/DocumentRegister/",
  },
  {
    id: "inventory",
    title: "Stock and consumption",
    lead: "Sheet Store keeps the glass you own, what it cost, what it sells for, and where it went.",
    items: [
      { title: "Priced both ways", detail: "Purchase price per SQM and sell price per SQM, with balance at cost and balance at sell." },
      { title: "Movements, not overwrites", detail: "Buys and uses are recorded as movements with a balance-after, so history is inspectable." },
      { title: "Consumption analysis", detail: "Usage grouped by job or invoice reference, with potential profit and profit margin." },
      { title: "Reorder levels", detail: "Per-item reorder level so a low rack is visible before it stops the floor." },
      { title: "Imports and exports", detail: "CSV / Excel import; PDF export for the stock position." },
    ],
    evidence: "Views/SheetStore/, Services/SheetStore/, proglass.db",
  },
  {
    id: "operations",
    title: "Operations and control",
    lead: "The commercial side and the factory side see the same job. Neither has to ask the other what happened.",
    items: [
      {
        title: "Production Control Center",
        detail: "Assign, start, complete, hold, release hold, reprioritise and move a job's stage — with a recorded reason when a job is blocked.",
      },
      {
        title: "Eight workstations",
        detail: "Cutting, Holes & Cutouts, Polish, Edge Deletion, Mitring, Tempering, Pairing and Doubling / Lamination each have their own operational view.",
      },
      { title: "Dispatch readiness", detail: "Delivery notes track what has left and what is still owed, per line." },
      { title: "Activity everywhere", detail: "Create, edit, delete, print and login are recorded — never keystrokes or mouse movement." },
    ],
    evidence: "ViewModels/Production/, Views/DeliveryNote/, Services/Audit/",
  },
  {
    id: "platform",
    title: "Platform",
    lead: "Desktop software that behaves like desktop software: local data, no browser, no account, no subscription dashboard.",
    items: [
      { title: "Local SQLite databases", detail: "One DbContext per bounded context, additive-only migrations, backup before every migration." },
      { title: "Backup and restore", detail: "Timestamped backup sets, retention, a pre-restore safety snapshot and a restore drill." },
      { title: "Roles and permissions", detail: "Eight roles and twenty-nine granular capabilities, enforced at the service boundary." },
      { title: "Audit trail", detail: "Before/after diffs with session, actor and module on every meaningful write." },
      { title: "Themes", detail: "Light and dark themes with an appearance section and per-module wallpapers." },
      { title: "15 languages", detail: "Locales are pure resource dictionaries — adding one needs no code change." },
      {
        title: "Records Sync",
        detail:
          "An opt-in background pass that publishes finished proformas to this website's Records, Job Order and Deliveries pages. ProGlass stays the source of truth, the local workflows keep working offline, and no credential is ever written into the queue.",
      },
    ],
    evidence:
      "Services/Database/, Services/Audit/, Themes/, Themes/Languages/, Services/RecordsSync/",
  },
];

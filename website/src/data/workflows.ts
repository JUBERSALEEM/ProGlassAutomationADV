/**
 * The business chain, as the application actually implements it.
 * Source: README.md, docs/WORKFLOW.md, Services/Workflow/StatusTransitions.cs
 *
 * Status vocabularies are the real ones. They enter their stores through a
 * single normalising home, and an unknown status passes through untouched —
 * it is never remapped to something plausible.
 */

export type ChainStage = {
  id: string;
  index: string;
  name: string;
  moduleKey: string;
  /** The status vocabulary the stage actually stores. */
  statuses: string[];
  summary: string;
  /** What crosses the handoff into the next stage. */
  handoff: string;
  /** What the code guarantees about that handoff. */
  guarantee: string;
  evidence: string;
  href: string;
};

export const businessChain: ChainStage[] = [
  {
    id: "inquiry",
    index: "01",
    name: "Inquiry",
    moduleKey: "Inquiry",
    statuses: ["Received", "Prepared", "Sent", "Confirmed", "Hold", "Cancelled"],
    summary: "A customer request arrives with a scope of supply: build type, specification, colours, W×H, quantity and expected rate.",
    handoff: "BOQ rows → the proforma's step-5 specification grid",
    guarantee:
      "The copy is atomic. A row that cannot be imported stays on the inquiry as a note — it is never dropped and never guessed at. Conversion is permission-gated and logged.",
    evidence: "Services/ProformaInvoice/InquiryBoqConversion.cs",
    href: "/features#inquiries",
  },
  {
    id: "proforma",
    index: "02",
    name: "Proforma Invoice",
    moduleKey: "ProformaInvoice",
    statuses: ["Draft", "Sent", "Approved", "Rejected", "Expired", "Cancelled"],
    summary:
      "The 9-step wizard prices the job: company, client, project, pricing, the step-5 specification grid, charges, print preview, attachments, profit & loss.",
    handoff: "Real grid geometry and the Step-9 profit snapshot",
    guarantee:
      "Step 9 is the application's single pricing engine. Its snapshot is what every downstream screen reads — reporting never recomputes money, it flags drift.",
    evidence: "Views/ProformaInvoice/Step9ProfitLoss/, Services/ProformaInvoice/GlassPriceStamper.cs",
    href: "/features#pricing-engine",
  },
  {
    id: "job-order",
    index: "03",
    name: "Job Order",
    moduleKey: "JobOrder",
    statuses: ["In Production", "Completed", "Delivered", "Approved"],
    summary: "The confirmed quotation becomes factory work, carrying priority and production status.",
    handoff: "Pending pieces → the delivery note",
    guarantee:
      "Real grid data crosses, not a re-typed summary — sheet selections and air-spacer gaps survive the handoff, so Step 4 and Step 5 stay consistent with what the floor builds.",
    evidence: "Services/JobOrder/Mapper/JobOrderMapper.cs",
    href: "/features#job-order",
  },
  {
    id: "production",
    index: "04",
    name: "Production",
    moduleKey: "ProductionControlCenter",
    statuses: ["Assigned", "Started", "Completed", "On hold", "Blocked"],
    summary:
      "Cutting, Holes & Cutouts, Polish, Edge Deletion, Mitring, Tempering, Pairing and Doubling / Lamination each have a workstation view.",
    handoff: "Finished pieces → dispatch",
    guarantee:
      "The Production Control Center assigns, starts, completes, holds and releases work, and records a reason when a job is blocked — so a stalled job has an explanation attached to it.",
    evidence: "ViewModels/Production/ProductionControlCenterViewModel.cs",
    href: "/features#group-production",
  },
  {
    id: "delivery",
    index: "05",
    name: "Delivery Note",
    moduleKey: "DeliveryNote",
    statuses: ["Draft", "Dispatching", "Dispatched", "Signed", "Received", "Delivered"],
    summary: "A job can leave in several movements, each one a separate delivery note against the same balance.",
    handoff: "Delivered quantities → the tax invoice",
    guarantee:
      "Balance tracking is double-count-safe and lives in one place, and the validation service refuses to deliver more than was ordered.",
    evidence: "Services/DeliveryNote/DeliveryNoteValidationService.cs",
    href: "/features#showcase-delivery-note",
  },
  {
    id: "tax-invoice",
    index: "06",
    name: "Tax Invoice",
    moduleKey: "TaxInvoice",
    statuses: ["Issued", "Paid", "Overdue"],
    summary: "VAT-compliant invoicing converted from the proforma or a delivery-note family, with partial and incremental invoices supported.",
    handoff: "Invoice lines → the account statement",
    guarantee:
      "One centralised VAT calculator handles every money path, so rounding cannot drift between two screens that show the same figure.",
    evidence: "Services/Reporting/VatCalculator.cs, Services/TaxInvoice/",
    href: "/features#group-sales",
  },
  {
    id: "statement",
    index: "07",
    name: "Statement",
    moduleKey: "StatementGenerator",
    statuses: ["Balances, not a lifecycle"],
    summary: "A statement aggregates a customer's invoices into running balances with ageing.",
    handoff: "Balances → reporting",
    guarantee: "The statement reads the invoice record; it does not carry its own lifecycle.",
    evidence: "Services/Statement/, ViewModels/Reports/",
    href: "/features#statement-generator",
  },
  {
    id: "reporting",
    index: "08",
    name: "Reporting",
    moduleKey: "SalesReport",
    statuses: ["Persisted figures"],
    summary: "Reporting reads the figures the documents stored — sales, proforma, job order, delivery, tax invoice and statement.",
    handoff: "Decisions",
    guarantee:
      "An ERP that quietly recalculates yesterday's quote with today's glass prices is not an ERP. A stale price basis is flagged, never silently rescored.",
    evidence: "ViewModels/JobProfitability/, Services/Reporting/",
    href: "/features#group-reports",
  },
];

/** The short rail used in the hero and in "How it works". */
export const chainRail = businessChain.map((s) => ({ index: s.index, name: s.name, id: s.id }));

/** Workflow verbs from the README, verbatim. */
export const chainVerbs = ["Inquiry", "Quote", "Confirm", "Produce", "Deliver", "Invoice", "Settle"] as const;

/**
 * The same job, two ways.
 *
 * Each row pairs one step of a real glass-fabrication job as it is usually run
 * on paper and in spreadsheets with what the application does for that same
 * step. The left column describes a general practice — never a named customer,
 * never a measured figure — and the right column is only what the code does;
 * where a claim has a source in the repository it is the same one the rest of
 * the site cites. No time savings, percentages or price differences appear
 * here, because nothing in the repository measures them.
 */
export type BeforeAfterRow = {
  id: string;
  step: string;
  before: string;
  after: string;
  /** The part of the application that does the work. */
  anchor: string;
  href: string;
};

export const beforeAfter: BeforeAfterRow[] = [
  {
    id: "price",
    step: "Price the job",
    before:
      "The build-up is worked out on a calculator or in a spreadsheet, and the basis it was priced on — glass, spacer, gas, labour — lives in that one file, if it is written down at all.",
    after:
      "A nine-step proforma wizard prices SGU, DGU, lamination and DGU + lamination from a specification library, and the basis the figure was built on is stamped onto the quotation.",
    anchor: "Proforma invoice",
    href: "/features#pricing-engine",
  },
  {
    id: "nest",
    step: "Decide what to cut",
    before:
      "The cutting plan is drawn by hand, or a guillotine is set by feel. Whether the plan is any good is only visible once the sheets are on the table.",
    after:
      "The optimizer runs Auto or one of five stages, tries six plate patterns and six cut complexities, and two independent oracles check the plan before it is used. The result exports to CSV, Excel, DXF, Bottero .OPT, Intermac .ISO or Lisec .DAT.",
    anchor: "Optimization",
    href: "/features#showcase-optimization",
  },
  {
    id: "stock",
    step: "Know what is in the racks",
    before:
      "Stock is whatever the last count said. An offcut put back on the rack is a memory, not a record, and nobody knows which sheet went into which job.",
    after:
      "The sheet store keeps buy and use movements per sheet, so offcuts and remnants are stock the system knows about, and a sheet's history stays attached to it.",
    anchor: "Sheet store",
    href: "/features#showcase-sheet-store",
  },
  {
    id: "floor",
    step: "See where the work is",
    before:
      "Progress is found by walking to the machine and asking. Nobody can tell the whole floor's position without interrupting it.",
    after:
      "Eight stage queues — cutting through pairing and doubling/lamination — feed work centres and a production control centre. A job is dispatch-ready only when the full quantity has cleared the chain.",
    anchor: "Production",
    href: "/features#group-production",
  },
  {
    id: "deliver",
    step: "Deliver part of an order",
    before:
      "A part delivery is re-typed from the order, and the balance owed is worked out again on paper — or lost until the customer asks.",
    after:
      "Delivery notes support multiple movements against one order, each with its own balance, so what has gone out and what is still owed never has to be reconstructed.",
    anchor: "Delivery note",
    href: "/features#showcase-delivery-note",
  },
  {
    id: "invoice",
    step: "Invoice what actually went out",
    before:
      "VAT is recalculated invoice by invoice, and the figure on the invoice is only as good as the person retyping it.",
    after:
      "One centralised VAT calculator serves the invoices, and the tax invoice is raised from the delivery and the job order rather than from a fresh transcription.",
    anchor: "Tax invoice",
    href: "/features#group-sales",
  },
  {
    id: "profit",
    step: "Answer what the job made",
    before:
      "The answer arrives at month end, if it arrives, because cost was never captured against the job in the first place.",
    after:
      "Job profitability reports against the job itself, using the charges and costs the job actually recorded — fan holes, countersunk holes, argon gas, silicon bed, U-inserts, overlap and shutter bonding among them.",
    anchor: "Job profitability",
    href: "/features#showcase-job-profitability",
  },
  {
    id: "trace",
    step: "Trace one piece of glass",
    before:
      "Nothing reliably links the sheet that was cut to the delivery note and the invoice that followed from it.",
    after:
      "The chain runs Inquiry ➜ Quote ➜ Confirm ➜ Produce ➜ Deliver ➜ Invoice ➜ Settle, and every document keeps its link to the one before it. That traceability is the point of the application.",
    anchor: "The chain",
    href: "/how-it-works",
  },
];

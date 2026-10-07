/**
 * The business chain for ProGlass Automation.
 *
 * Captures the complete high-level business journey:
 * Inquiry → Estimation → Proforma / Quotation → Job Order → Production
 * → Optimization → Delivery → Invoicing → Reporting.
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
  /** What the system guarantees about that handoff. */
  guarantee: string;
  evidence?: string;
  href: string;
};

export const businessChain: ChainStage[] = [
  {
    id: "inquiry",
    index: "01",
    name: "Inquiry",
    moduleKey: "Inquiry",
    statuses: ["Received", "Prepared", "Sent", "Confirmed", "Hold", "Cancelled"],
    summary: "Customer requirements are captured and prepared for estimation.",
    handoff: "Customer requirements & scope → Estimation",
    guarantee:
      "Customer requirements and BOQ lines are captured with full traceability from initial contact.",
    evidence: "Services/ProformaInvoice/InquiryBoqConversion.cs",
    href: "/features#inquiries",
  },
  {
    id: "estimation",
    index: "02",
    name: "Estimation",
    moduleKey: "GlassCalculators",
    statuses: ["Calculated", "Spec Verified", "Cost Stamped"],
    summary: "Glass specifications, dimensions and quantities are calculated.",
    handoff: "Calculated glass geometry & pane build-ups → Quotation",
    guarantee:
      "Pane-by-pane build-ups price actual glass thicknesses, interlayers, and processes accurately.",
    evidence: "Services/Glass/GlassBuildUpCalculator.cs",
    href: "/features#specifications",
  },
  {
    id: "proforma",
    index: "03",
    name: "Proforma / Quotation",
    moduleKey: "ProformaInvoice",
    statuses: ["Draft", "Sent", "Approved", "Rejected", "Expired", "Cancelled"],
    summary: "Pricing and specifications are prepared into a professional quotation.",
    handoff: "Approved quotation specifications → Factory job order",
    guarantee:
      "Quotation margins and unit prices are permanently stamped, protecting historical jobs from cost drift.",
    evidence: "Views/ProformaInvoice/Step9ProfitLoss/, Services/ProformaInvoice/GlassPriceStamper.cs",
    href: "/features#pricing-engine",
  },
  {
    id: "job-order",
    index: "04",
    name: "Job Order",
    moduleKey: "JobOrder",
    statuses: ["In Production", "Completed", "Delivered", "Approved"],
    summary: "Approved work moves into production planning.",
    handoff: "Production work orders & cutting dimensions → Workstation queues",
    guarantee:
      "Confirmed order specifications convert without re-keying, preserving exact dimensions and tolerances.",
    evidence: "Services/JobOrder/Mapper/JobOrderMapper.cs",
    href: "/features#job-order",
  },
  {
    id: "production",
    index: "05",
    name: "Production",
    moduleKey: "ProductionControlCenter",
    statuses: ["Assigned", "Started", "Completed", "On hold", "Blocked"],
    summary: "Production work and processing requirements are tracked.",
    handoff: "Fabrication batches → Cutting optimization & workstation queues",
    guarantee:
      "Eight dedicated workstation stages track progress live and record clear blockage reasons.",
    evidence: "ViewModels/Production/ProductionControlCenterViewModel.cs",
    href: "/features#group-production",
  },
  {
    id: "optimization",
    index: "06",
    name: "Optimization",
    moduleKey: "Optimization",
    statuses: ["Planned", "Optimized", "Verified", "Exported"],
    summary: "Glass cutting is optimized to reduce sheet usage and wastage.",
    handoff: "Optimized cutting patterns & offcut records → Cutting tables & sheet store",
    guarantee:
      "Guillotine nesting honors safety margins and blade kerf, and checks piece conservation before export.",
    evidence: "Services/Optimization/GuillotinePacker.cs",
    href: "/optimization",
  },
  {
    id: "delivery",
    index: "07",
    name: "Delivery",
    moduleKey: "DeliveryNote",
    statuses: ["Draft", "Dispatching", "Dispatched", "Signed", "Received", "Delivered"],
    summary: "Completed work is tracked through delivery.",
    handoff: "Delivered dispatch quantities → Invoicing",
    guarantee:
      "Multi-movement deliveries track remaining balances so you never over-deliver or lose pending counts.",
    evidence: "Services/DeliveryNote/DeliveryNoteValidationService.cs",
    href: "/features#showcase-delivery-note",
  },
  {
    id: "invoicing",
    index: "08",
    name: "Invoicing",
    moduleKey: "TaxInvoice",
    statuses: ["Issued", "Paid", "Overdue"],
    summary: "Completed business transactions move into invoicing.",
    handoff: "Invoiced totals & VAT breakdowns → Ledger and customer statements",
    guarantee:
      "Invoices convert straight from verified delivery notes with automated, audit-proof VAT calculation.",
    evidence: "Services/Reporting/VatCalculator.cs, Services/TaxInvoice/",
    href: "/features#group-sales",
  },
  {
    id: "reporting",
    index: "09",
    name: "Reporting",
    moduleKey: "SalesReport",
    statuses: ["Aggregated", "Audited", "Exported"],
    summary: "Business activity is consolidated into operational reports.",
    handoff: "Executive visibility & operational insights → Business management",
    guarantee:
      "Real-time visibility across sales performance, job profitability, outstanding balances, and inventory.",
    evidence: "ViewModels/JobProfitability/, Services/Reporting/",
    href: "/features#group-reports",
  },
];

/** The short rail used in the hero and in "How it works". */
export const chainRail = businessChain.map((s) => ({ index: s.index, name: s.name, id: s.id }));

/** Workflow verbs from the product journey. */
export const chainVerbs = [
  "Inquiry",
  "Estimation",
  "Quotation",
  "Job Order",
  "Production",
  "Optimization",
  "Delivery",
  "Invoicing",
  "Reporting",
] as const;

/**
 * The same job, two ways.
 *
 * Each row pairs one step of a real glass-fabrication job as it is usually run
 * on paper and in spreadsheets with what the application does for that same
 * step.
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
    href: "/optimization",
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
      "The chain runs Inquiry ➜ Estimation ➜ Quotation ➜ Job Order ➜ Production ➜ Optimization ➜ Delivery ➜ Invoicing ➜ Reporting, and every document keeps its link to the one before it.",
    anchor: "The chain",
    href: "/how-it-works",
  },
];

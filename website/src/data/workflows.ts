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
    handoff: "BOQ rows → the proforma's Step 5 spec grid",
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
    summary: "The 9-step wizard prices the job: company, client, project, pricing, spec grid, charges, summary, attachments, profit & loss.",
    handoff: "Real grid geometry and the Step-9 profit snapshot",
    guarantee:
      "Step 9 is the application's single pricing engine. Its snapshot is what every downstream screen reads — reporting never recomputes money, it flags drift.",
    evidence: "Views/ProformaInvoice/Step9ProfitLoss/, Services/ProformaInvoice/GlassPriceStamper.cs",
    href: "/features#proforma",
  },
  {
    id: "job-order",
    index: "03",
    name: "Job Order",
    moduleKey: "JobOrder",
    statuses: ["InProduction", "Completed", "Delivered", "Approved"],
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
    href: "/features#production",
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
    href: "/features#delivery",
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
    href: "/features#tax-invoice",
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
    href: "/features#statement",
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
    href: "/features#reports",
  },
];

/** The short rail used in the hero and in "How it works". */
export const chainRail = businessChain.map((s) => ({ index: s.index, name: s.name, id: s.id }));

/** Workflow verbs from the README, verbatim. */
export const chainVerbs = ["Inquiry", "Quote", "Confirm", "Produce", "Deliver", "Invoice", "Settle"] as const;

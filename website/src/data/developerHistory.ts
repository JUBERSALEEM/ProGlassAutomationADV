/**
 * The developer's working history, as the repository records it.
 *
 * The site will not carry an invented biography. There is no school, no
 * employer, no "fifteen years of experience" figure and no certification
 * anywhere to draw on, and a page that manufactured them would be the one thing
 * this project has consistently refused to do.
 *
 * What does exist is a dated paper trail. The author writes long working
 * documents as he goes — an audit, a forensic second pass, a remediation plan,
 * two engine analyses — and leaves them at the root of the repository with the
 * date in the file name. Those documents are the history: they say what broke,
 * what the evidence was, what was changed, and what is still open. Everything
 * below is drawn from them, and each entry names the file it came from so a
 * reader can open it and check the claim.
 *
 * If a document is reissued or a phase closes, edit this file. It is data, not
 * prose, precisely so that it can be corrected without rewriting a page.
 */

export type HistoryEntry = {
  /** ISO date, rendered as written. */
  date: string;
  /** Short label for the timeline marker. */
  tag: string;
  title: string;
  body: string;
  /** Where this comes from. Shown on the card, and it is a real path. */
  source: string;
};

/** The working documents the timeline is built from. The sync designs live on
 * the development line whose work they record; everything else is at the root
 * of this repository, and the entries they feed say so where it matters. */
export const workingDocuments = [
  "AUDIT_REPORT_Optimization_Step5.md",
  "FORENSIC_AUDIT_RC009_JOBS_TO_SIZES_2026-09-20.md",
  "OPTIMIZATION_MODULE_SENIOR_AUDIT_REPORT.md",
  "OPTIMIZATION_ENGINE_REMEDIATION_PLAN_2026-09-20.md",
  "ENGINE_STRATEGY_ANALYSIS_2026-09-26.md",
  "OPTIMIZATION_SETTINGS_CUT_PATTERN_ANALYSIS_2026-09-26.md",
  "website/docs/DELIVERY_TRACKING_ANALYSIS.md",
  "docs/proglass-sync-architecture-audit.md · development line",
  "docs/proglass-sync-implementation-design.md · development line",
] as const;

export const history: HistoryEntry[] = [
  {
    date: "20 September 2026",
    tag: "The report",
    title: "The cutting list came back empty",
    body: "An operator ticked a job, the specification panel filled in, the pane checklist filled in — and the cutting-size list stayed empty. A first pass was made and a syntax fault corrected. The symptom survived it. So the next pass began by refusing to trust that first fix: it is described in the document as unverified and not defended, and nothing downstream was allowed to assume it had been right.",
    source: "AUDIT_REPORT_Optimization_Step5.md",
  },
  {
    date: "20 September 2026",
    tag: "The diagnosis",
    title: "The data was not lost in the binding",
    body: "The second pass answered, to file and line, which object holds the cutting sizes when a job is selected, which collections the SIZES tab binds to, and where the values disappear between the two. The answer was four conversion boundaries: a Step 5 filter that checked width but not height; a stock lookup that returned false on a cold cache, so the first run of the day had no racks at all; DGU and lamination builds that carry no sheet sizes by design; and a race in which a view-model's constructor loaded jobs on a background thread and, finishing late, cleared the cutting list that had just been built.",
    source: "FORENSIC_AUDIT_RC009_JOBS_TO_SIZES_2026-09-20.md",
  },
  {
    date: "20 September 2026",
    tag: "The fix",
    title: "A host that owns its own work",
    body: "The last of those four was the interesting one. The first attempt had patched the wrong layer, which is why the fault kept surviving fixes. The correction gave an optimization host a stated identity — a headless host, one created to run Step 5 rather than to be looked at — so it alone owns its pieces and its stocks, and a background job that finishes later cannot reach in and empty them. That contract is now true by construction rather than by timing.",
    source: "AUDIT_REPORT_Optimization_Step5.md §A",
  },
  {
    date: "20 September 2026",
    tag: "The plan",
    title: "Twenty-three findings, and one rule",
    body: "The audit was turned into a remediation plan against the build it was raised on, with 23 findings written out across eight phases: margin authority, input parsing, document identity, verification, stock routing, object lifetime, the save pipeline, and scale. It opens with a rule that explains the whole project — nothing in the plan counts as fixed until it is proven on a real build. Findings carry their status honestly: 17 landed with what changed, and 11 still open with the reason each one is open.",
    source: "OPTIMIZATION_ENGINE_REMEDIATION_PLAN_2026-09-20.md",
  },
  {
    date: "20 September 2026",
    tag: "The domain",
    title: "The margins came from the shop floor",
    body: "One finding is a good example of what the software is for. The safety margins the packer works to were not chosen by a developer: the shop owner supplied them — 15 mm on every edge up to 6 mm glass, 30 mm for 7 and 8 mm, 50 mm for 9 mm and above with no bottom margin at all, and a kerf of 3 mm rising to 4 mm. Thicknesses the table does not name round up to the next bucket, so 7 mm takes the 8 mm profile. Thirteen tests pin the table so it cannot drift.",
    source: "OPTIMIZATION_ENGINE_REMEDIATION_PLAN_2026-09-20.md · house margin table",
  },
  {
    date: "26 September 2026",
    tag: "The engine",
    title: "Counting what is actually in there",
    body: "Before changing the optimizer, a strategy analysis counted what it already contains: a pool of independent generators, each producing a complete plan from scratch, with a streaming minimum keeping the best of them and no plan ever mutated into another. Shelf and strip packers, capacity-aware strips, per-plate best-split with beam lookahead, fill and seed variants, a non-guillotine placer, a repeat-master for pattern jobs, a batched parallel path for very large jobs, and an offcut-reuse pass on the end.",
    source: "ENGINE_STRATEGY_ANALYSIS_2026-09-26.md",
  },
  {
    date: "26 September 2026",
    tag: "The correction",
    title: "Five strategies were one algorithm at five budgets",
    body: "The last analysis ends with an answer its author did not want. The five \"strategies\" the application offers are one algorithm run at five different time budgets, and the engine records no cut sequence and enforces no cut-pattern constraint — so adding more strategy names would move nothing. It ranks what would actually help, in order: capture the cut sequence, reuse remnants, and then make cut complexity a constraint the packer has to respect. It says so on the page rather than in a private note.",
    source: "OPTIMIZATION_SETTINGS_CUT_PATTERN_ANALYSIS_2026-09-26.md",
  },
  {
    date: "2 October 2026",
    tag: "The calendar",
    title: "One date format, everywhere the site shows it",
    body: "The records pages on this website read dates from files written by different machines, and the first answer — one more parser at each table — was refused. A single date module now normalizes what arrives to DD-MM-YYYY, sorts ascending by the real date rather than by text, and serializes back the same way on update, so the timeline a customer reads cannot depend on which workstation saved the file.",
    source: "website/src/lib/date-utils.ts · website/src/lib/records-api.ts",
  },
  {
    date: "3 October 2026",
    tag: "The fallback",
    title: "Do not trust the fast path",
    body: "The dashboard's aggregation had one lookup, and a day was spent making its failure boring: freshness is compared across candidate files rather than trusting the newest name, candidates are ranked across several columns, the RPC aggregate carries a direct-table fallback with a cache reload behind it, and a known-missing answer is memoized briefly so an error never becomes a request storm. Six commits in one day, each removing a different way the same query could have lied.",
    source: "website/src/lib/records-api.ts · website/src/components/records/RecordsDashboard.tsx",
  },
  {
    date: "5 October 2026",
    tag: "The ledger",
    title: "Deliveries became a tracker, not a page",
    body: "The confirmed proformas a shop still owes glass against became their own surface: a dashboard that tracks the remaining obligation per invoice and keeps a movement ledger behind every row, so 'partially delivered' is a state with paperwork rather than a remark. The analysis that drove it is dated and in the repository, and the summary cards, filters and ledger are the website's own components.",
    source: "website/docs/DELIVERY_TRACKING_ANALYSIS.md · website/src/components/deliveries/",
  },
  {
    date: "6 October 2026",
    tag: "The audit",
    title: "The sync was designed before it was built",
    body: "Before any synchronization code changed, the application's side of it was audited and then written up as a design that asked for approval: ProGlass remains the source of truth, every local workflow keeps working with the cable unplugged, each document type gets its own destination contract, and the website only ever displays what was published successfully. Both documents state their own verification boundary — no live Supabase schema or deployment was inspected — which is exactly the sort of sentence this page exists to quote.",
    source: "docs/proglass-sync-architecture-audit.md · docs/proglass-sync-implementation-design.md (development line)",
  },
  {
    date: "6 October 2026",
    tag: "The register",
    title: "One view for the whole chain",
    body: "The document register landed as a module in its own right: proforma invoices with their issue date, company, customer, project, value in AED and status, every row carrying the job orders raised from it and the delivery notes written against those. The inquiry-to-delivery chain, read sideways, from one screen.",
    source: "Views/DocumentRegister/ (development line)",
  },
  {
    date: "7 October 2026",
    tag: "The binding",
    title: "Credentials that know which project they belong to",
    body: "Records Sync grew its settings page — URL, auth email, API key and password, with Save and Test commands — and the discipline that makes a background service safe to ship: credentials bind to the pinned production project at save time rather than being trusted whenever a sync runs, and credential binding, URL validation and client behaviour are held in place by their own test suites. The application moved to 2.0.13 and then 2.0.15 in the same two days.",
    source: "Services/RecordsSync/ · Tests/RecordsSync/ · ProGlassAutomation.csproj (development line)",
  },
  {
    date: "Today",
    tag: "The state",
    title: "2.0.11 shipped, 2.0.15 in the workshop",
    body: "The release line this website ships beside is version 2.0.11 — the number in the project's own file. The development line is at 2.0.15, carried there by the sync work above. And the remediation plan's status column still shows one phase in progress with eleven findings open and the reason each is open — including two that need a decision from the shop owner rather than a patch from a developer. That is the honest state of the project, and it is why this page can be specific.",
    source: "OPTIMIZATION_ENGINE_REMEDIATION_PLAN_2026-09-20.md · ProGlassAutomation.csproj",
  },
];

/**
 * How the work is done, in the author's own terms.
 *
 * These are habits the documents above demonstrate — each one is visible in the
 * files they cite, so none of them is a claim about someone's character.
 */
export const workingMethod = [
  {
    index: "01",
    title: "Write it down before changing it",
    body: "Every fault on this page was described in full — symptom, scope, file and line — before a line of the fix was written. The document exists first; the change follows it.",
  },
  {
    index: "02",
    title: "Do not trust the previous fix",
    body: "When a symptom survived its first correction, the correction was marked unverified and the question was asked again from the data. That is what turned four boundary faults up.",
  },
  {
    index: "03",
    title: "Name what is still open",
    body: "Each document ends with the work that was not done and why — sometimes because it needs a decision from the shop owner, not a patch. Nothing is quietly dropped.",
  },
  {
    index: "04",
    title: "Prove it on a real build",
    body: "A finding is not closed because the code looks right. The plan lists the runtime checks a change still has to pass, by hand, on a running application.",
  },
];

/**
 * The journey, as phases rather than a timeline of dates.
 *
 * The timeline below this is the engineering record — what broke, when, and
 * what was changed. This is the other half: how an estimator with no software
 * background ended up maintaining a commercial product, told in the order it
 * happened, including the part that has not happened yet.
 *
 * Phases 1 to 4 are the past and are backed by the repository. Phase 5 is a
 * stated intention, and it is written as one.
 */
export type JourneyPhase = {
  index: string;
  /** Where this phase sits in time. */
  when: string;
  title: string;
  body: string;
  /** What it left behind, in the files. Empty for the phase that has not run. */
  proof?: string;
};

export const journey: JourneyPhase[] = [
  {
    index: "01",
    when: "Before any of this",
    title: "The trade came first",
    body: "Sizing, pricing, wastage allowances, delivery charges, the same build-up priced again for a different customer. Estimating glass is not difficult arithmetic — it is a lot of small numbers held in the head at once, every day, on paper. That is where the mistakes lived, and it was clear which parts of the day kept going wrong.",
    proof: "The working day the software was written to fix — see the estimator's day below.",
  },
  {
    index: "02",
    when: "The first version",
    title: "A tool for one person, not a product",
    body: "The first version was written to stop retyping. It had no plan behind it, no company, and nobody to sell it to — just an estimator removing the parts of his own day he kept getting wrong. Everything in the current application that feels like a working day rather than a data model was decided in this phase.",
    proof: "src/data/developerHistory.ts · `workingMethod`, and the decisions listed under 'what that background produced'.",
  },
  {
    index: "03",
    when: "The realisation",
    title: "The same day is happening in glass shops everywhere",
    body: "What turned a private tool into something worth maintaining was the discovery that the day it was built around is not one shop's day. The same build-ups, the same offcut problem, the same delivery balance that gets argued about at month end — in a trade where the software on offer was either generic ERP or a spreadsheet.",
    proof: "The module set, from inquiry through the eight production stages to the statement.",
  },
  {
    index: "04",
    when: "The hard part",
    title: "Learning to trust the record, not the memory",
    body: "The engine taught the hardest lesson of the project. A cutting list came back empty, the first fix did not hold, and the symptom survived it — which forced a second pass that refused to assume the first was right. That pass turned up four separate boundary faults, and it produced the habit that now runs through the whole build: write the fault down before touching it, and name what is still open.",
    proof: "AUDIT_REPORT_Optimization_Step5.md · FORENSIC_AUDIT_RC009_JOBS_TO_SIZES_2026-09-20.md",
  },
  {
    index: "05",
    when: "Now",
    title: "Where it has to go next",
    body: "2.0.11 is in use on the commercial side: inquiry to statement, with the optimizer and the production stages behind it. It is also one person working evenings on borrowed machines, and that is the limit — not ideas, not demand. The next phase is a company rather than an author: people who can be paid, a machine of its own, and a data owner for the module that needs one.",
  },
];

/**
 * The thing the author says slowed him down everywhere except in the code.
 *
 * "When overthinking is used in success" is his own phrase for it: the habit of
 * re-checking everything, which is a liability in a conversation and the reason
 * a cutting plan can be trusted. Each point below is visible in the documents
 * the timeline cites, so this is not a claim about a personality — it is what
 * the paper trail shows the habit produced.
 */
export const overthinking = {
  /** The one-line thesis, set as the section's own statement. */
  thesis: "Overthinking is a liability almost everywhere. Inside a cutting engine it is the whole product.",
  /** The habit described plainly, before the evidence. */
  lead:
    "The author's own description of himself is not flattering: he re-reads, re-checks and re-runs things long after a reasonable person would have stopped. It is the reason this project has taken the time it has, and it is also the reason the software can be trusted, because a habit that is expensive in a meeting is exactly what a verification layer is made of.",
  /** Four consequences, each demonstrated by a real document. */
  points: [
    {
      index: "01",
      title: "The first fix was not trusted",
      body: "When the cutting list came back empty a second time, the working assumption was that the previous correction had been wrong — and it was recorded as unverified rather than quietly kept. A less careful author would have built on it.",
      proof: "AUDIT_REPORT_Optimization_Step5.md",
    },
    {
      index: "02",
      title: "Everything is written down first",
      body: "Six working documents sit at the root of the repository, each written before the change it describes: symptom, scope, file and line. They are the project's memory, and they are the reason a decision made in September can still be argued about in October.",
      proof: "OPTIMIZATION_ENGINE_REMEDIATION_PLAN_2026-09-20.md",
    },
    {
      index: "03",
      title: "Nothing ships on the author's word",
      body: "Two independent oracles check every optimization result, and a plan that fails either one is shown but never exported. The tests are invariants rather than getters — they assert what has to be true, which is what someone who does not trust himself writes.",
    },
    {
      index: "04",
      title: "And it is why this is slow",
      body: "The same habit sets the pace. Twenty-three findings with eleven still open, one phase in progress and the rest not started, is what thoroughness looks like from the outside — and it is the honest reason this page exists rather than a finished product announcement.",
      proof: "OPTIMIZATION_ENGINE_REMEDIATION_PLAN_2026-09-20.md · status",
    },
  ],
  /** The cost, said out loud, because the section would be dishonest without it. */
  cost:
    "The cost is real: it is slow, it is lonely, and it is not a personality trait a shop owner would put in a job advert. The claim here is not that overthinking is a virtue. It is that this particular product needs it, and that the trait has somewhere to go.",
};

/**
 * The dream, as the author states it.
 *
 * Aspiration, labelled as aspiration. Nothing here is a forecast, a valuation or
 * a claim of progress — it is what the work is for, which is a different kind of
 * statement and is allowed to be one.
 */
export const dream = {
  lead:
    "The intention is not a side project that stays a side project. It is to run a business around this software — a small, durable industrial software company with people in it, selling to glass fabricators who need exactly this and cannot buy it anywhere else.",
  /** What that means in practice, stated as intentions rather than results. */
  intentions: [
    {
      title: "A company with people in it",
      body: "Support that answers in a working day, testing by somebody other than the author, localisation reviewed by speakers of the language, and eventually a second developer on the engine.",
    },
    {
      title: "A product shops buy and keep",
      body: "Licensed, not rented; local data, no account to create; software a shop can run for years without being migrated onto somebody's cloud.",
    },
    {
      title: "Growth measured in shops, not in funding",
      body: "The measure that matters is how many fabricators run their day on it and quote jobs correctly because of it — not how much was raised to get there.",
    },
    {
      title: "The years already in it, not wasted",
      body: "Whatever happens next, the point of the exercise is that this does not end as an unfinished folder on a borrowed laptop.",
    },
  ],
  /** First person, because this part is the author's own and reads like it. */
  quote:
    "I came to this from the glass trade, not from software. I am not a company, I do not have a team, and I have written this in the hours around a job that also expects me to estimate. What I do have is a product that works, a record of everything that is wrong with it, and no intention of letting it stop here.",
  attribution: "Juber Saleem · author of ProGlass Automation",
};

/**
 * The technical implementation ledger.
 *
 * Maps high-level workflow stages to their exact C# services and architectural
 * guarantees inside the ProGlass Automation application tree.
 */
export type TechnicalWorkflowMapping = {
  stage: string;
  index: string;
  sourceFiles: string[];
  architecture: string;
};

export const technicalWorkflowArchitecture: TechnicalWorkflowMapping[] = [
  {
    stage: "Inquiry to BOQ Conversion",
    index: "01",
    sourceFiles: ["Services/ProformaInvoice/InquiryBoqConversion.cs", "Models/Inquiry/InquiryRecord.cs"],
    architecture:
      "Atomic BOQ row transfer into the Proforma specification grid. Unparseable rows are preserved as annotations rather than dropped. Conversion state changes are permission-gated and logged.",
  },
  {
    stage: "Glass Build-up Estimation",
    index: "02",
    sourceFiles: ["Services/Glass/GlassBuildUpCalculator.cs", "Services/Glass/PaneCostingEngine.cs"],
    architecture:
      "Pane-by-pane multi-lite calculations for SGU, DGU, Laminated, and Hybrid constructions. Applies base rates, interlayer costs, edge processing, and minimum 0.5 m² area rules.",
  },
  {
    stage: "Proforma & Pricing Stamper",
    index: "03",
    sourceFiles: ["Views/ProformaInvoice/Step9ProfitLoss/", "Services/ProformaInvoice/GlassPriceStamper.cs"],
    architecture:
      "Step 9 serves as the authoritative pricing engine. The calculated price basis and profit snapshot are stamped permanently onto the quotation record to prevent retrospective margin drift.",
  },
  {
    stage: "Job Order Mapping",
    index: "04",
    sourceFiles: ["Services/JobOrder/Mapper/JobOrderMapper.cs", "Models/JobOrder/SavedJobOrder.cs"],
    architecture:
      "Lossless conversion of confirmed proforma lines into factory job orders. Exact panel dimensions, glass types, spacer specifications, and tolerances cross without manual re-keying.",
  },
  {
    stage: "Production Control Center",
    index: "05",
    sourceFiles: ["ViewModels/Production/ProductionControlCenterViewModel.cs", "Models/Production/"],
    architecture:
      "State-machine driven queue management across 8 factory workstations (Cutting through Lamination). Dispatches, holds, and releases jobs with explicit blockage reason telemetry.",
  },
  {
    stage: "Cutting Optimization Engine",
    index: "06",
    sourceFiles: ["Services/Optimization/GuillotinePacker.cs", "Services/Optimization/PlatePatternOracle.cs"],
    architecture:
      "5 search families plus Auto, 6 plate patterns, and 6 cutting complexity levels. Enforces blade kerf and safety trim margins. Double verification oracles validate geometry before machine export.",
  },
  {
    stage: "Delivery Balance Tracking",
    index: "07",
    sourceFiles: ["Services/DeliveryNote/DeliveryNoteValidationService.cs", "Models/DeliveryNote/"],
    architecture:
      "Double-count-safe balance tracking supporting multiple incremental delivery movements per order. Prevents shipping quantities exceeding remaining order line balances.",
  },
  {
    stage: "Tax Invoice & VAT Calculation",
    index: "08",
    sourceFiles: ["Services/Reporting/VatCalculator.cs", "Services/TaxInvoice/"],
    architecture:
      "Centralized UAE VAT (5%) rounding engine. Directly converts delivery note movements or proforma invoices into fiscal tax invoices with line-level rounding consistency.",
  },
  {
    stage: "Reporting & Ledger Synthesis",
    index: "09",
    sourceFiles: ["ViewModels/JobProfitability/", "Services/Reporting/ReportingExportService.cs"],
    architecture:
      "Persisted document aggregation across sales, profitability, statements, and VAT returns. Reads stamped historical cost bases rather than recalculating past quotes.",
  },
];


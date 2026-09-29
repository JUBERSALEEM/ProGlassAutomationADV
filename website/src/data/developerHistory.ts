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

/** The six working documents the timeline is built from. */
export const workingDocuments = [
  "AUDIT_REPORT_Optimization_Step5.md",
  "FORENSIC_AUDIT_RC009_JOBS_TO_SIZES_2026-09-20.md",
  "OPTIMIZATION_MODULE_SENIOR_AUDIT_REPORT.md",
  "OPTIMIZATION_ENGINE_REMEDIATION_PLAN_2026-09-20.md",
  "ENGINE_STRATEGY_ANALYSIS_2026-09-26.md",
  "OPTIMIZATION_SETTINGS_CUT_PATTERN_ANALYSIS_2026-09-26.md",
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
    date: "Today",
    tag: "The state",
    title: "2.0.11, with the open work named",
    body: "The application is at version 2.0.11. The remediation plan's own status column still shows one phase in progress and the rest not yet started, and its carried-forward table lists eleven findings with the reason each is still open — including two that need a decision from the shop owner rather than a patch from a developer. A plan that still shows its own unfinished work two weeks later is the most useful document in the repository, and it is why this page can be specific.",
    source: "OPTIMIZATION_ENGINE_REMEDIATION_PLAN_2026-09-20.md · status",
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

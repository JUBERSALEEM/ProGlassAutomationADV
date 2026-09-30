/**
 * The production floor — the eight stage queues, as the application runs them.
 *
 * `Views/Production/README.md` is explicit about what these screens are, and
 * none of it was on the website before this file: the site was showing the
 * *optimizer's* piece list on the stage pages, which is a different screen with
 * different columns.
 *
 * What the README locks down, and what this file therefore states as data:
 *
 *   SOURCE LOCK   Production runs exclusively from Job Orders, and a job order
 *                 only exists for a confirmed (Approved) proforma. Drafts never
 *                 reach the floor; only non-void Released / In Production job
 *                 orders are releasable.
 *
 *   SCOPE LOCK    Four Step-4 build families — SGU, DGU, LAM, DGU+LAM. No triple
 *                 glazing. A name outside the family degrades with a warning
 *                 rather than being silently accepted.
 *
 *   PIECE FLOW    Every piece enters at Cutting. Only the pieces a stage
 *                 completes arrive in the next stage, and a floor write is
 *                 clamped to what arrived (`ComputeUnitFlows` +
 *                 `SetDoneQtyAsync`, so a station cannot report work it never
 *                 received). A unit is READY FOR DISPATCH when its full quantity
 *                 finishes the chain.
 *
 * The rows are the seeded job's own. `seedSpecGroups` in `workedExample.ts`
 * holds PI-2026-001's three specification groups — 22 lines, 83 pieces — and
 * this file takes them from there rather than restating them, so the floor and
 * the quotation cannot disagree.
 */

import { seedParties, seedSpecGroups, seededDocuments } from "@/data/workedExample";

/** The seeded numbers, taken from the documents rather than retyped. */
const PI = seededDocuments.find((doc) => doc.key === "proforma")?.example ?? "PI-2026-001";
const JOB = seededDocuments.find((doc) => doc.key === "job")?.example ?? "JOB-2026-001";

/** Step 6 charge keys that raise floor work, with the stage they arrive at. */
export const stageRouting = [
  { charge: "FAN_HOLES", stage: "Holes & Cutouts", quantity: "Pieces" },
  { charge: "HOLES", stage: "Holes & Cutouts", quantity: "Pieces" },
  { charge: "CUTOUT", stage: "Holes & Cutouts", quantity: "Pieces" },
  { charge: "COUNTERSUNK_HOLES", stage: "Holes & Cutouts", quantity: "Pieces" },
  { charge: "POLISH", stage: "Polish", quantity: "Linear metres" },
  { charge: "POLISH_MATCHED_LAM", stage: "Polish", quantity: "Linear metres" },
  { charge: "EDGE_GRINDING", stage: "Polish", quantity: "Linear metres" },
  { charge: "MITRING", stage: "Mitring", quantity: "Linear metres" },
  { charge: "ARGON", stage: "Assembly notes", quantity: "Note on the assembly item" },
  { charge: "SILICON", stage: "Assembly notes", quantity: "Note on the assembly item" },
  { charge: "U-INSERT", stage: "Assembly notes", quantity: "Note on the assembly item" },
  { charge: "OVERLAP", stage: "Assembly notes", quantity: "Note on the assembly item" },
  { charge: "SHUTTER_BONDING", stage: "Assembly notes", quantity: "Note on the assembly item" },
] as const;

/**
 * How a row routes, by build family and by pane treatment.
 *
 * SGU rows carry W1/H1 only. Annealed single glazing is finished when it is
 * cut, so it goes straight to dispatch-ready. Float, heat-strengthened and
 * heat-soaked panes have to be tempered first. Anything multi-pane splits into
 * pane occurrences — outer W1/H1, inner or laminated pair W2/H2 — and then
 * meets at Pairing and Doubling / Lamination.
 */
export const stageRoutes = [
  {
    family: "SGU",
    carries: "W1 / H1 only",
    route: "Cutting → Tempering (FT / heat-strengthened / heat-soaked) or Cutting → dispatch-ready (annealed)",
  },
  {
    family: "DGU",
    carries: "Outer W1 / H1 and inner W2 / H2",
    route: "Cutting → … → Pairing → Doubling / Lamination → dispatch-ready",
  },
  {
    family: "LAM",
    carries: "Laminate pair",
    route: "Cutting → … → Pairing → Doubling / Lamination → dispatch-ready",
  },
  {
    family: "DGU + LAM",
    carries: "Outer, inner and laminate pair",
    route: "Cutting → … → Pairing → Doubling / Lamination → dispatch-ready",
  },
] as const;

/** The eight stages, in floor order, as the chain bar draws them. */
export const stageChain = [
  { id: "Cutting", label: "Cutting", metric: "pcs" },
  { id: "HolesCutouts", label: "Holes & Cutouts", metric: "pcs" },
  { id: "Polish", label: "Polish", metric: "LM" },
  { id: "EdgeDeletion", label: "Edge Deletion", metric: "pcs" },
  { id: "Mitring", label: "Mitring", metric: "LM" },
  { id: "Tempering", label: "Tempering", metric: "pcs" },
  { id: "Pairing", label: "Pairing", metric: "pairs" },
  { id: "DoublingLamination", label: "Doubling / Lamination", metric: "units" },
] as const;

export type StageChainId = (typeof stageChain)[number]["id"];

/** 83 pieces across the seeded job’s three groups — from the quotation itself. */
export const seededPieceCount = seedSpecGroups.reduce(
  (total, group) => total + group.lines.reduce((sum, line) => sum + line.qty, 0),
  0,
);

export type StageRow = {
  st: string;
  job: string;
  pi: string;
  customer: string;
  salesman: string;
  spec: string;
  sr: string;
  ref: string;
  build: string;
  w1: string;
  h1: string;
  w2: string;
  h2: string;
  qty: number;
  /** `IN` — what the previous stage actually completed, which is the ceiling. */
  inQty: number;
  /** `DONE` — what this station has recorded. Never more than `inQty`. */
  doneQty: number;
  /** True when nothing has reached this stage yet — the waiting-upstream case. */
  waitingUpstream: boolean;
};

/**
 * How far each station is through the work it has received.
 *
 * Cutting is the one with the queue: the job has been released and the floor is
 * part-way through the first stage. Every station after it is roughly a day
 * behind the one before, which is what makes the chain bar's numbers fall away
 * down the line instead of the whole floor reporting the same total.
 */
const stageProgress: Record<StageChainId, number> = {
  Cutting: 0.86,
  HolesCutouts: 0.9,
  Polish: 0.9,
  EdgeDeletion: 0.9,
  Mitring: 0.85,
  Tempering: 0.85,
  Pairing: 0.8,
  DoublingLamination: 0.8,
};

/**
 * Which stages a row passes through — the routing rules, per row.
 *
 * SGU rows carry W1/H1 only. A tempered single pane has to go through Tempering;
 * anything multi-pane splits into pane occurrences and meets again at Pairing
 * and Doubling / Lamination. SGU rows never reach those two: a single pane is
 * finished when it comes off its own chain.
 */
function routeFor(build: string, ref: string): StageChainId[] {
  /*
   * Only the group the seeder names "Clear Tempered" carries a glass reference
   * that says it is heat-treated — `CT-`. The temperature treatment is not a
   * field in the seeded data, so nothing here guesses at it: the tempered route
   * is the one the reference states, and the rest take the annealed route past
   * Tempering. Every stage's numbers are therefore the work that belongs to that
   * stage, not the whole job copied eight times.
   */
  const tempered = ref.startsWith("CT-");
  if (build === "DGU" || build === "LAM") {
    return [
      "Cutting",
      "HolesCutouts",
      "Polish",
      "EdgeDeletion",
      "Mitring",
      ...(tempered ? (["Tempering"] as const) : []),
      "Pairing",
      "DoublingLamination",
    ];
  }
  return ["Cutting", "HolesCutouts", "Polish", "EdgeDeletion", "Mitring", ...(tempered ? (["Tempering"] as const) : [])];
}

/**
 * The seeded job's specification rows, walked down the chain to a stage.
 *
 * The walk is the point, and it is the application's rule: `inQty` at a stage is
 * what the *previous* stage completed, not the row's original quantity. So a row
 * of 8 pieces may have 8 arrived at Cutting, 6 at Holes & Cutouts and none at all
 * at Tempering — and the grid shows it that way rather than pretending every
 * station has the whole job in front of it. A stage never records more than
 * arrived, which is the clamp `SetDoneQtyAsync` enforces.
 *
 * A row that does not pass through a stage — an SGU pane has no pairing step —
 * simply has nothing there, so the stage's counts are the counts of the work
 * that actually belongs to it.
 */
export function stageRowsFor(stage: StageChainId): StageRow[] {
  const rows: StageRow[] = [];
  const chain = stageChain.map((entry) => entry.id);

  seedSpecGroups.forEach((group) => {
    group.lines.forEach((line) => {
      const route = routeFor(group.glassType, line.ref);
      const targetIndex = chain.indexOf(stage);

      /* Walk the row's own route, carrying what each station completed forward. */
      let carried = line.qty;
      let inAtTarget = 0;
      let doneAtTarget = 0;
      let onRoute = false;

      for (const step of chain) {
        if (!route.includes(step)) continue;
        const stepIndex = chain.indexOf(step);
        if (stepIndex > targetIndex) break;

        const arrived = carried;
        /*
         * The first station has the whole quantity and is part-way through it.
         * Every station after that can only be as advanced as the quantity that
         * actually reached it, so the figure decays down the line.
         */
        const done = arrived === 0 ? 0 : Math.max(1, Math.round(arrived * stageProgress[step]));

        if (step === stage) {
          inAtTarget = arrived;
          doneAtTarget = done;
          onRoute = true;
        }
        carried = done;
      }

      /* A row the stage's route never reaches is not part of that stage's queue. */
      if (!onRoute) return;

      const pane = line.secondPane;
      rows.push({
        st: String(targetIndex + 1),
        job: JOB,
        pi: PI,
        customer: seedParties.customer.name,
        salesman: seedParties.people.salesman,
        spec: `${group.number} — ${group.name}`,
        sr: String(line.serial),
        ref: line.ref,
        build: group.glassType,
        w1: String(line.widthMm),
        h1: String(line.heightMm),
        w2: pane ? String(pane.widthMm) : "—",
        h2: pane ? String(pane.heightMm) : "—",
        qty: line.qty,
        inQty: inAtTarget,
        doneQty: doneAtTarget,
        waitingUpstream: inAtTarget === 0,
      });
    });
  });

  return rows;
}

/** The chain bar's factory-wide counts — open · done / total, per stage. */
export function stageChainCounts(): { id: StageChainId; label: string; open: number; done: number; total: number }[] {
  return stageChain.map((stage) => {
    const rows = stageRowsFor(stage.id);
    const total = rows.reduce((sum, row) => sum + row.qty, 0);
    const open = rows.reduce((sum, row) => sum + Math.max(0, row.inQty - row.doneQty), 0);
    const done = rows.reduce((sum, row) => sum + row.doneQty, 0);
    return { id: stage.id, label: stage.label, open, done, total };
  });
}

/**
 * The release bar's state, from `ProductionReleaseService`.
 *
 * Only non-void job orders that are Released or In Production can be released
 * to the floor; asking for anything else is refused with the reason, not
 * silently ignored.
 */
export const releaseCandidates = [
  { job: JOB, status: "Released", proforma: PI, releasable: true, note: "Confirmed proforma — open on the floor" },
  { job: "JOB-2026-002", status: "Draft", proforma: "PI-2026-002", releasable: false, note: "Not released — a draft job order never reaches the floor" },
  { job: "JOB-2026-003", status: "Void", proforma: "PI-2026-003", releasable: false, note: "Voided — excluded from release" },
] as const;

/**
 * The KPI strip's labels, which are stage-aware: Polish and Mitring count linear
 * metres, Pairing counts pairs, Doubling counts units, and the rest count pieces.
 */
export const kpiLabels = {
  arrived: "Arrived",
  pending: "Pending",
  inProgress: "In progress",
  completed: "Completed",
  load: "Load",
  readyForDispatch: "Ready for dispatch",
} as const;

/**
 * What the application says when a release is refused, and when a write is
 * clamped — the two messages that make the floor's rules visible to the person
 * standing at the station.
 */
export const floorMessages = {
  refusedDraft: "Only Released or In Production job orders can go to the floor. A draft proforma never reaches production.",
  refusedVoid: "This job order is void and cannot be released.",
  clampedWrite: "Saved quantity was reduced to the pieces that actually arrived from the previous stage.",
  waitingUpstream: "Waiting upstream — no pieces have arrived at this stage yet.",
  readyForDispatch: "Full quantity has finished the chain. This unit is ready for dispatch.",
} as const;

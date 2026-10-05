/**
 * The one place that works out delivery balance.
 *
 * A glass order is rarely delivered once. A confirmed PI goes out in whatever
 * the vehicle and the site can take, so the tracker's unit is the PI and its
 * contents are movements. Every figure on the deliveries page — how much has
 * gone out, how much is owed, whether a PI is part-delivered — is computed
 * here and nowhere else, because the failure mode of the alternative is well
 * documented: two screens compute the balance slightly differently, three
 * people quote three numbers, and the job costs more than the glass did.
 *
 * The rules, in one list:
 *
 *   · A movement only counts when it has left the building. `Draft` and
 *     `Dispatching` notes are preparation, not dispatch — they appear in the
 *     ledger for completeness and contribute zero.
 *   · A voided movement never counts. Void is the ERP's soft-close; the row
 *     stays for audit and the maths skips it.
 *   · A `Return` movement subtracts. Items that came back are not delivered
 *     however they were invoiced.
 *   · `Rework` and `Replacement` movements add like any other dispatch, but
 *     they are the one legitimate reason a PI can end up over its ordered
 *     quantity — a broken pane that was re-cut and re-delivered is not an
 *     accounting error. Over-delivery is therefore flagged only for the part
 *     of the excess that rework does not explain.
 *
 * `website/supabase-deliveries.sql` mirrors these rules in the
 * `pi_delivery_balance` view so the database can answer the same question
 * without shipping every row to the browser. When one changes, the other must
 * change with it — that is the cost of having one owner instead of none.
 */

import type {
  DeliveryBreakdownItem,
  DeliveryBreakdowns,
  DeliveryFilterOptions,
  DeliveryLifecycle,
  DeliveryMovement,
  DeliveryRow,
  DeliverySort,
  DeliverySummary,
  DeliveryRecordRow,
} from "@/types/deliveries";

export type { DeliveryRecordRow };

/* ── Movement classification ─────────────────────────────────────────── */

/** Statuses that mean the note has not left the building yet, or never will. */
const NON_DISPATCH_STATUSES = new Set([
  "draft",
  "dispatching",
  "preparing",
  "cancelled",
  "canceled",
  "void",
  "voided",
  "reversed",
]);

/** Delivery types that carry a reworked or replacement pane. */
const REWORK_TYPES = new Set(["rework", "replacement", "remake"]);

const asNumber = (value: unknown): number => {
  if (typeof value === "number") return Number.isFinite(value) ? value : 0;
  if (typeof value === "string") {
    const parsed = Number(value.replace(/,/g, "").trim());
    return Number.isFinite(parsed) ? parsed : 0;
  }
  return 0;
};

const asText = (value: unknown): string => (typeof value === "string" ? value.trim() : "");

/** The first non-empty value among candidate column names (schema tolerance). */
export function pickField(row: Record<string, unknown>, names: string[]): unknown {
  for (const name of names) {
    const value = row[name];
    if (value !== null && value !== undefined && value !== "") return value;
  }
  return undefined;
}

export function isVoidMovement(movement: Pick<DeliveryMovement, "is_void" | "status">): boolean {
  if (movement.is_void) return true;
  return NON_DISPATCH_STATUSES.has(movement.status.trim().toLowerCase());
}

/**
 * Whether a movement counts against the ordered quantity.
 *
 * Drafts do not count, voids do not count, everything else does — including a
 * `Return`, whose quantity is applied as a subtraction in {@link summariseMovements}.
 */
export function movementCountsTowardBalance(
  movement: Pick<DeliveryMovement, "is_void" | "status">,
): boolean {
  return !isVoidMovement(movement);
}

export function isReworkMovement(movement: Pick<DeliveryMovement, "delivery_type">): boolean {
  return REWORK_TYPES.has(movement.delivery_type.trim().toLowerCase());
}

export function isReturnMovement(movement: Pick<DeliveryMovement, "delivery_type">): boolean {
  return movement.delivery_type.trim().toLowerCase() === "return";
}

/* ── Ledger arithmetic ───────────────────────────────────────────────── */

export type MovementTotals = {
  /** Sum of movements that count, returns already subtracted. */
  deliveredQty: number;
  deliveredSqm: number;
  /** Movements that count toward the balance (returns included as movements). */
  countedMovements: number;
  /** All movements on the ledger, void and draft included. */
  totalMovements: number;
  /** Quantity that came back, reported separately for the audit line. */
  returnedQty: number;
  returnedSqm: number;
  /** Quantity re-delivered as rework/replacement, reported separately. */
  reworkQty: number;
  reworkSqm: number;
  voidedMovements: number;
  draftMovements: number;
  signedMovements: number;
  /** The latest counted movement date, ISO-ish or the raw string when unparseable. */
  lastMovementDate: string | null;
  lastMovementEpoch: number;
};

const MOVEMENT_ORDER = (a: DeliveryMovement, b: DeliveryMovement): number => {
  const aTime = movementEpoch(a.delivery_date);
  const bTime = movementEpoch(b.delivery_date);
  if (aTime !== bTime) return aTime - bTime;
  const aSeq = a.sequence ?? Number.MAX_SAFE_INTEGER;
  const bSeq = b.sequence ?? Number.MAX_SAFE_INTEGER;
  if (aSeq !== bSeq) return aSeq - bSeq;
  return String(a.id).localeCompare(String(b.id));
};

/** Sorts a ledger into the order a human reads it: chronological, then sequence. */
export function sortMovements(movements: DeliveryMovement[]): DeliveryMovement[] {
  return [...movements].sort(MOVEMENT_ORDER);
}

export function movementEpoch(value: unknown): number {
  if (!value) return 0;
  const raw = String(value).trim();
  if (!raw) return 0;
  // DD-MM-YYYY / DD/MM/YYYY first: `new Date("01-10-2026")` reads as January 10.
  const dmy = raw.match(/^(\d{1,2})[-/.](\d{1,2})[-/.](\d{2,4})$/);
  if (dmy) {
    const day = Number(dmy[1]);
    const month = Number(dmy[2]) - 1;
    let year = Number(dmy[3]);
    if (year < 100) year += year >= 70 ? 1900 : 2000;
    return Date.UTC(year, month, day);
  }
  const parsed = new Date(raw).getTime();
  return Number.isFinite(parsed) ? parsed : 0;
}

/**
 * Sum a PI's ledger.
 *
 * Everything downstream — lifecycle, progress, summary cards, breakdowns —
 * reads this function's output rather than re-deriving anything from the
 * individual movements.
 */
export function summariseMovements(movements: DeliveryMovement[]): MovementTotals {
  let deliveredQty = 0;
  let deliveredSqm = 0;
  let returnedQty = 0;
  let returnedSqm = 0;
  let reworkQty = 0;
  let reworkSqm = 0;
  let countedMovements = 0;
  let voidedMovements = 0;
  let draftMovements = 0;
  let signedMovements = 0;
  let lastMovementEpoch = 0;
  let lastMovementDate: string | null = null;

  for (const movement of movements) {
    if (movement.is_void) {
      voidedMovements += 1;
      continue;
    }
    if (isVoidMovement(movement)) {
      draftMovements += 1;
      continue;
    }

    countedMovements += 1;
    const qty = asNumber(movement.delivered_qty);
    const sqm = asNumber(movement.delivered_sqm);
    const isReturn = isReturnMovement(movement);
    const isRework = isReworkMovement(movement);

    if (isReturn) {
      deliveredQty -= Math.abs(qty);
      deliveredSqm -= Math.abs(sqm);
      returnedQty += Math.abs(qty);
      returnedSqm += Math.abs(sqm);
    } else {
      deliveredQty += qty;
      deliveredSqm += sqm;
      if (isRework) {
        reworkQty += qty;
        reworkSqm += sqm;
      }
    }

    if (signedOffStatus(movement.status)) signedMovements += 1;

    const epoch = movementEpoch(movement.delivery_date);
    if (epoch >= lastMovementEpoch) {
      lastMovementEpoch = epoch;
      lastMovementDate = movement.delivery_date;
    }
  }

  return {
    deliveredQty: round4(deliveredQty),
    deliveredSqm: round2(deliveredSqm),
    countedMovements,
    totalMovements: movements.length,
    returnedQty,
    returnedSqm: round2(returnedSqm),
    reworkQty,
    reworkSqm: round2(reworkSqm),
    voidedMovements,
    draftMovements,
    signedMovements,
    lastMovementDate,
    lastMovementEpoch,
  };
}

function signedOffStatus(status: string): boolean {
  const normalized = status.trim().toLowerCase();
  return normalized === "signed" || normalized === "received";
}

/* ── Lifecycle ───────────────────────────────────────────────────────── */

/**
 * Where the PI stands.
 *
 * A PI whose ordered quantity is zero is treated as awaiting dispatch rather
 * than complete: a PI with `qty = 0` is a data fault, and calling it delivered
 * would hide it. Anything strictly past the ordered quantity is `over_delivered`
 * unless rework explains the whole excess.
 */
export function deliveryLifecycle(args: {
  orderedQty: number;
  deliveredQty: number;
  reworkQty?: number;
}): DeliveryLifecycle {
  const { orderedQty, deliveredQty } = args;
  const reworkQty = args.reworkQty ?? 0;

  if (deliveredQty <= 0) return "awaiting_dispatch";
  if (orderedQty <= 0) return "over_delivered";

  const excess = round4(deliveredQty - orderedQty);
  if (excess > 0.0001 && excess > reworkQty + 0.0001) return "over_delivered";
  if (deliveredQty + 0.0001 >= orderedQty) return "complete";
  return "partial";
}

export function lifecycleLabel(lifecycle: DeliveryLifecycle): string {
  switch (lifecycle) {
    case "awaiting_dispatch":
      return "Awaiting dispatch";
    case "partial":
      return "Part delivered";
    case "complete":
      return "Delivered";
    case "over_delivered":
      return "Over delivered";
  }
}

/**
 * The label back to the key.
 *
 * The breakdown panels are grouped by label (a human reads "Part delivered"),
 * but the filter and the API speak the lifecycle key. Round-tripping through
 * one function means a panel click cannot send a value the API will reject.
 */
export function lifecycleFromLabel(label: string): DeliveryLifecycle | "" {
  const normalized = label.trim().toLowerCase();
  if (normalized === "awaiting dispatch") return "awaiting_dispatch";
  if (normalized === "part delivered") return "partial";
  if (normalized === "delivered") return "complete";
  if (normalized === "over delivered") return "over_delivered";
  return "";
}

/* ── Obligations ─────────────────────────────────────────────────────── */

export type PiObligation = {
  pi_number: string;
  company: string;
  customer_ref: string;
  salesman: string;
  prepared_by: string;
  type_of_work: string;
  pi_status: string;
  zoho_status: string;
  record_status: string;
  order_date: string;
  latest_order_date: string;
  ordered_qty: number;
  ordered_sqm: number;
  record_row_count: number;
  record_row_ids: (string | number)[];
  source_file: string | null;
  /** True when at least one row carried a confirmed status. */
  has_confirmed_row: boolean;
};

const CONFIRMED_FALLBACK = ["Confirmed"];

/**
 * Collapse `public.records` rows into one obligation per PI number.
 *
 * The most recent row (by update timestamp, then order date) wins for the
 * descriptive columns, because a PI's company or salesman can be corrected in
 * Excel and the tracker should show the correction rather than the first draft
 * of it. Quantities sum, because a PI can legitimately span several rows
 * (several specification groups of the same order).
 */
export function groupRecordsIntoObligations(
  records: DeliveryRecordRow[],
  confirmedStatuses: string[] = CONFIRMED_FALLBACK,
): Map<string, PiObligation> {
  const confirmed = new Set(confirmedStatuses.map((status) => status.trim().toLowerCase()));
  const obligations = new Map<string, PiObligation>();

  for (const record of records) {
    if (record.is_active === false) continue;
    const pi = asText(record.pi_number);
    if (!pi) continue;

    const status = asText(record.status);
    const existing = obligations.get(pi);
    const qty = asNumber(record.qty);
    const sqm = asNumber(record.sqm);
    const date = asText(record.date);

    if (!existing) {
      obligations.set(pi, {
        pi_number: pi,
        company: asText(record.company),
        customer_ref: asText(record.customer_ref),
        salesman: asText(record.salesman),
        prepared_by: asText(record.prepared_by),
        type_of_work: asText(record.type_of_work),
        pi_status: asText(record.pi_status),
        zoho_status: asText(record.zoho_status),
        record_status: status,
        order_date: date,
        latest_order_date: date,
        ordered_qty: qty,
        ordered_sqm: sqm,
        record_row_count: 1,
        record_row_ids: [record.id],
        source_file: asText(record.source_file) || null,
        has_confirmed_row: confirmed.has(status.toLowerCase()),
      });
      continue;
    }

    const existingEpoch = Math.max(
      movementEpoch(existing.latest_order_date),
      asTimestamp(record.updated_at),
      asTimestamp(record.created_at),
    );
    const incomingEpoch = Math.max(
      movementEpoch(date),
      asTimestamp(record.updated_at),
      asTimestamp(record.created_at),
    );

    existing.ordered_qty = round4(existing.ordered_qty + qty);
    existing.ordered_sqm = round2(existing.ordered_sqm + sqm);
    existing.record_row_count += 1;
    existing.record_row_ids.push(record.id);
    existing.has_confirmed_row = existing.has_confirmed_row || confirmed.has(status.toLowerCase());

    if (movementEpoch(date) < movementEpoch(existing.order_date)) existing.order_date = date;
    if (movementEpoch(date) > movementEpoch(existing.latest_order_date)) {
      existing.latest_order_date = date;
    }

    if (incomingEpoch >= existingEpoch) {
      existing.company = asText(record.company) || existing.company;
      existing.customer_ref = asText(record.customer_ref) || existing.customer_ref;
      existing.salesman = asText(record.salesman) || existing.salesman;
      existing.prepared_by = asText(record.prepared_by) || existing.prepared_by;
      existing.type_of_work = asText(record.type_of_work) || existing.type_of_work;
      existing.pi_status = asText(record.pi_status) || existing.pi_status;
      existing.zoho_status = asText(record.zoho_status) || existing.zoho_status;
      existing.record_status = status || existing.record_status;
      existing.source_file = asText(record.source_file) || existing.source_file;
    }
  }

  return obligations;
}

function asTimestamp(value: unknown): number {
  if (!value) return 0;
  const parsed = new Date(String(value)).getTime();
  return Number.isFinite(parsed) ? parsed : 0;
}

/**
 * Build a tracker row from one obligation and its ledger.
 *
 * `now` is injected so the "stale" computation is testable and so a page render
 * does not depend on two different clocks.
 */
export function buildDeliveryRow(
  obligation: PiObligation,
  movements: DeliveryMovement[],
  now: Date = new Date(),
): DeliveryRow {
  const ledger = sortMovements(movements);
  const totals = summariseMovements(ledger);

  const orderedQty = obligation.ordered_qty;
  const orderedSqm = obligation.ordered_sqm;
  const balanceQty = round4(Math.max(0, orderedQty - totals.deliveredQty));
  const balanceSqm = round2(Math.max(0, orderedSqm - totals.deliveredSqm));
  const lifecycle = deliveryLifecycle({
    orderedQty,
    deliveredQty: totals.deliveredQty,
    reworkQty: totals.reworkQty,
  });

  const lastEpoch = totals.lastMovementEpoch;
  const lastDeliveryDate =
    totals.lastMovementDate === null ? null : totals.lastMovementDate.trim() || null;
  const daysSinceLastDelivery =
    lastEpoch > 0 ? Math.max(0, Math.floor((now.getTime() - lastEpoch) / 86_400_000)) : null;

  return {
    pi_number: obligation.pi_number,
    company: obligation.company,
    customer_ref: obligation.customer_ref,
    salesman: obligation.salesman,
    prepared_by: obligation.prepared_by,
    type_of_work: obligation.type_of_work,
    pi_status: obligation.pi_status,
    zoho_status: obligation.zoho_status,
    record_status: obligation.record_status,
    order_date: obligation.order_date,
    latest_order_date: obligation.latest_order_date,
    ordered_qty: orderedQty,
    ordered_sqm: orderedSqm,
    delivered_qty: totals.deliveredQty,
    delivered_sqm: totals.deliveredSqm,
    balance_qty: balanceQty,
    balance_sqm: balanceSqm,
    progress_pct:
      orderedQty > 0 ? round1((totals.deliveredQty / orderedQty) * 100) : totals.deliveredQty > 0 ? 100 : 0,
    delivery_status: lifecycle,
    delivery_count: totals.countedMovements,
    total_movement_count: totals.totalMovements,
    last_delivery_date: lastDeliveryDate,
    days_since_last_delivery: daysSinceLastDelivery,
    tracked_without_confirmed_status: !obligation.has_confirmed_row,
    record_row_count: obligation.record_row_count,
    record_row_ids: obligation.record_row_ids,
    source_file: obligation.source_file,
    movements: ledger,
  };
}

/* ── Filtering, sorting ──────────────────────────────────────────────── */

export type ResolvedDateRange = { from: string; to: string };

const withinRange = (date: string, range: ResolvedDateRange): boolean => {
  if (!range.from && !range.to) return true;
  const epoch = movementEpoch(date);
  if (epoch <= 0) return false;
  if (range.from && epoch < movementEpoch(range.from)) return false;
  if (range.to && epoch > movementEpoch(range.to)) return false;
  return true;
};

export type DeliveryRowFilter = {
  search?: string;
  dateRange?: ResolvedDateRange;
  company?: string;
  salesman?: string;
  preparedBy?: string;
  typeOfWork?: string;
  piStatus?: string;
  deliveryStatus?: string;
  balanceOnly?: boolean;
  pi?: string;
};

/** Applies the tracker's filters to built rows. Mirrors the SQL RPC's WHERE clauses. */
export function filterDeliveryRows(rows: DeliveryRow[], filters: DeliveryRowFilter): DeliveryRow[] {
  const search = (filters.search ?? "").trim().toLowerCase();
  const pi = (filters.pi ?? "").trim().toLowerCase();

  return rows.filter((row) => {
    if (pi && row.pi_number.toLowerCase() !== pi) return false;
    if (filters.company && row.company !== filters.company) return false;
    if (filters.salesman && row.salesman !== filters.salesman) return false;
    if (filters.preparedBy && row.prepared_by !== filters.preparedBy) return false;
    if (filters.typeOfWork && row.type_of_work !== filters.typeOfWork) return false;
    if (filters.piStatus && row.pi_status !== filters.piStatus) return false;
    if (filters.deliveryStatus && row.delivery_status !== filters.deliveryStatus) return false;
    if (filters.balanceOnly && row.balance_qty <= 0) return false;
    if (filters.dateRange && !withinRange(row.order_date, filters.dateRange)) return false;

    if (search) {
      const haystack = [
        row.pi_number,
        row.customer_ref,
        row.company,
        row.salesman,
        row.prepared_by,
        row.type_of_work,
        ...row.movements.map((movement) => movement.delivery_note_ref ?? ""),
      ]
        .join(" ")
        .toLowerCase();
      if (!haystack.includes(search)) return false;
    }

    return true;
  });
}

const LIFECYCLE_ORDER: Record<DeliveryLifecycle, number> = {
  over_delivered: 0,
  partial: 1,
  awaiting_dispatch: 2,
  complete: 3,
};

export function sortDeliveryRows(rows: DeliveryRow[], sort: DeliverySort): DeliveryRow[] {
  const direction = sort.direction === "asc" ? 1 : -1;
  const field = sort.field;

  return [...rows].sort((a, b) => {
    let comparison = 0;

    switch (field) {
      case "pi_number":
        comparison = naturalCompare(a.pi_number, b.pi_number);
        break;
      case "company":
        comparison = a.company.localeCompare(b.company);
        break;
      case "order_date":
        comparison = movementEpoch(a.order_date) - movementEpoch(b.order_date);
        break;
      case "ordered_qty":
        comparison = a.ordered_qty - b.ordered_qty;
        break;
      case "ordered_sqm":
        comparison = a.ordered_sqm - b.ordered_sqm;
        break;
      case "delivered_qty":
        comparison = a.delivered_qty - b.delivered_qty;
        break;
      case "delivered_sqm":
        comparison = a.delivered_sqm - b.delivered_sqm;
        break;
      case "balance_qty":
        comparison = a.balance_qty - b.balance_qty;
        break;
      case "balance_sqm":
        comparison = a.balance_sqm - b.balance_sqm;
        break;
      case "progress":
        comparison = a.progress_pct - b.progress_pct;
        break;
      case "delivery_count":
        comparison = a.delivery_count - b.delivery_count;
        break;
      case "last_delivery_date":
        comparison = movementEpoch(a.last_delivery_date) - movementEpoch(b.last_delivery_date);
        break;
      case "delivery_status":
        comparison = LIFECYCLE_ORDER[a.delivery_status] - LIFECYCLE_ORDER[b.delivery_status];
        break;
    }

    if (comparison !== 0) return comparison * direction;

    // Stable, deterministic tie-break: oldest PI first, then serial.
    const dateTie = movementEpoch(a.order_date) - movementEpoch(b.order_date);
    if (dateTie !== 0) return dateTie;
    return naturalCompare(a.pi_number, b.pi_number);
  });
}

/**
 * PI numbers are data, and data sorts the way a person reads it: `PI-…-1009`
 * before `PI-…-1010`. A plain string compare gets that wrong the first time a
 * number crosses a digit boundary.
 */
export function naturalCompare(a: string, b: string): number {
  return a.localeCompare(b, undefined, { numeric: true, sensitivity: "base" });
}

/* ── Aggregates ──────────────────────────────────────────────────────── */

const STALE_AFTER_DAYS = 14;

export function buildDeliverySummary(rows: DeliveryRow[]): DeliverySummary {
  let orderedQty = 0;
  let orderedSqm = 0;
  let deliveredQty = 0;
  let deliveredSqm = 0;
  let totalMovements = 0;
  let fullyDeliveredPIs = 0;
  let partiallyDeliveredPIs = 0;
  let awaitingDispatchPIs = 0;
  let overDeliveredPIs = 0;
  let stalePIs = 0;
  let largestBalanceSqm = 0;

  const balanceByCompany = new Map<string, number>();

  for (const row of rows) {
    orderedQty += row.ordered_qty;
    orderedSqm += row.ordered_sqm;
    deliveredQty += row.delivered_qty;
    deliveredSqm += row.delivered_sqm;
    totalMovements += row.delivery_count;
    largestBalanceSqm = Math.max(largestBalanceSqm, row.balance_sqm);

    switch (row.delivery_status) {
      case "complete":
        fullyDeliveredPIs += 1;
        break;
      case "partial":
        partiallyDeliveredPIs += 1;
        break;
      case "awaiting_dispatch":
        awaitingDispatchPIs += 1;
        break;
      case "over_delivered":
        overDeliveredPIs += 1;
        fullyDeliveredPIs += 1;
        break;
    }

    if (
      (row.delivery_status === "partial" || row.delivery_status === "awaiting_dispatch") &&
      row.days_since_last_delivery !== null &&
      row.days_since_last_delivery >= STALE_AFTER_DAYS
    ) {
      stalePIs += 1;
    }

    if (row.balance_sqm > 0) {
      const key = row.company || "(Unassigned)";
      balanceByCompany.set(key, round2((balanceByCompany.get(key) ?? 0) + row.balance_sqm));
    }
  }

  const totalBalanceSqm = round2(Math.max(0, orderedSqm - deliveredSqm));
  const concentration = Array.from(balanceByCompany.entries())
    .map(([name, balanceSqm]) => ({
      name,
      balanceSqm,
      share: totalBalanceSqm > 0 ? round1((balanceSqm / totalBalanceSqm) * 100) : 0,
    }))
    .sort((a, b) => b.balanceSqm - a.balanceSqm)
    .slice(0, 5);

  return {
    totalPis: rows.length,
    totalMovements,
    orderedQty: round4(orderedQty),
    orderedSqm: round2(orderedSqm),
    deliveredQty: round4(deliveredQty),
    deliveredSqm: round2(deliveredSqm),
    balanceQty: round4(Math.max(0, orderedQty - deliveredQty)),
    balanceSqm: totalBalanceSqm,
    progressPct: orderedSqm > 0 ? round1((deliveredSqm / orderedSqm) * 100) : 0,
    fullyDeliveredPIs,
    partiallyDeliveredPIs,
    awaitingDispatchPIs,
    overDeliveredPIs,
    stalePIs,
    averageMovementsPerPi: rows.length > 0 ? round2(totalMovements / rows.length) : 0,
    largestBalanceSqm: round2(largestBalanceSqm),
    concentration,
  };
}

function groupRows(
  rows: DeliveryRow[],
  keyOf: (row: DeliveryRow) => string,
): DeliveryBreakdownItem[] {
  const groups = new Map<string, DeliveryBreakdownItem>();
  const total = rows.length;

  for (const row of rows) {
    const name = keyOf(row).trim() || "(Unassigned)";
    const bucket = groups.get(name) ?? {
      name,
      count: 0,
      orderedQty: 0,
      orderedSqm: 0,
      deliveredQty: 0,
      deliveredSqm: 0,
      balanceQty: 0,
      balanceSqm: 0,
      percentage: 0,
    };

    bucket.count += 1;
    bucket.orderedQty = round4(bucket.orderedQty + row.ordered_qty);
    bucket.orderedSqm = round2(bucket.orderedSqm + row.ordered_sqm);
    bucket.deliveredQty = round4(bucket.deliveredQty + row.delivered_qty);
    bucket.deliveredSqm = round2(bucket.deliveredSqm + row.delivered_sqm);
    bucket.balanceQty = round4(bucket.balanceQty + row.balance_qty);
    bucket.balanceSqm = round2(bucket.balanceSqm + row.balance_sqm);
    groups.set(name, bucket);
  }

  return Array.from(groups.values())
    .map((bucket) => ({
      ...bucket,
      percentage: total > 0 ? round1((bucket.count / total) * 100) : 0,
    }))
    .sort((a, b) => b.count - a.count || b.balanceSqm - a.balanceSqm)
    .slice(0, 20);
}

export function buildDeliveryBreakdowns(rows: DeliveryRow[]): DeliveryBreakdowns {
  return {
    company: groupRows(rows, (row) => row.company),
    salesman: groupRows(rows, (row) => row.salesman),
    lifecycle: groupRows(rows, (row) => lifecycleLabel(row.delivery_status)),
    typeOfWork: groupRows(rows, (row) => row.type_of_work),
  };
}

export function buildDeliveryFilterOptions(
  rows: DeliveryRow[],
  extras: { sourceFiles?: string[]; currentSourceFile?: string | null } = {},
): DeliveryFilterOptions {
  const distinct = (getter: (row: DeliveryRow) => string): string[] => {
    const set = new Set<string>();
    for (const row of rows) {
      const value = getter(row).trim();
      if (value && value !== "(Unassigned)") set.add(value);
    }
    return Array.from(set).sort((a, b) => a.localeCompare(b));
  };

  const lifecycles: DeliveryLifecycle[] = [];
  for (const row of rows) {
    if (!lifecycles.includes(row.delivery_status)) lifecycles.push(row.delivery_status);
  }

  return {
    companies: distinct((row) => row.company),
    salesmen: distinct((row) => row.salesman),
    preparedBy: distinct((row) => row.prepared_by),
    typesOfWork: distinct((row) => row.type_of_work),
    piStatuses: distinct((row) => row.pi_status),
    lifecycles,
    recordStatuses: distinct((row) => row.record_status),
    sourceFiles: extras.sourceFiles ?? [],
    currentSourceFile: extras.currentSourceFile ?? null,
  };
}

/* ── Rounding helpers ────────────────────────────────────────────────── */

export function round2(value: number): number {
  return Math.round((value + Number.EPSILON) * 100) / 100;
}

export function round1(value: number): number {
  return Math.round((value + Number.EPSILON) * 10) / 10;
}

export function round4(value: number): number {
  return Math.round((value + Number.EPSILON) * 10_000) / 10_000;
}

/**
 * ProGlass Automation — Deliveries Tracker Types
 *
 * The deliveries tracker answers one question per proforma: **how much of what
 * was confirmed has actually left the building, and what is still owed?**
 *
 * It reads the same Excel-synced dataset the Reports dashboard reads
 * (`public.records`), but where Reports lists *every* row of every status, the
 * tracker only accepts a PI that has been marked **Confirmed** — or one that
 * already carries delivery movements, because a PI that shipped and later moved
 * to `Completed` must not vanish from the ledger mid-flight.
 *
 * The unit of tracking is the PI, not the record row. A single confirmed PI may
 * be fulfilled by several movements (partial deliveries), so every row carries
 * its own `movements[]` ledger — that is the structure the whole page is built
 * around.
 *
 * Balance arithmetic lives in exactly one place (`lib/delivery-ledger.ts` in
 * TypeScript, mirrored by the `pi_delivery_balance` view in
 * `website/supabase-deliveries.sql`). Nothing else is allowed to work it out
 * independently — that is the rule the product's own delivery-note validation
 * service follows, and the rule this tracker inherits.
 */

/**
 * The subset of `public.records` columns the tracker reads.
 *
 * Deliberately loose: the table is written by Excel Sync, so the tracker reads
 * what it needs and tolerates what it does not recognize rather than insisting
 * on a row shape the sync contract does not actually guarantee.
 */
export type DeliveryRecordRow = {
  id: string | number;
  date?: string | null;
  pi_number?: string | null;
  company?: string | null;
  customer_ref?: string | null;
  salesman?: string | null;
  prepared_by?: string | null;
  type_of_work?: string | null;
  pi_status?: string | null;
  zoho_status?: string | null;
  status?: string | null;
  qty?: number | string | null;
  sqm?: number | string | null;
  is_active?: boolean | null;
  source_file?: string | null;
  created_at?: string | null;
  updated_at?: string | null;
};

/** One delivery movement — a single dispatch against a PI. */
export type DeliveryMovement = {
  id: string | number;
  pi_number: string;
  /** User-facing delivery note reference, e.g. "DN-2026-001-02". */
  delivery_note_ref: string | null;
  /** Sequence within the PI when the ledger knows it (Master DN = 0). */
  sequence: number | null;
  /** Business date of the movement, normalized to DD-MM-YYYY for display. */
  delivery_date: string;
  delivery_time: string | null;
  delivered_qty: number;
  delivered_sqm: number;
  /** Original | Rework | Return | Replacement | Dummy (free string — data, not an enum). */
  delivery_type: string;
  /** Draft | Dispatching | Dispatched | Delivered | Signed | Received. */
  status: string;
  driver_name: string | null;
  vehicle_number: string | null;
  receiver_name: string | null;
  notes: string | null;
  is_void: boolean;
  /** True when this movement counts against the ordered quantity (see ledger rules). */
  counts_toward_balance: boolean;
  /** True when the receiver has signed for it (Signed / Received). */
  signed_off: boolean;
  source_file: string | null;
};

/** Where a PI stands relative to its ordered quantity. Derived — never stored. */
export type DeliveryLifecycle =
  | "awaiting_dispatch"
  | "partial"
  | "complete"
  | "over_delivered";

/**
 * One row of the tracker: a confirmed PI and its delivery ledger.
 *
 * `record_row_ids` lists the `public.records` rows the obligation was summed
 * from, so the tracker can link back to the exact entries the Reports dashboard
 * shows.
 */
export type DeliveryRow = {
  pi_number: string;
  company: string;
  customer_ref: string;
  salesman: string;
  prepared_by: string;
  type_of_work: string;
  /** The proforma's own status column from the workbook (Approved / Draft / Sent). */
  pi_status: string;
  zoho_status: string;
  /** The operational status observed most recently on the underlying records. */
  record_status: string;
  /** First order date on the PI (DD-MM-YYYY). */
  order_date: string;
  /** Most recent order date across the PI's records rows (DD-MM-YYYY). */
  latest_order_date: string;
  ordered_qty: number;
  ordered_sqm: number;
  delivered_qty: number;
  delivered_sqm: number;
  balance_qty: number;
  balance_sqm: number;
  /** delivered_qty / ordered_qty, 0–100+. */
  progress_pct: number;
  delivery_status: DeliveryLifecycle;
  /** Number of movements that actually count toward the balance. */
  delivery_count: number;
  /** Including voided and draft movements — shown in the ledger, not in the maths. */
  total_movement_count: number;
  last_delivery_date: string | null;
  /** Days since the last counted movement, relative to the server's clock. */
  days_since_last_delivery: number | null;
  /** True when the PI is only on the tracker because it already has movements. */
  tracked_without_confirmed_status: boolean;
  /** Number of `public.records` rows summed into this obligation. */
  record_row_count: number;
  record_row_ids: (string | number)[];
  source_file: string | null;
  movements: DeliveryMovement[];
};

export type DeliveryDatePreset =
  | "all"
  | "today"
  | "yesterday"
  | "this_week"
  | "this_month"
  | "previous_month"
  | "custom";

export type DeliveryFilters = {
  search: string;
  /** Applied to the PI's order date — the date the commitment was made. */
  datePreset: DeliveryDatePreset;
  dateFrom: string;
  dateTo: string;
  company: string;
  salesman: string;
  preparedBy: string;
  typeOfWork: string;
  piStatus: string;
  /** Derived lifecycle filter: awaiting_dispatch | partial | complete | over_delivered. */
  deliveryStatus: "" | DeliveryLifecycle;
  /** When true, only PIs with a non-zero balance are listed. */
  balanceOnly: boolean;
  sourceFile?: string;
  /** Deep link from the Reports dashboard: /deliveries?pi=PI-2026-1004 */
  pi?: string;
};

export type DeliverySortField =
  | "pi_number"
  | "company"
  | "order_date"
  | "ordered_qty"
  | "ordered_sqm"
  | "delivered_qty"
  | "delivered_sqm"
  | "balance_qty"
  | "balance_sqm"
  | "progress"
  | "delivery_count"
  | "last_delivery_date"
  | "delivery_status";

export type SortDirection = "asc" | "desc";

export type DeliverySort = {
  field: DeliverySortField;
  direction: SortDirection;
};

export type DeliverySummary = {
  /** Confirmed PIs tracked (includes movements-only carry-overs). */
  totalPis: number;
  totalMovements: number;
  orderedQty: number;
  orderedSqm: number;
  deliveredQty: number;
  deliveredSqm: number;
  balanceQty: number;
  balanceSqm: number;
  /** delivered_sqm / ordered_sqm as a percentage, clamped to 0–100 for display math. */
  progressPct: number;
  fullyDeliveredPIs: number;
  partiallyDeliveredPIs: number;
  awaitingDispatchPIs: number;
  overDeliveredPIs: number;
  /** PIs awaiting or part-delivered with no movement in 14 days. */
  stalePIs: number;
  averageMovementsPerPi: number;
  largestBalanceSqm: number;
  /** Balance held by the top 5 companies by outstanding area. */
  concentration: { name: string; balanceSqm: number; share: number }[];
};

export type DeliveryBreakdownItem = {
  name: string;
  count: number;
  orderedQty: number;
  orderedSqm: number;
  deliveredQty: number;
  deliveredSqm: number;
  balanceQty: number;
  balanceSqm: number;
  percentage: number;
};

export type DeliveryBreakdowns = {
  company: DeliveryBreakdownItem[];
  salesman: DeliveryBreakdownItem[];
  lifecycle: DeliveryBreakdownItem[];
  typeOfWork: DeliveryBreakdownItem[];
};

export type DeliveryFilterOptions = {
  companies: string[];
  salesmen: string[];
  preparedBy: string[];
  typesOfWork: string[];
  piStatuses: string[];
  lifecycles: DeliveryLifecycle[];
  recordStatuses: string[];
  sourceFiles: string[];
  currentSourceFile: string | null;
};

export type DeliveryPagination = {
  page: number;
  pageSize: number;
  totalEntries: number;
  totalPages: number;
};

export type DeliveriesApiData = {
  rows: DeliveryRow[];
  summary: DeliverySummary;
  breakdowns: DeliveryBreakdowns;
  filterOptions: DeliveryFilterOptions;
  pagination: DeliveryPagination;
  serverTime: string;
  source: "supabase" | "mock_preview";
  executionMs: number;
  currentSourceFile?: string | null;
  sourceFiles?: string[];
  isCurrentSourceScoped?: boolean;
  warning?: string;
  /** False when `public.delivery_entries` has not been installed yet. */
  ledgerInstalled: boolean;
  /** True when the bounded read hit its ceiling — figures are a floor, not a total. */
  truncated: boolean;
  /** The status values the tracker accepts as "confirmed". */
  confirmedStatuses: string[];
  diagnostics?: {
    targetHost: string;
    authRole: string | null;
    hasServerServiceKey: boolean;
    recordsRowsRead: number;
    movementRowsRead: number;
    aggregation: "rpc" | "client";
    rpcStatus: "success" | "error" | "not_installed";
    rpcError: { message: string; code?: string; details?: string; hint?: string } | null;
  };
};

export type DeliveriesApiSuccessResponse = {
  ok: true;
  data: DeliveriesApiData;
};

export type DeliveriesApiErrorResponse = {
  ok: false;
  error: {
    message: string;
    code?: string;
    details?: string;
  };
};

export type DeliveriesApiResponse = DeliveriesApiSuccessResponse | DeliveriesApiErrorResponse;

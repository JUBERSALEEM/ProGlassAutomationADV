/**
 * ProGlass Automation — Deliveries Tracker Data Layer
 *
 * Reads the *same* Excel-synced dataset the Reports dashboard reads
 * (`public.records`), but answers a different question. Reports lists every row
 * of every status. The tracker accepts only a **confirmed** PI — a proforma
 * that has been marked `Confirmed` — and follows it from the moment of
 * confirmation to the last movement against it.
 *
 * ## Why the unit is the PI, not the row
 *
 * A single confirmed PI can be delivered in several movements: four pieces on
 * the first vehicle, the balance next week, a replacement pane after a break.
 * So one PI row here owns a ledger of movements, and the arithmetic that turns
 * "ordered 40" plus "moved 15, moved 20, moved 2 rework" into "balance 5" lives
 * in exactly one place — `lib/delivery-ledger.ts`. This module's job is to get
 * the raw rows, hand them to the ledger, and return a page's worth of rows.
 *
 * ## The two sources
 *
 *   1. `public.records` — the obligation. Summed per PI number, filtered to the
 *      status values the operator calls "confirmed" (`Confirmed` by default).
 *   2. `public.delivery_entries` — the ledger. One row per dispatch. Optional:
 *      a project that has not run `website/supabase-deliveries.sql` yet gets a
 *      tracker that shows every confirmed PI as awaiting dispatch, plus a
 *      warning naming the file to run. It never fabricates movements.
 *
 * ## The union rule
 *
 * A PI that has movements is tracked even after its status moves on. Without
 * this, the workbook's own status field — which progresses to `Completed` —
 * would delete a delivered PI from the ledger, and the balance would be
 * computed from an empty set. Confirmation is the entry condition; a movement
 * is the retention condition.
 *
 * ## Bounds
 *
 * Every read is bounded and paginated in 1000-row batches up to a documented
 * ceiling. When the ceiling is reached the response says so (`truncated: true`)
 * and the UI reports the figures as a floor rather than pretending they are a
 * total. Silent truncation is how a dashboard lies.
 */

import { isSupabaseConfigured, supabaseServer, supabaseServerWithToken } from "@/lib/supabase";
import { buildSourceFileFilter, resolveSourceScope } from "@/lib/source-scope";
import { resolveDateRange, previewRecords } from "@/lib/records-api";
import { formatDateDDMMYYYY } from "@/lib/date-utils";
import {
  buildDeliveryBreakdowns,
  buildDeliveryFilterOptions,
  buildDeliveryRow,
  buildDeliverySummary,
  filterDeliveryRows,
  groupRecordsIntoObligations,
  movementEpoch,
  movementCountsTowardBalance,
  pickField,
  round2,
  sortDeliveryRows,
  type PiObligation,
} from "@/lib/delivery-ledger";
import type {
  DeliveriesApiData,
  DeliveryFilters,
  DeliveryMovement,
  DeliveryRecordRow,
  DeliveryRow,
  DeliverySort,
} from "@/types/deliveries";

/* ── Configuration ───────────────────────────────────────────────────── */

/**
 * The status values the tracker accepts as "confirmed".
 *
 * `Confirmed` is the workbook's own word for a proforma that has been accepted
 * and is therefore a delivery obligation. It is configurable by environment
 * rather than hard-coded because a business that also treats `In Production` or
 * `Ready` as committed work should be able to widen the entry condition without
 * a code change:
 *
 *     PROGLASS_CONFIRMED_STATUSES="Confirmed,In Production,Ready"
 *
 * Widening it never breaks the balance maths — that comes from the ledger, not
 * from the status.
 */
export const DEFAULT_CONFIRMED_STATUSES = ["Confirmed"];

export function confirmedStatuses(): string[] {
  const configured = process.env.PROGLASS_CONFIRMED_STATUSES;
  if (!configured) return DEFAULT_CONFIRMED_STATUSES;
  const values = configured
    .split(",")
    .map((value) => value.trim())
    .filter(Boolean);
  return values.length > 0 ? values : DEFAULT_CONFIRMED_STATUSES;
}

/** Ceilings. Documented, surfaced, never silent. */
const RECORD_BATCH = 1000;
const MAX_RECORD_ROWS = 10_000;
const MOVEMENT_BATCH = 1000;
const MAX_MOVEMENT_ROWS = 10_000;
const MAX_MOVEMENTS_PER_ROW = 500;

/** Candidate ledger tables, in preference order. */
const LEDGER_TABLES = ["delivery_entries", "deliveries", "delivery_notes", "delivery_movements"];

const RECORD_COLUMNS = [
  "id",
  "date",
  "pi_number",
  "company",
  "customer_ref",
  "salesman",
  "prepared_by",
  "type_of_work",
  "pi_status",
  "zoho_status",
  "status",
  "qty",
  "sqm",
  "is_active",
  "source_file",
  "created_at",
  "updated_at",
].join(", ");

/* ── Public entry point ──────────────────────────────────────────────── */

export async function fetchDeliveriesDashboardData({
  filters,
  sort = { field: "order_date", direction: "desc" },
  page = 1,
  pageSize = 50,
  authToken,
}: {
  filters: DeliveryFilters;
  sort?: DeliverySort;
  page?: number;
  pageSize?: number;
  authToken?: string | null;
}): Promise<DeliveriesApiData> {
  const started = Date.now();
  const clampedPageSize = Math.min(Math.max(Number(pageSize) || 50, 10), MAX_RECORD_ROWS);
  const clampedPage = Math.max(Number(page) || 1, 1);
  const statuses = confirmedStatuses();

  const { from: resolvedFrom, to: resolvedTo } = resolveDateRange(
    filters.datePreset,
    filters.dateFrom,
    filters.dateTo,
  );

  const isProduction = process.env.NODE_ENV === "production";

  if (isProduction && !isSupabaseConfigured()) {
    throw new Error(
      "Supabase environment variables (NEXT_PUBLIC_SUPABASE_URL and NEXT_PUBLIC_SUPABASE_ANON_KEY) are not configured in production.",
    );
  }

  if (isSupabaseConfigured()) {
    const client = authToken ? supabaseServerWithToken(authToken) : supabaseServer();
    const scope = await resolveSourceScope(client, filters.sourceFile);

    const {
      rows: recordRows,
      truncated: recordsTruncated,
      degraded: recordsDegraded,
    } = await readConfirmedRecords(
      client,
      scope.effectiveSourceFile,
      statuses,
      resolvedFrom,
      resolvedTo,
    );

    const ledger = await readLedger(client, scope.effectiveSourceFile);

    const obligations = groupRecordsIntoObligations(recordRows, statuses);
    const rows = assembleRows(obligations, ledger.movements, new Date());

    const filtered = filterDeliveryRows(rows, {
      search: filters.search,
      company: filters.company,
      salesman: filters.salesman,
      preparedBy: filters.preparedBy,
      typeOfWork: filters.typeOfWork,
      piStatus: filters.piStatus,
      deliveryStatus: filters.deliveryStatus,
      balanceOnly: filters.balanceOnly,
      pi: filters.pi,
      dateRange: { from: resolvedFrom, to: resolvedTo },
    });

    const sorted = sortDeliveryRows(filtered, sort);
    const totalEntries = sorted.length;
    const offset = (clampedPage - 1) * clampedPageSize;
    const pagedRows = sorted.slice(offset, offset + clampedPageSize);

    const summary = buildDeliverySummary(filtered);
    const breakdowns = buildDeliveryBreakdowns(filtered);
    const filterOptions = buildDeliveryFilterOptions(filtered, {
      sourceFiles: scope.availableSourceFiles,
      currentSourceFile: scope.currentSourceFile,
    });

    const warning = buildWarning({
      ledgerInstalled: ledger.installed,
      ledgerWarning: ledger.warning,
      truncated: recordsTruncated || ledger.truncated,
      degraded: recordsDegraded,
      detectedLatestHasNoSourceFile: scope.detectedLatestHasNoSourceFile,
      totalEntries,
    });

    return {
      rows: pagedRows,
      summary,
      breakdowns,
      filterOptions,
      pagination: {
        page: clampedPage,
        pageSize: clampedPageSize,
        totalEntries,
        totalPages: Math.max(1, Math.ceil(totalEntries / clampedPageSize)),
      },
      serverTime: new Date().toISOString(),
      source: "supabase",
      executionMs: Date.now() - started,
      currentSourceFile: scope.currentSourceFile,
      sourceFiles: scope.availableSourceFiles,
      isCurrentSourceScoped: Boolean(scope.effectiveSourceFile),
      warning,
      ledgerInstalled: ledger.installed,
      truncated: recordsTruncated || ledger.truncated,
      confirmedStatuses: statuses,
      diagnostics: {
        targetHost: supabaseHost(),
        authRole: anonRole(),
        hasServerServiceKey: Boolean(
          process.env.SUPABASE_SERVICE_ROLE_KEY || process.env.SUPABASE_SERVICE_KEY,
        ),
        recordsRowsRead: recordRows.length,
        movementRowsRead: ledger.movements.length,
        aggregation: "client",
        rpcStatus: "not_installed",
        rpcError: null,
      },
    };
  }

  if (isProduction) {
    throw new Error(
      "Supabase connection is not available in production. Refusing to serve preview data in production.",
    );
  }

  return executePreviewQuery({
    filters,
    sort,
    page: clampedPage,
    pageSize: clampedPageSize,
    resolvedFrom,
    resolvedTo,
    statuses,
    started,
  });
}

/* ── Supabase reads ──────────────────────────────────────────────────── */

type LedgerReadResult = {
  movements: DeliveryMovement[];
  installed: boolean;
  warning?: string;
  truncated: boolean;
  table: string | null;
};

/**
 * Read confirmed PI rows in bounded batches.
 *
 * PostgREST caps a response at the project's `max-rows` setting (1000 by
 * default), so a single `.limit(10000)` would quietly return 1000. Batching
 * with `range()` is the only way to read the full set, and stopping at a
 * ceiling we then report is the only honest way to bound it.
 */
async function readConfirmedRecords(
  client: ReturnType<typeof supabaseServer>,
  effectiveSourceFile: string | null,
  statuses: string[],
  resolvedFrom: string,
  resolvedTo: string,
) : Promise<{ rows: DeliveryRecordRow[]; truncated: boolean; degraded: boolean }> {
  const rows: DeliveryRecordRow[] = [];
  const cleanedStatuses = statuses.map((status) => status.replace(/[,()]/g, "").trim()).filter(Boolean);
  let truncated = false;
  let useOptionalColumns = true;
  let useActiveFlag = true;
  let degraded = false;

  for (let offset = 0; offset < MAX_RECORD_ROWS; offset += RECORD_BATCH) {
    const columns = useOptionalColumns
      ? RECORD_COLUMNS
      : "id, date, pi_number, company, customer_ref, salesman, prepared_by, type_of_work, qty, sqm, status";

    let query = client
      .from("records")
      .select(columns)
      .order("pi_number", { ascending: true })
      .order("date", { ascending: true })
      .range(offset, offset + RECORD_BATCH - 1);

    if (useActiveFlag) query = query.neq("is_active", false);
    if (effectiveSourceFile && useOptionalColumns) {
      query = query.or(buildSourceFileFilter(effectiveSourceFile));
    }
    if (cleanedStatuses.length === 1) {
      query = query.ilike("status", cleanedStatuses[0] as string);
    } else if (cleanedStatuses.length > 1) {
      query = query.or(cleanedStatuses.map((status) => `status.ilike.${status}`).join(","));
    }
    if (resolvedFrom) query = query.gte("date", resolvedFrom);
    if (resolvedTo) query = query.lte("date", resolvedTo);

    const { data, error } = await query;

    if (error) {
      // Schema tolerance, same policy as the records dashboard: when a column
      // the tracker would like does not exist yet, drop to the core set and
      // retry rather than failing the whole page. The status filter and the
      // date window always survive — they are what makes this the deliveries
      // tracker rather than a full table scan.
      if (error.code === "42703") {
        const message = error.message?.toLowerCase() ?? "";
        if (message.includes("is_active")) useActiveFlag = false;
        if (useOptionalColumns) {
          useOptionalColumns = false;
          degraded = true;
          offset -= RECORD_BATCH;
          continue;
        }
      }
      throw new Error(`Supabase records query failed [${error.code || "unknown"}]: ${error.message}`);
    }

    const batch = (data ?? []) as unknown as DeliveryRecordRow[];
    rows.push(...batch);
    if (batch.length < RECORD_BATCH) break;
    if (offset + RECORD_BATCH >= MAX_RECORD_ROWS) truncated = true;
  }

  return { rows, truncated, degraded };
}

/** True when a table actually carries a column — used to skip filters that would fail. */
async function hasColumn(
  client: ReturnType<typeof supabaseServer>,
  table: string,
  column: string,
): Promise<boolean> {
  try {
    const { error } = await client.from(table).select(column).limit(1);
    return !error;
  } catch {
    return false;
  }
}

/**
 * Read the delivery ledger, if one exists.
 *
 * The table is optional by design: a project that has not installed
 * `website/supabase-deliveries.sql` still gets a tracker — with zero movement
 * rows and a warning that says exactly which file to run. The candidate list
 * also lets the page read a table an existing integration already maintains,
 * because the alternative (demanding our schema) would mean re-entering data
 * that already exists.
 */
async function readLedger(
  client: ReturnType<typeof supabaseServer>,
  effectiveSourceFile: string | null,
): Promise<LedgerReadResult> {
  let lastError: string | undefined;

  for (const table of LEDGER_TABLES) {
    const probe = await client.from(table).select("id").limit(1);
    if (probe.error) {
      lastError = probe.error.message;
      continue;
    }

    const hasSourceFile = effectiveSourceFile ? await hasColumn(client, table, "source_file") : false;
    const orderColumn = (await hasColumn(client, table, "created_at")) ? "created_at" : "id";

    const movements: DeliveryMovement[] = [];
    let truncated = false;
    let failed = false;

    for (let offset = 0; offset < MAX_MOVEMENT_ROWS; offset += MOVEMENT_BATCH) {
      let query = client
        .from(table)
        .select("*")
        .order(orderColumn, { ascending: true })
        .range(offset, offset + MOVEMENT_BATCH - 1);

      if (effectiveSourceFile && hasSourceFile) {
        query = query.or(buildSourceFileFilter(effectiveSourceFile));
      }

      const { data, error } = await query;
      if (error) {
        lastError = error.message;
        failed = movements.length === 0;
        break;
      }

      const batch = data ?? [];
      for (const raw of batch) movements.push(mapMovementRow(raw as Record<string, unknown>));
      if (batch.length < MOVEMENT_BATCH) break;
      if (offset + MOVEMENT_BATCH >= MAX_MOVEMENT_ROWS) truncated = true;
    }

    if (failed) continue;
    return { movements, installed: true, truncated, table };
  }

  return {
    movements: [],
    installed: false,
    warning:
      "The delivery ledger is not installed in this Supabase project, so every confirmed PI is shown as awaiting dispatch. Run website/supabase-deliveries.sql in the SQL editor to create public.delivery_entries; the tracker picks it up automatically." +
      (lastError ? ` (Last probe error: ${lastError})` : ""),
    truncated: false,
    table: null,
  };
}

const VOID_FLAG_FIELDS = ["is_void", "void", "voided", "is_cancelled", "is_canceled", "iscancelled"];

/**
 * Map a ledger row onto the tracker's movement shape.
 *
 * Column names are matched against an alias list per field rather than read
 * positionally, so the page works against the canonical table this repository
 * ships *and* against a table an existing integration already writes, without
 * either side having to rename anything.
 */
export function mapMovementRow(raw: Record<string, unknown>): DeliveryMovement {
  const voidValue = pickField(raw, VOID_FLAG_FIELDS);
  const isVoid =
    voidValue === true ||
    (typeof voidValue === "string" &&
      ["true", "1", "yes", "y", "void", "voided"].includes(voidValue.trim().toLowerCase()));

  const sequenceRaw = pickField(raw, [
    "sequence",
    "sequence_number",
    "sequenceNumber",
    "seq",
    "delivery_sequence",
  ]);
  const sequenceNumber = Number(sequenceRaw);
  const status = String(pickField(raw, ["status", "delivery_status", "dn_status"]) ?? "Dispatched").trim();

  const movement: DeliveryMovement = {
    id:
      (pickField(raw, ["id", "entry_id", "delivery_entry_id"]) as string | number | undefined) ??
      `movement-${String(pickField(raw, ["delivery_note_id", "dn_number", "pi_number"]) ?? "unknown")}`,
    pi_number: String(
      pickField(raw, [
        "pi_number",
        "piNumber",
        "pi_no",
        "pi",
        "proforma_id",
        "proforma_number",
        "proforma_no",
      ]) ?? "",
    ).trim(),
    delivery_note_ref: textOrNull(
      pickField(raw, [
        "delivery_note_ref",
        "delivery_note_id",
        "deliveryNoteId",
        "delivery_note_number",
        "dn_number",
        "dn_id",
        "dn_ref",
        "note_ref",
      ]),
    ),
    sequence: Number.isFinite(sequenceNumber) && sequenceRaw !== undefined ? sequenceNumber : null,
    // Normalized here, at the boundary: everything downstream (sorting,
    // "days since", the ledger table) may then assume DD-MM-YYYY.
    delivery_date: formatDateDDMMYYYY(
      pickField(raw, [
        "delivery_date",
        "deliveryDate",
        "date",
        "dispatched_at",
        "dispatched_on",
        "delivered_at",
        "created_at",
      ]) ?? "",
    ),
    delivery_time: textOrNull(pickField(raw, ["delivery_time", "deliveryTime", "time"])),
    delivered_qty: toNumber(
      pickField(raw, [
        "delivered_qty",
        "deliveredQty",
        "delivered_quantity",
        "qty",
        "quantity",
        "total_delivered_qty",
        "totalDeliveredQty",
      ]),
    ),
    delivered_sqm: toNumber(
      pickField(raw, [
        "delivered_sqm",
        "deliveredSqm",
        "delivered_area",
        "sqm",
        "area",
        "total_delivered_sqm",
      ]),
    ),
    delivery_type: String(
      pickField(raw, ["delivery_type", "deliveryType", "dn_type", "type"]) ?? "Original",
    ).trim(),
    status,
    driver_name: textOrNull(pickField(raw, ["driver_name", "driverName", "driver"])),
    vehicle_number: textOrNull(pickField(raw, ["vehicle_number", "vehicleNumber", "vehicle", "vehicle_no"])),
    receiver_name: textOrNull(pickField(raw, ["receiver_name", "receiverName", "receiver", "signed_by"])),
    notes: textOrNull(pickField(raw, ["notes", "note", "remarks", "remarks_text"])),
    is_void: isVoid,
    counts_toward_balance: movementCountsTowardBalance({ is_void: isVoid, status }),
    signed_off: ["signed", "received"].includes(status.toLowerCase()),
    source_file: textOrNull(pickField(raw, ["source_file", "sourceFile", "file_name", "filename"])),
  };

  return movement;
}

function textOrNull(value: unknown): string | null {
  if (value === null || value === undefined) return null;
  const text = String(value).trim();
  return text ? text : null;
}

function toNumber(value: unknown): number {
  if (typeof value === "number") return Number.isFinite(value) ? value : 0;
  if (typeof value === "string") {
    const parsed = Number(value.replace(/,/g, "").trim());
    return Number.isFinite(parsed) ? parsed : 0;
  }
  return 0;
}

/* ── Assembly ────────────────────────────────────────────────────────── */

/**
 * Combine obligations with their ledger.
 *
 * Movements whose PI has no confirmed row are *still tracked* — goods left the
 * building, so the PI is a delivery whether or not the workbook still says
 * `Confirmed`. Those rows carry `tracked_without_confirmed_status` so the UI can
 * say why they are on the page instead of hiding the fact.
 */
export function assembleRows(
  obligations: Map<string, PiObligation>,
  movements: DeliveryMovement[],
  now: Date,
): DeliveryRow[] {
  const byPi = new Map<string, DeliveryMovement[]>();
  for (const movement of movements) {
    if (!movement.pi_number) continue;
    const list = byPi.get(movement.pi_number) ?? [];
    if (list.length < MAX_MOVEMENTS_PER_ROW) list.push(movement);
    byPi.set(movement.pi_number, list);
  }

  const rows: DeliveryRow[] = [];

  for (const [pi, obligation] of obligations) {
    const list = byPi.get(pi) ?? [];

    // The eligibility rule, stated once for the whole tracker: a PI is tracked
    // when it was confirmed *or* when something has already moved against it.
    // A draft proforma with no movements is Reports' business, not this page's.
    if (!obligation.has_confirmed_row && list.length === 0) continue;

    rows.push(buildDeliveryRow(obligation, list, now));
    byPi.delete(pi);
  }

  for (const [pi, list] of byPi) {
    rows.push(
      buildDeliveryRow(
        {
          pi_number: pi,
          company: "",
          customer_ref: "",
          salesman: "",
          prepared_by: "",
          type_of_work: "",
          pi_status: "",
          zoho_status: "",
          record_status: "",
          order_date: list[0]?.delivery_date ?? "",
          latest_order_date: list[list.length - 1]?.delivery_date ?? "",
          ordered_qty: 0,
          ordered_sqm: 0,
          record_row_count: 0,
          record_row_ids: [],
          source_file: list[0]?.source_file ?? null,
          has_confirmed_row: false,
        },
        list,
        now,
      ),
    );
  }

  return rows;
}

function buildWarning(args: {
  ledgerInstalled: boolean;
  ledgerWarning?: string;
  truncated: boolean;
  degraded: boolean;
  detectedLatestHasNoSourceFile: boolean;
  totalEntries: number;
}): string | undefined {
  if (!args.ledgerInstalled && args.ledgerWarning) return args.ledgerWarning;
  if (args.degraded) {
    return "Notice: public.records is missing some columns the tracker reads (workbook scoping or soft-delete flags). Running website/supabase-dashboard.sql restores them; until then the tracker reads the core columns only.";
  }
  if (args.truncated) {
    return "This dataset is larger than the tracker's bounded read (10,000 rows). Figures below are a floor, not a total — narrow the filters to see a complete picture.";
  }
  if (args.totalEntries === 0) {
    return "No confirmed PIs matched. The tracker only reads proformas whose status is Confirmed (see PROGLASS_CONFIRMED_STATUSES) plus any PI that already has delivery movements.";
  }
  if (args.detectedLatestHasNoSourceFile) {
    return "Notice: the most recently synced rows in public.records do not carry 'source_file'. Showing all recent entries rather than scoping to one workbook.";
  }
  return undefined;
}

function supabaseHost(): string {
  try {
    return new URL(process.env.NEXT_PUBLIC_SUPABASE_URL || "").host;
  } catch {
    return "unknown";
  }
}

function anonRole(): string | null {
  const key = process.env.NEXT_PUBLIC_SUPABASE_ANON_KEY;
  if (!key) return null;
  const parts = key.split(".");
  if (parts.length !== 3 || !parts[1]) return null;
  try {
    const base64 = parts[1].replace(/-/g, "+").replace(/_/g, "/");
    const padded = base64 + "=".repeat((4 - (base64.length % 4)) % 4);
    const claims = JSON.parse(Buffer.from(padded, "base64").toString("utf-8")) as { role?: unknown };
    return typeof claims.role === "string" ? claims.role : null;
  } catch {
    return null;
  }
}

/* ── Local preview (no Supabase configured) ──────────────────────────── */

/**
 * A deterministic preview: the same order book the Reports dashboard shows,
 * with movements that demonstrate every state the tracker has to render —
 * awaiting dispatch, part-delivered, delivered, over-delivered by rework, a
 * voided note and a return.
 */
function executePreviewQuery({
  filters,
  sort,
  page,
  pageSize,
  resolvedFrom,
  resolvedTo,
  statuses,
  started,
}: {
  filters: DeliveryFilters;
  sort: DeliverySort;
  page: number;
  pageSize: number;
  resolvedFrom: string;
  resolvedTo: string;
  statuses: string[];
  started: number;
}): DeliveriesApiData {
  const records = previewRecords().filter((record) => record.is_active !== false);
  const obligations = groupRecordsIntoObligations(records, statuses);
  const mockMovements = generatePreviewMovements(obligations);

  const rows = assembleRows(obligations, mockMovements, new Date());

  const filtered = filterDeliveryRows(rows, {
    search: filters.search,
    company: filters.company,
    salesman: filters.salesman,
    preparedBy: filters.preparedBy,
    typeOfWork: filters.typeOfWork,
    piStatus: filters.piStatus,
    deliveryStatus: filters.deliveryStatus,
    balanceOnly: filters.balanceOnly,
    pi: filters.pi,
    dateRange: { from: resolvedFrom, to: resolvedTo },
  });

  const sorted = sortDeliveryRows(filtered, sort);
  const totalEntries = sorted.length;
  const offset = (page - 1) * pageSize;
  const pagedRows = sorted.slice(offset, offset + pageSize);

  return {
    rows: pagedRows,
    summary: buildDeliverySummary(filtered),
    breakdowns: buildDeliveryBreakdowns(filtered),
    filterOptions: buildDeliveryFilterOptions(filtered, {
      sourceFiles: ["Daily_Orders_Master_2026.xlsx"],
      currentSourceFile: "Daily_Orders_Master_2026.xlsx",
    }),
    pagination: {
      page,
      pageSize,
      totalEntries,
      totalPages: Math.max(1, Math.ceil(totalEntries / pageSize)),
    },
    serverTime: new Date().toISOString(),
    source: "mock_preview",
    executionMs: Date.now() - started,
    currentSourceFile: "Daily_Orders_Master_2026.xlsx",
    sourceFiles: ["Daily_Orders_Master_2026.xlsx"],
    isCurrentSourceScoped: true,
    warning:
      "Local development preview (Supabase environment variables not set). Movements are generated to demonstrate part-delivery arithmetic.",
    ledgerInstalled: true,
    truncated: false,
    confirmedStatuses: statuses,
    diagnostics: {
      targetHost: supabaseHost(),
      authRole: anonRole(),
      hasServerServiceKey: false,
      recordsRowsRead: records.length,
      movementRowsRead: mockMovements.length,
      aggregation: "client",
      rpcStatus: "not_installed",
      rpcError: null,
    },
  };
}

/**
 * Deterministic preview movements.
 *
 * The pattern is chosen so a reader opening the preview sees, in one screen:
 * a PI nobody has touched, one sent in two loads, one completed with a signed
 * note, one over its ordered quantity because a pane was re-cut, and a voided
 * note that must not move any of the numbers.
 */
export function generatePreviewMovements(
  obligations: Map<string, PiObligation>,
): DeliveryMovement[] {
  const movements: DeliveryMovement[] = [];
  let cursor = 0;

  for (const [pi, obligation] of obligations) {
    if (!obligation.has_confirmed_row) continue;
    cursor += 1;
    const variant = cursor % 6;
    const orderedQty = Math.max(1, Math.round(obligation.ordered_qty));
    const orderedSqm = obligation.ordered_sqm;
    const orderEpoch = movementEpoch(obligation.order_date) || Date.UTC(2026, 9, 1);

    const movement = (
      index: number,
      qty: number,
      type: string,
      status: string,
      offsetDays: number,
      extra: Partial<DeliveryMovement> = {},
    ): DeliveryMovement => {
      const sqm = orderedQty > 0 ? round2((orderedSqm * (qty / orderedQty)) ) : 0;
      const date = new Date(orderEpoch + (offsetDays + index) * 86_400_000);
      const iso = formatDateDDMMYYYY(date);
      const movementBase: DeliveryMovement = {
        id: `mock-${pi}-${index}`,
        pi_number: pi,
        delivery_note_ref: `DN-2026-${String(100 + cursor).padStart(3, "0")}-${String(index).padStart(2, "0")}`,
        sequence: index,
        delivery_date: iso,
        delivery_time: "09:30",
        delivered_qty: qty,
        delivered_sqm: sqm,
        delivery_type: type,
        status,
        driver_name: "Rashid Ali",
        vehicle_number: "DXB-C-4471",
        receiver_name: status === "Signed" || status === "Received" ? "Site engineer" : null,
        notes: null,
        is_void: false,
        counts_toward_balance: true,
        signed_off: status === "Signed" || status === "Received",
        source_file: "Daily_Orders_Master_2026.xlsx",
      };
      // The flags are derived here, not typed in: a preview that disagreed
      // with the ledger's own rules would teach the wrong thing.
      const merged = { ...movementBase, ...extra } as DeliveryMovement;
      merged.counts_toward_balance = movementCountsTowardBalance(merged);
      merged.signed_off = ["signed", "received"].includes(merged.status.trim().toLowerCase());
      return merged;
    };

    switch (variant) {
      case 0:
        // Awaiting dispatch — a voided note on the ledger must not change that.
        movements.push(
          movement(1, 0, "Original", "Draft", 1, {
            is_void: true,
            delivery_note_ref: `DN-2026-${String(100 + cursor).padStart(3, "0")}-01 (voided)`,
            notes: "Cancelled before loading — vehicle allocation changed.",
          }),
        );
        break;
      case 1:
        // A part load followed by a smaller one: 75 % out, balance owed.
        movements.push(movement(1, Math.floor(orderedQty * 0.4), "Original", "Signed", 2));
        movements.push(movement(2, Math.floor(orderedQty * 0.35), "Original", "Dispatched", 6));
        break;
      case 2:
        // Two loads, the second signed for on site: fully delivered.
        movements.push(movement(1, Math.floor(orderedQty * 0.5), "Original", "Signed", 3));
        movements.push(
          movement(2, orderedQty - Math.floor(orderedQty * 0.5), "Original", "Received", 9),
        );
        break;
      case 3:
        // Over-delivered: the full order, two re-cut panes, then three more
        // sent in error. The excess beyond what rework explains is the signal.
        movements.push(movement(1, orderedQty, "Original", "Signed", 4));
        movements.push(
          movement(2, 2, "Rework", "Dispatched", 12, { notes: "Two panes re-cut after breakage." }),
        );
        movements.push(
          movement(3, 3, "Original", "Dispatched", 16, {
            notes: "Loaded against the wrong line — validation should have refused this.",
          }),
        );
        break;
      case 4:
        // A live note that has not left the building is excluded from the
        // balance but stays visible on the ledger.
        movements.push(movement(1, Math.floor(orderedQty * 0.3), "Original", "Dispatched", 5));
        movements.push(movement(2, Math.floor(orderedQty * 0.2), "Original", "Dispatching", 6, {
          notes: "Being loaded — not yet dispatched.",
        }));
        break;
      default:
        // A rejected pane comes back: delivered is the order minus what returned.
        movements.push(movement(1, Math.floor(orderedQty * 0.6), "Original", "Signed", 3));
        movements.push(
          movement(2, orderedQty - Math.floor(orderedQty * 0.6), "Original", "Received", 8),
        );
        movements.push(
          movement(3, 1, "Return", "Received", 11, {
            notes: "One pane rejected on site — chipped corner.",
          }),
        );
        break;
    }
  }

  return movements;
}

/** Exposed for the API route's sort whitelist. */
export const DELIVERY_SORT_FIELDS: DeliverySort["field"][] = [
  "pi_number",
  "company",
  "order_date",
  "ordered_qty",
  "ordered_sqm",
  "delivered_qty",
  "delivered_sqm",
  "balance_qty",
  "balance_sqm",
  "progress",
  "delivery_count",
  "last_delivery_date",
  "delivery_status",
];


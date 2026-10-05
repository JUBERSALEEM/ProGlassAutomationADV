import { fetchDeliveriesDashboardData } from "@/lib/deliveries-api";
import { fetchJobOrdersDashboardData } from "@/lib/job-orders-api";
import { fetchRecordsDashboardData } from "@/lib/records-api";
import type {
  DeliveryDatePreset,
  DeliveryFilters,
  DeliveryLifecycle,
  DeliveryRow,
  DeliverySort,
  DeliverySortField,
} from "@/types/deliveries";
import type { JobOrderFilters, JobOrderRow, JobOrderSort, JobOrderSortField } from "@/types/job-orders";
import type {
  DatePreset,
  RecordFilters,
  RecordRow,
  RecordSort,
  RecordSortField,
  SortDirection,
} from "@/types/records";

export const OPERATIONS_EXPORT_ROW_LIMIT = 10_000;
const RECORD_EXPORT_PAGE_SIZE = 1_000;

export type OperationsExportReport = "proformas" | "job-orders" | "deliveries";
export type OperationsExportFormat = "csv" | "xlsx" | "pdf";

export type ExportColumn = {
  key: string;
  header: string;
};

export type ExportSheet = {
  name: string;
  columns: ExportColumn[];
  rows: Record<string, unknown>[];
};

export type OperationsExportBundle = {
  schemaVersion: 1;
  report: OperationsExportReport;
  title: string;
  generatedAt: string;
  rowCount: number;
  filters: Record<string, string>;
  summary: Record<string, number | string>;
  sheets: ExportSheet[];
  /** CSV is a single flat table; deliveries use one row per movement. */
  csvSheet: ExportSheet;
};

export class ExportLimitError extends Error {
  readonly status = 413;

  constructor(message: string) {
    super(message);
    this.name = "ExportLimitError";
  }
}

export class ExportConflictError extends Error {
  readonly status = 409;

  constructor(message: string) {
    super(message);
    this.name = "ExportConflictError";
  }
}

const clean = (value: string | null, maxLength = 160) => (value || "").trim().slice(0, maxLength);
const PRESETS: DatePreset[] = ["all", "today", "yesterday", "this_week", "this_month", "previous_month", "custom"];
const DELIVERY_PRESETS: DeliveryDatePreset[] = PRESETS;
const DELIVERY_STAGES: DeliveryLifecycle[] = ["awaiting_dispatch", "partial", "complete", "over_delivered"];
const DIRECTIONS: SortDirection[] = ["asc", "desc"];

function parsePreset(value: string | null): DatePreset {
  return PRESETS.includes(value as DatePreset) ? (value as DatePreset) : "all";
}

function parseDeliveryPreset(value: string | null): DeliveryDatePreset {
  return DELIVERY_PRESETS.includes(value as DeliveryDatePreset) ? (value as DeliveryDatePreset) : "all";
}

function parseDirection(value: string | null, fallback: SortDirection): SortDirection {
  return DIRECTIONS.includes(value as SortDirection) ? (value as SortDirection) : fallback;
}

function filterSummary(filters: Record<string, string | boolean | undefined>): Record<string, string> {
  return Object.fromEntries(
    Object.entries(filters)
      .filter(([, value]) => value !== undefined && value !== "" && value !== false)
      .map(([key, value]) => [key, value === true ? "Yes" : String(value)]),
  );
}

function uniqueCount(values: string[]): number {
  return new Set(values.map((value) => value.trim()).filter(Boolean)).size;
}

function proformaColumns(): ExportColumn[] {
  return [
    { key: "date", header: "Order date" },
    { key: "pi_number", header: "Proforma / PI number" },
    { key: "customer_ref", header: "Customer reference" },
    { key: "company", header: "Company" },
    { key: "salesman", header: "Salesman" },
    { key: "prepared_by", header: "Prepared by" },
    { key: "type_of_work", header: "Type of work" },
    { key: "qty", header: "Quantity (pcs)" },
    { key: "sqm", header: "Area (m²)" },
    { key: "status", header: "Operational status" },
    { key: "pi_status", header: "PI status" },
    { key: "zoho_status", header: "Zoho status" },
    { key: "source_file", header: "Source workbook" },
    { key: "id", header: "Source record ID" },
  ];
}

function jobOrderColumns(): ExportColumn[] {
  return [
    { key: "order_date", header: "Order date" },
    { key: "pi_number", header: "Proforma / PI number" },
    { key: "customer_ref", header: "Customer reference" },
    { key: "company", header: "Company" },
    { key: "salesman", header: "Salesman" },
    { key: "prepared_by", header: "Prepared by" },
    { key: "type_of_work", header: "Type of work" },
    { key: "ordered_qty", header: "Confirmed quantity (pcs)" },
    { key: "ordered_sqm", header: "Confirmed area (m²)" },
    { key: "record_count", header: "Source lines" },
    { key: "confirmation_status", header: "Confirmation gate" },
    { key: "pi_status", header: "PI status" },
    { key: "zoho_status", header: "Zoho status" },
    { key: "source_file", header: "Source workbook" },
    { key: "record_ids", header: "Source record IDs" },
  ];
}

function deliverySummaryColumns(): ExportColumn[] {
  return [
    { key: "pi_number", header: "Proforma / PI number" },
    { key: "company", header: "Company" },
    { key: "customer_ref", header: "Customer reference" },
    { key: "salesman", header: "Salesman" },
    { key: "prepared_by", header: "Prepared by" },
    { key: "type_of_work", header: "Type of work" },
    { key: "pi_status", header: "PI status" },
    { key: "record_status", header: "Operational status" },
    { key: "order_date", header: "Order date" },
    { key: "latest_order_date", header: "Latest source date" },
    { key: "ordered_qty", header: "Ordered quantity (pcs)" },
    { key: "ordered_sqm", header: "Ordered area (m²)" },
    { key: "delivered_qty", header: "Delivered quantity (pcs)" },
    { key: "delivered_sqm", header: "Delivered area (m²)" },
    { key: "balance_qty", header: "Balance quantity (pcs)" },
    { key: "balance_sqm", header: "Balance area (m²)" },
    { key: "progress_pct", header: "Progress (%)" },
    { key: "delivery_status", header: "Delivery stage" },
    { key: "delivery_count", header: "Counted deliveries" },
    { key: "total_movement_count", header: "All movements" },
    { key: "source_file", header: "Source workbook" },
  ];
}

function deliveryMovementColumns(): ExportColumn[] {
  return [
    { key: "pi_number", header: "Proforma / PI number" },
    { key: "company", header: "Company" },
    { key: "customer_ref", header: "Customer reference" },
    { key: "order_date", header: "Order date" },
    { key: "ordered_qty", header: "PI ordered quantity (pcs)" },
    { key: "ordered_sqm", header: "PI ordered area (m²)" },
    { key: "balance_qty", header: "Current PI balance (pcs)" },
    { key: "balance_sqm", header: "Current PI balance (m²)" },
    { key: "delivery_note_ref", header: "Delivery Note reference" },
    { key: "sequence", header: "Sequence" },
    { key: "delivery_date", header: "Delivery date" },
    { key: "delivery_time", header: "Delivery time" },
    { key: "delivered_qty", header: "Movement quantity (pcs)" },
    { key: "delivered_sqm", header: "Movement area (m²)" },
    { key: "delivery_type", header: "Movement type" },
    { key: "status", header: "Movement status" },
    { key: "driver_name", header: "Driver" },
    { key: "vehicle_number", header: "Vehicle" },
    { key: "receiver_name", header: "Receiver" },
    { key: "notes", header: "Notes" },
    { key: "is_void", header: "Voided" },
    { key: "counts_toward_balance", header: "Counts toward balance" },
    { key: "signed_off", header: "Signed off" },
    { key: "source_file", header: "Source workbook" },
  ];
}

function flattenDeliveryRows(rows: DeliveryRow[]): Record<string, unknown>[] {
  return rows.flatMap((row) => {
    const movementRows = row.movements.length > 0 ? row.movements : [null];
    return movementRows.map((movement) => ({
      pi_number: row.pi_number,
      company: row.company,
      customer_ref: row.customer_ref,
      order_date: row.order_date,
      ordered_qty: row.ordered_qty,
      ordered_sqm: row.ordered_sqm,
      balance_qty: row.balance_qty,
      balance_sqm: row.balance_sqm,
      delivery_note_ref: movement?.delivery_note_ref ?? "",
      sequence: movement?.sequence ?? "",
      delivery_date: movement?.delivery_date ?? "",
      delivery_time: movement?.delivery_time ?? "",
      delivered_qty: movement?.delivered_qty ?? "",
      delivered_sqm: movement?.delivered_sqm ?? "",
      delivery_type: movement?.delivery_type ?? "",
      status: movement?.status ?? "",
      driver_name: movement?.driver_name ?? "",
      vehicle_number: movement?.vehicle_number ?? "",
      receiver_name: movement?.receiver_name ?? "",
      notes: movement?.notes ?? "",
      is_void: movement?.is_void ?? "",
      counts_toward_balance: movement?.counts_toward_balance ?? "",
      signed_off: movement?.signed_off ?? "",
      source_file: movement?.source_file ?? row.source_file ?? "",
    }));
  });
}

function asRecordRows(rows: RecordRow[]): Record<string, unknown>[] {
  return rows.map((row) => ({
    date: row.date,
    pi_number: row.pi_number,
    customer_ref: row.customer_ref,
    company: row.company,
    salesman: row.salesman,
    prepared_by: row.prepared_by,
    type_of_work: row.type_of_work,
    qty: typeof row.qty === "string" ? Number(row.qty.replace(/,/g, "")) : row.qty,
    sqm: typeof row.sqm === "string" ? Number(row.sqm.replace(/,/g, "")) : row.sqm,
    status: row.status,
    pi_status: row.pi_status,
    zoho_status: row.zoho_status,
    source_file: row.source_file ?? "",
    id: row.id,
  }));
}

async function loadAllProformas(
  params: URLSearchParams,
  authToken: string | null,
): Promise<{ rows: RecordRow[]; filters: RecordFilters }> {
  const filters: RecordFilters = {
    search: clean(params.get("search")),
    datePreset: parsePreset(params.get("datePreset")),
    dateFrom: clean(params.get("dateFrom"), 20),
    dateTo: clean(params.get("dateTo"), 20),
    company: clean(params.get("company")),
    salesman: clean(params.get("salesman")),
    preparedBy: clean(params.get("preparedBy")),
    status: clean(params.get("status")),
    piStatus: clean(params.get("piStatus")),
    typeOfWork: clean(params.get("typeOfWork")),
    zohoStatus: clean(params.get("zohoStatus")),
    sourceFile: clean(params.get("sourceFile"), 500),
  };
  const sortFields: RecordSortField[] = ["date", "pi_number", "customer_ref", "company", "salesman", "prepared_by", "qty", "sqm", "status", "pi_status", "type_of_work", "zoho_status"];
  const requestedSort = params.get("sortBy") as RecordSortField | null;
  const sort: RecordSort = {
    field: requestedSort && sortFields.includes(requestedSort) ? requestedSort : "date",
    direction: parseDirection(params.get("sortOrder"), "asc"),
  };

  const first = await fetchRecordsDashboardData({
    filters,
    sort,
    page: 1,
    pageSize: RECORD_EXPORT_PAGE_SIZE,
    authToken,
    includeAggregates: false,
  });
  if (first.pagination.totalEntries > OPERATIONS_EXPORT_ROW_LIMIT) {
    throw new ExportLimitError(
      `This export matches ${first.pagination.totalEntries.toLocaleString()} proforma rows. Narrow the filters to ${OPERATIONS_EXPORT_ROW_LIMIT.toLocaleString()} rows or fewer and try again.`,
    );
  }

  const rows = [...first.records];
  let offset = first.records.length;
  while (offset < first.pagination.totalEntries) {
    const next = await fetchRecordsDashboardData({
      filters,
      sort,
      page: 1,
      pageSize: RECORD_EXPORT_PAGE_SIZE,
      authToken,
      includeAggregates: false,
      exportOffset: offset,
    });
    if (next.pagination.totalEntries !== first.pagination.totalEntries) {
      throw new ExportConflictError("The proforma records changed while the export was being prepared. Refresh the page and try again.");
    }
    if (next.records.length === 0) {
      throw new ExportConflictError("The export stopped before all matching proforma rows could be read. Refresh the page and try again.");
    }
    rows.push(...next.records);
    offset += next.records.length;
  }

  const uniqueIds = new Set(rows.map((row) => String(row.id)));
  if (rows.length !== first.pagination.totalEntries || uniqueIds.size !== rows.length) {
    throw new ExportConflictError("The export could not read a stable set of proforma rows. Refresh the page and try again.");
  }
  return { rows, filters };
}

function recordsSummary(rows: RecordRow[]): Record<string, number | string> {
  const qty = rows.reduce((total, row) => total + (Number(String(row.qty).replace(/,/g, "")) || 0), 0);
  const sqm = rows.reduce((total, row) => total + (Number(String(row.sqm).replace(/,/g, "")) || 0), 0);
  return {
    "Proforma rows": rows.length,
    "Total quantity (pcs)": Number(qty.toFixed(2)),
    "Total area (m²)": Number(sqm.toFixed(2)),
    Companies: uniqueCount(rows.map((row) => row.company)),
  };
}

function parseJobOrderFilters(params: URLSearchParams): JobOrderFilters {
  return {
    search: clean(params.get("search")),
    datePreset: parsePreset(params.get("datePreset")),
    dateFrom: clean(params.get("dateFrom"), 20),
    dateTo: clean(params.get("dateTo"), 20),
    company: clean(params.get("company")),
    salesman: clean(params.get("salesman")),
    preparedBy: clean(params.get("preparedBy")),
    typeOfWork: clean(params.get("typeOfWork")),
    sourceFile: clean(params.get("sourceFile"), 500),
  };
}

function loadJobOrderSort(params: URLSearchParams): JobOrderSort {
  const sortFields: JobOrderSortField[] = ["order_date", "pi_number", "company", "ordered_qty", "ordered_sqm"];
  const requestedSort = params.get("sortBy") as JobOrderSortField | null;
  return {
    field: requestedSort && sortFields.includes(requestedSort) ? requestedSort : "order_date",
    direction: parseDirection(params.get("sortOrder"), "desc"),
  };
}

function mapJobOrderRows(rows: JobOrderRow[]): Record<string, unknown>[] {
  return rows.map((row) => ({ ...row, record_ids: row.record_ids.join(", ") }));
}

function parseDeliveryFilters(params: URLSearchParams): DeliveryFilters {
  const requestedStage = params.get("deliveryStatus") as DeliveryLifecycle | null;
  return {
    search: clean(params.get("search")),
    datePreset: parseDeliveryPreset(params.get("datePreset")),
    dateFrom: clean(params.get("dateFrom"), 20),
    dateTo: clean(params.get("dateTo"), 20),
    company: clean(params.get("company")),
    salesman: clean(params.get("salesman")),
    preparedBy: clean(params.get("preparedBy")),
    typeOfWork: clean(params.get("typeOfWork")),
    piStatus: clean(params.get("piStatus")),
    deliveryStatus: DELIVERY_STAGES.includes(requestedStage as DeliveryLifecycle) ? requestedStage! : "",
    balanceOnly: params.get("balanceOnly") === "1",
    sourceFile: clean(params.get("sourceFile"), 500),
    pi: clean(params.get("pi")),
  };
}

function loadDeliverySort(params: URLSearchParams): DeliverySort {
  const sortFields: DeliverySortField[] = ["pi_number", "company", "order_date", "ordered_qty", "ordered_sqm", "delivered_qty", "delivered_sqm", "balance_qty", "balance_sqm", "progress", "delivery_count", "last_delivery_date", "delivery_status"];
  const requestedSort = params.get("sortBy") as DeliverySortField | null;
  return {
    field: requestedSort && sortFields.includes(requestedSort) ? requestedSort : "order_date",
    direction: parseDirection(params.get("sortOrder"), "desc"),
  };
}

function mapDeliverySummaryRows(rows: DeliveryRow[]): Record<string, unknown>[] {
  return rows.map((row) => ({
    ...row,
    delivery_status: row.delivery_status.replaceAll("_", " "),
    source_file: row.source_file ?? "",
    movements: undefined,
  }));
}

function deliverySummaryFilters(filters: DeliveryFilters): Record<string, string | boolean | undefined> {
  return {
    search: filters.search,
    datePreset: filters.datePreset,
    dateFrom: filters.dateFrom,
    dateTo: filters.dateTo,
    company: filters.company,
    salesman: filters.salesman,
    preparedBy: filters.preparedBy,
    typeOfWork: filters.typeOfWork,
    piStatus: filters.piStatus,
    deliveryStatus: filters.deliveryStatus,
    balanceOnly: filters.balanceOnly,
    sourceFile: filters.sourceFile,
    pi: filters.pi,
  };
}

export async function buildOperationsExportBundle(
  report: OperationsExportReport,
  params: URLSearchParams,
  authToken: string | null,
): Promise<OperationsExportBundle> {
  const generatedAt = new Date().toISOString();

  if (report === "proformas") {
    const { rows, filters } = await loadAllProformas(params, authToken);
    const columns = proformaColumns();
    const sheet: ExportSheet = { name: "Proforma Records", columns, rows: asRecordRows(rows) };
    return {
      schemaVersion: 1,
      report,
      title: "Proforma Invoice Records",
      generatedAt,
      rowCount: rows.length,
      filters: filterSummary(filters),
      summary: recordsSummary(rows),
      sheets: [sheet],
      csvSheet: sheet,
    };
  }

  if (report === "job-orders") {
    const filters = parseJobOrderFilters(params);
    const data = await fetchJobOrdersDashboardData({
      filters,
      sort: loadJobOrderSort(params),
      page: 1,
      pageSize: OPERATIONS_EXPORT_ROW_LIMIT + 1,
      authToken,
    });
    if (data.pagination.totalEntries > OPERATIONS_EXPORT_ROW_LIMIT) {
      throw new ExportLimitError(
        `This export matches ${data.pagination.totalEntries.toLocaleString()} Job Orders. Narrow the filters to ${OPERATIONS_EXPORT_ROW_LIMIT.toLocaleString()} rows or fewer and try again.`,
      );
    }
    if (data.warning?.includes("safety limit")) {
      throw new ExportLimitError("The confirmed PI source read reached its safety limit. Narrow the workbook or date range before exporting.");
    }
    const columns = jobOrderColumns();
    const sheet: ExportSheet = { name: "Job Orders", columns, rows: mapJobOrderRows(data.rows) };
    return {
      schemaVersion: 1,
      report,
      title: "Confirmed Proforma Job Order Queue",
      generatedAt,
      rowCount: data.rows.length,
      filters: filterSummary(filters),
      summary: {
        "Confirmed PIs": data.summary.totalJobOrders,
        "Source lines": data.summary.totalSourceRows,
        "Confirmed quantity (pcs)": data.summary.totalQty,
        "Confirmed area (m²)": data.summary.totalSqm,
        Companies: data.summary.activeCompaniesCount,
        Salesmen: data.summary.activeSalesmenCount,
      },
      sheets: [sheet],
      csvSheet: sheet,
    };
  }

  const filters = parseDeliveryFilters(params);
  const data = await fetchDeliveriesDashboardData({
    filters,
    sort: loadDeliverySort(params),
    page: 1,
    pageSize: OPERATIONS_EXPORT_ROW_LIMIT,
    authToken,
  });
  if (data.truncated) {
    throw new ExportLimitError("The Delivery Note tracker reached its safe read limit. Narrow the workbook or filters before exporting to avoid an incomplete file.");
  }
  if (data.pagination.totalEntries > OPERATIONS_EXPORT_ROW_LIMIT) {
    throw new ExportLimitError(
      `This export matches ${data.pagination.totalEntries.toLocaleString()} PIs. Narrow the filters to ${OPERATIONS_EXPORT_ROW_LIMIT.toLocaleString()} rows or fewer and try again.`,
    );
  }

  const summaryColumns = deliverySummaryColumns();
  const movementColumns = deliveryMovementColumns();
  const summarySheet: ExportSheet = {
    name: "PI Summary",
    columns: summaryColumns,
    rows: mapDeliverySummaryRows(data.rows),
  };
  const movementSheet: ExportSheet = {
    name: "Delivery Movements",
    columns: movementColumns,
    rows: flattenDeliveryRows(data.rows),
  };
  return {
    schemaVersion: 1,
    report,
    title: "Delivery Note Tracker",
    generatedAt,
    rowCount: data.rows.length,
    filters: filterSummary(deliverySummaryFilters(filters)),
    summary: {
      "Confirmed PIs": data.summary.totalPis,
      "Delivery movements": data.summary.totalMovements,
      "Ordered quantity (pcs)": data.summary.orderedQty,
      "Ordered area (m²)": data.summary.orderedSqm,
      "Delivered quantity (pcs)": data.summary.deliveredQty,
      "Delivered area (m²)": data.summary.deliveredSqm,
      "Balance quantity (pcs)": data.summary.balanceQty,
      "Balance area (m²)": data.summary.balanceSqm,
      "Progress (%)": data.summary.progressPct,
    },
    sheets: [summarySheet, movementSheet],
    csvSheet: movementSheet,
  };
}

export function exportFileBase(bundle: OperationsExportBundle): string {
  return `${bundle.report}-${bundle.generatedAt.slice(0, 10)}`;
}

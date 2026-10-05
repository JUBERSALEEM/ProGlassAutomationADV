/**
 * Pure transformations for the confirmed-proforma → Job Order queue.
 *
 * One PI may arrive as several records (for example, multiple work lines in an
 * imported workbook), so the queue groups source rows by PI before it counts,
 * filters, sorts or paginates. Only the exact, case-sensitive `Confirmed`
 * status qualifies; `confirmed`, padded values, `In Production`, `Ready`, and
 * other statuses are not pulled into this hand-off queue.
 */
import { getDateEpoch } from "@/lib/date-utils";
import { isActiveConfirmedProforma } from "@/lib/job-order-source-gate";
import type {
  JobOrderFilterOptions,
  JobOrderFilters,
  JobOrderRow,
  JobOrderSort,
  JobOrderSourceLine,
  JobOrderSummary,
} from "@/types/job-orders";

export type JobOrderSourceRecord = {
  id?: string | number | null;
  date?: unknown;
  pi_number?: unknown;
  company?: unknown;
  customer_ref?: unknown;
  salesman?: unknown;
  prepared_by?: unknown;
  type_of_work?: unknown;
  status?: unknown;
  pi_status?: unknown;
  zoho_status?: unknown;
  qty?: unknown;
  sqm?: unknown;
  is_active?: unknown;
  source_file?: unknown;
};

const text = (value: unknown): string =>
  typeof value === "string" || typeof value === "number" ? String(value).trim() : "";

const number = (value: unknown): number => {
  if (typeof value === "number") return Number.isFinite(value) ? value : 0;
  if (typeof value !== "string") return 0;
  const parsed = Number(value.replace(/,/g, "").trim());
  return Number.isFinite(parsed) ? parsed : 0;
};

const round2 = (value: number): number => Math.round((value + Number.EPSILON) * 100) / 100;

function sourceLine(record: JobOrderSourceRecord): JobOrderSourceLine {
  return {
    id: record.id === null || record.id === undefined ? "" : String(record.id),
    type_of_work: text(record.type_of_work),
    qty: number(record.qty),
    sqm: number(record.sqm),
  };
}

function normalizeSource(value: unknown): string {
  return text(value).toLocaleLowerCase();
}

function naturalCompare(a: string, b: string): number {
  return a.localeCompare(b, undefined, { numeric: true, sensitivity: "base" });
}

/** Group active `Confirmed` source records into one queue row per PI. */
export function groupConfirmedProformas(records: JobOrderSourceRecord[]): JobOrderRow[] {
  const groups = new Map<string, JobOrderRow & { orderDateEpoch: number }>();

  for (const record of records) {
    if (!isActiveConfirmedProforma(record.status, record.is_active)) continue;

    const piNumber = text(record.pi_number);
    if (!piNumber) continue;

    const sourceFile = text(record.source_file) || null;
    const key = `${piNumber.toLocaleLowerCase()}\u0000${normalizeSource(sourceFile)}`;
    const epoch = getDateEpoch(record.date);
    const existing = groups.get(key);

    if (existing) {
      existing.ordered_qty += number(record.qty);
      existing.ordered_sqm = round2(existing.ordered_sqm + number(record.sqm));
      existing.record_count += 1;
      existing.source_lines.push(sourceLine(record));
      if (record.id !== null && record.id !== undefined) existing.record_ids.push(record.id);
      if (!existing.company) existing.company = text(record.company);
      if (!existing.customer_ref) existing.customer_ref = text(record.customer_ref);
      if (!existing.salesman) existing.salesman = text(record.salesman);
      if (!existing.prepared_by) existing.prepared_by = text(record.prepared_by);
      if (!existing.type_of_work) existing.type_of_work = text(record.type_of_work);
      if (!existing.pi_status) existing.pi_status = text(record.pi_status);
      if (!existing.zoho_status) existing.zoho_status = text(record.zoho_status);
      if (!existing.source_file) existing.source_file = sourceFile;
      if (epoch > 0 && (existing.orderDateEpoch === 0 || epoch < existing.orderDateEpoch)) {
        existing.orderDateEpoch = epoch;
        existing.order_date = text(record.date);
      }
      continue;
    }

    const id = sourceFile ? `${piNumber}::${sourceFile}` : piNumber;
    groups.set(key, {
      id,
      pi_number: piNumber,
      company: text(record.company),
      customer_ref: text(record.customer_ref),
      salesman: text(record.salesman),
      prepared_by: text(record.prepared_by),
      type_of_work: text(record.type_of_work),
      order_date: text(record.date),
      orderDateEpoch: epoch,
      confirmation_status: "Confirmed",
      pi_status: text(record.pi_status),
      zoho_status: text(record.zoho_status),
      ordered_qty: number(record.qty),
      ordered_sqm: round2(number(record.sqm)),
      record_count: 1,
      record_ids: record.id === null || record.id === undefined ? [] : [record.id],
      source_lines: [sourceLine(record)],
      source_file: sourceFile,
    });
  }

  return Array.from(groups.values())
    .map(({ orderDateEpoch: _orderDateEpoch, ...row }) => ({
      ...row,
      ordered_qty: round2(row.ordered_qty),
      ordered_sqm: round2(row.ordered_sqm),
    }))
    .sort((a, b) => {
      const dateOrder = getDateEpoch(b.order_date) - getDateEpoch(a.order_date);
      return dateOrder || naturalCompare(a.pi_number, b.pi_number);
    });
}

export function filterJobOrders(
  rows: JobOrderRow[],
  filters: JobOrderFilters,
  dateRange: { from: string; to: string },
): JobOrderRow[] {
  const search = filters.search.trim().toLocaleLowerCase();
  const fromEpoch = dateRange.from ? getDateEpoch(dateRange.from) : 0;
  const toEpoch = dateRange.to ? getDateEpoch(dateRange.to) : 0;

  return rows.filter((row) => {
    if (filters.company && row.company !== filters.company) return false;
    if (filters.salesman && row.salesman !== filters.salesman) return false;
    if (filters.preparedBy && row.prepared_by !== filters.preparedBy) return false;
    if (filters.typeOfWork && row.type_of_work !== filters.typeOfWork) return false;

    const orderEpoch = getDateEpoch(row.order_date);
    if (fromEpoch && (!orderEpoch || orderEpoch < fromEpoch)) return false;
    if (toEpoch && (!orderEpoch || orderEpoch > toEpoch + 86_399_999)) return false;

    if (search) {
      const haystack = [
        row.pi_number,
        row.company,
        row.customer_ref,
        row.salesman,
        row.prepared_by,
        row.type_of_work,
      ]
        .join(" ")
        .toLocaleLowerCase();
      if (!haystack.includes(search)) return false;
    }

    return true;
  });
}

export function sortJobOrders(rows: JobOrderRow[], sort: JobOrderSort): JobOrderRow[] {
  const direction = sort.direction === "asc" ? 1 : -1;
  return [...rows].sort((a, b) => {
    let comparison = 0;
    switch (sort.field) {
      case "order_date":
        comparison = getDateEpoch(a.order_date) - getDateEpoch(b.order_date);
        break;
      case "pi_number":
        comparison = naturalCompare(a.pi_number, b.pi_number);
        break;
      case "company":
        comparison = a.company.localeCompare(b.company, undefined, { sensitivity: "base" });
        break;
      case "ordered_qty":
        comparison = a.ordered_qty - b.ordered_qty;
        break;
      case "ordered_sqm":
        comparison = a.ordered_sqm - b.ordered_sqm;
        break;
    }
    if (comparison !== 0) return comparison * direction;
    return naturalCompare(a.pi_number, b.pi_number);
  });
}

export function buildJobOrderSummary(rows: JobOrderRow[]): JobOrderSummary {
  let totalSourceRows = 0;
  let totalQty = 0;
  let totalSqm = 0;
  const companies = new Set<string>();
  const salesmen = new Set<string>();

  for (const row of rows) {
    totalSourceRows += row.record_count;
    totalQty += row.ordered_qty;
    totalSqm += row.ordered_sqm;
    if (row.company) companies.add(row.company);
    if (row.salesman) salesmen.add(row.salesman);
  }

  return {
    totalJobOrders: rows.length,
    totalSourceRows,
    totalQty: round2(totalQty),
    totalSqm: round2(totalSqm),
    activeCompaniesCount: companies.size,
    activeSalesmenCount: salesmen.size,
  };
}

export function buildJobOrderFilterOptions(
  rows: JobOrderRow[],
  extra: { sourceFiles?: string[]; currentSourceFile?: string | null } = {},
): JobOrderFilterOptions {
  const distinct = (getter: (row: JobOrderRow) => string) => {
    const values = new Set<string>();
    for (const row of rows) {
      const value = getter(row).trim();
      if (value) values.add(value);
    }
    return Array.from(values).sort((a, b) => a.localeCompare(b, undefined, { sensitivity: "base" }));
  };

  return {
    companies: distinct((row) => row.company),
    salesmen: distinct((row) => row.salesman),
    preparedBy: distinct((row) => row.prepared_by),
    typesOfWork: distinct((row) => row.type_of_work),
    sourceFiles: extra.sourceFiles ?? [],
    currentSourceFile: extra.currentSourceFile ?? null,
  };
}

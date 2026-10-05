/**
 * Deep links between the two operational dashboards.
 *
 * A tab gets an operator to the other page. A deep link gets them to the *same
 * PI* on the other page, which is what "connect both pages" actually means in
 * use: the person reading a row in Reports should be able to see its dispatch
 * history without retyping an eleven-character PI number, and the person
 * looking at a delivery balance should be able to reconcile the original row.
 *
 * Both builders drop empty values rather than emitting `&company=`, so a link
 * from an unfiltered page stays a clean URL and the receiving page's defaults
 * apply as designed.
 */

import type { RecordFilters } from "@/types/records";
import type { DeliveryFilters } from "@/types/deliveries";

function toQuery(params: Record<string, string | undefined>): string {
  const search = new URLSearchParams();
  for (const [key, value] of Object.entries(params)) {
    if (value && value !== "current") search.set(key, value);
  }
  const query = search.toString();
  return query ? `?${query}` : "";
}

/** Reports → Deliveries, preserving the slice of the workbook the operator is looking at. */
export function deliveriesHrefFromRecordFilters(
  filters: Partial<RecordFilters>,
  piNumber?: string,
): string {
  return `/deliveries${toQuery({
    pi: piNumber,
    search: piNumber ? undefined : filters.search,
    company: filters.company,
    salesman: filters.salesman,
    preparedBy: filters.preparedBy,
    typeOfWork: filters.typeOfWork,
    piStatus: filters.piStatus,
    sourceFile: filters.sourceFile,
  })}`;
}

/** Deliveries → Reports: find the source row behind a tracker row. */
export function reportsHrefForPi(piNumber: string, deliveryFilters?: Partial<DeliveryFilters>): string {
  return `/reports${toQuery({
    search: piNumber,
    company: deliveryFilters?.company,
    salesman: deliveryFilters?.salesman,
    preparedBy: deliveryFilters?.preparedBy,
    typeOfWork: deliveryFilters?.typeOfWork,
  })}`;
}

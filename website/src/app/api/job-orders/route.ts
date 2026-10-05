import { NextResponse, type NextRequest } from "next/server";
import { fetchJobOrdersDashboardData } from "@/lib/job-orders-api";
import type { JobOrderFilters, JobOrderSort, JobOrderSortField } from "@/types/job-orders";
import type { DatePreset } from "@/types/records";

export const dynamic = "force-dynamic";
export const revalidate = 0;

const DATE_PRESETS: DatePreset[] = [
  "all",
  "today",
  "yesterday",
  "this_week",
  "this_month",
  "previous_month",
  "custom",
];
const SORT_FIELDS: JobOrderSortField[] = [
  "order_date",
  "pi_number",
  "company",
  "ordered_qty",
  "ordered_sqm",
];
const clean = (value: string | null, maxLength = 160) => (value || "").trim().slice(0, maxLength);

export async function GET(request: NextRequest) {
  const { searchParams } = new URL(request.url);
  const authHeader = request.headers.get("authorization");
  const authToken = authHeader?.replace(/^Bearer\s+/i, "").trim() || null;

  if (process.env.NODE_ENV === "production" && !authToken) {
    return NextResponse.json(
      {
        ok: false,
        error: {
          message: "Authentication required. Please sign in to access job orders.",
          code: "UNAUTHORIZED",
        },
      },
      {
        status: 401,
        headers: {
          "WWW-Authenticate": 'Bearer realm="Operational Job Orders"',
          "Cache-Control": "no-store",
        },
      },
    );
  }

  try {
    const requestedPreset = searchParams.get("datePreset") || "all";
    const requestedSort = searchParams.get("sortBy") || "order_date";
    const filters: JobOrderFilters = {
      search: clean(searchParams.get("search")),
      datePreset: DATE_PRESETS.includes(requestedPreset as DatePreset)
        ? (requestedPreset as DatePreset)
        : "all",
      dateFrom: clean(searchParams.get("dateFrom"), 20),
      dateTo: clean(searchParams.get("dateTo"), 20),
      company: clean(searchParams.get("company")),
      salesman: clean(searchParams.get("salesman")),
      preparedBy: clean(searchParams.get("preparedBy")),
      typeOfWork: clean(searchParams.get("typeOfWork")),
      sourceFile: clean(searchParams.get("sourceFile")),
    };
    const sort: JobOrderSort = {
      field: SORT_FIELDS.includes(requestedSort as JobOrderSortField)
        ? (requestedSort as JobOrderSortField)
        : "order_date",
      direction: searchParams.get("sortOrder") === "asc" ? "asc" : "desc",
    };

    const data = await fetchJobOrdersDashboardData({
      filters,
      sort,
      page: Math.max(1, parseInt(searchParams.get("page") || "1", 10) || 1),
      pageSize: Math.min(250, Math.max(10, parseInt(searchParams.get("pageSize") || "50", 10) || 50)),
      authToken,
    });

    return NextResponse.json(
      { ok: true, data },
      {
        status: 200,
        headers: {
          "Cache-Control": "private, no-cache, no-store, max-age=0, must-revalidate",
          Pragma: "no-cache",
          Expires: "0",
        },
      },
    );
  } catch (error) {
    console.error("[job-orders-api-route] Unhandled error:", error);
    const message = error instanceof Error ? error.message : "Internal error occurred";
    const isAuthError = /jwt|token|expired|unauthorized|permission denied|42501|PGRST301/i.test(message);
    return NextResponse.json(
      {
        ok: false,
        error: {
          message: isAuthError
            ? "Your operator session has expired or is unauthorized. Please sign in again."
            : "Unable to load confirmed proformas. Please try again or adjust your filters.",
          code: isAuthError ? "UNAUTHORIZED" : "INTERNAL_ERROR",
          details: message,
        },
      },
      {
        status: isAuthError ? 401 : 500,
        headers: isAuthError ? { "WWW-Authenticate": 'Bearer error="invalid_token"', "Cache-Control": "no-store" } : undefined,
      },
    );
  }
}

import { NextResponse, type NextRequest } from "next/server";
import { fetchRecordsDashboardData } from "@/lib/records-api";
import type {
  DatePreset,
  RecordFilters,
  RecordSort,
  RecordSortField,
  SortDirection,
} from "@/types/records";

export const dynamic = "force-dynamic";
export const revalidate = 0;

export async function GET(request: NextRequest) {
  try {
    const { searchParams } = new URL(request.url);

    const page = Math.max(1, parseInt(searchParams.get("page") || "1", 10) || 1);
    const pageSize = Math.min(250, Math.max(10, parseInt(searchParams.get("pageSize") || "50", 10) || 50));

    const sortField = (searchParams.get("sortBy") || "date") as RecordSortField;
    const sortDirection = (searchParams.get("sortOrder") || "desc") as SortDirection;

    const validSortFields: RecordSortField[] = [
      "date",
      "pi_number",
      "customer_ref",
      "company",
      "salesman",
      "prepared_by",
      "qty",
      "sqm",
      "status",
      "pi_status",
      "type_of_work",
      "zoho_status",
    ];

    const sort: RecordSort = {
      field: validSortFields.includes(sortField) ? sortField : "date",
      direction: sortDirection === "asc" ? "asc" : "desc",
    };

    const filters: RecordFilters = {
      search: searchParams.get("search") || "",
      datePreset: (searchParams.get("datePreset") || "all") as DatePreset,
      dateFrom: searchParams.get("dateFrom") || "",
      dateTo: searchParams.get("dateTo") || "",
      company: searchParams.get("company") || "",
      salesman: searchParams.get("salesman") || "",
      preparedBy: searchParams.get("preparedBy") || "",
      status: searchParams.get("status") || "",
      piStatus: searchParams.get("piStatus") || "",
      typeOfWork: searchParams.get("typeOfWork") || "",
      zohoStatus: searchParams.get("zohoStatus") || "",
      sourceFile: searchParams.get("sourceFile") || "",
    };

    // Extract Bearer token from authorization header
    const authHeader = request.headers.get("authorization");
    const authToken = authHeader ? authHeader.replace(/^Bearer\s+/i, "").trim() : null;

    // In production, enforce authentication to protect operational records
    if (process.env.NODE_ENV === "production" && !authToken) {
      return NextResponse.json(
        {
          ok: false,
          error: {
            message: "Authentication required. Please sign in to access operational reports.",
            code: "UNAUTHORIZED",
          },
        },
        {
          status: 401,
          headers: {
            "WWW-Authenticate": "Bearer realm=\"Operational Records\"",
            "Cache-Control": "no-store",
          },
        },
      );
    }

    const result = await fetchRecordsDashboardData({
      filters,
      sort,
      page,
      pageSize,
      authToken,
    });

    return NextResponse.json(
      {
        ok: true,
        data: result,
      },
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
    console.error("[records-api-route] Unhandled error:", error);
    const message = error instanceof Error ? error.message : "Internal error occurred";
    const isAuthError =
      /jwt|token|expired|unauthorized|permission denied|42501|PGRST301/i.test(message);

    return NextResponse.json(
      {
        ok: false,
        error: {
          message: isAuthError
            ? "Your operator session has expired or is unauthorized. Please sign in again."
            : "Unable to load records. Please try again or adjust your filters.",
          code: isAuthError ? "UNAUTHORIZED" : "INTERNAL_ERROR",
          details: message,
        },
      },
      {
        status: isAuthError ? 401 : 500,
        headers: isAuthError
          ? {
              "WWW-Authenticate": "Bearer error=\"invalid_token\"",
              "Cache-Control": "no-store",
            }
          : undefined,
      },
    );
  }
}

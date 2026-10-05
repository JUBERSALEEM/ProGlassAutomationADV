import { NextResponse, type NextRequest } from "next/server";
import { DELIVERY_SORT_FIELDS, fetchDeliveriesDashboardData } from "@/lib/deliveries-api";
import type {
  DeliveryDatePreset,
  DeliveryFilters,
  DeliveryLifecycle,
  DeliverySort,
  DeliverySortField,
  SortDirection,
} from "@/types/deliveries";

export const dynamic = "force-dynamic";
export const revalidate = 0;

const LIFECYCLES: DeliveryLifecycle[] = [
  "awaiting_dispatch",
  "partial",
  "complete",
  "over_delivered",
];

export async function GET(request: NextRequest) {
  try {
    const { searchParams } = new URL(request.url);

    const page = Math.max(1, parseInt(searchParams.get("page") || "1", 10) || 1);
    const pageSize = Math.min(
      250,
      Math.max(10, parseInt(searchParams.get("pageSize") || "50", 10) || 50),
    );

    const sortField = (searchParams.get("sortBy") || "order_date") as DeliverySortField;
    const sortDirection = (searchParams.get("sortOrder") || "desc") as SortDirection;

    const sort: DeliverySort = {
      field: DELIVERY_SORT_FIELDS.includes(sortField) ? sortField : "order_date",
      direction: sortDirection === "asc" ? "asc" : "desc",
    };

    const deliveryStatus = (searchParams.get("deliveryStatus") || "") as DeliveryLifecycle | "";

    const filters: DeliveryFilters = {
      search: searchParams.get("search") || "",
      datePreset: (searchParams.get("datePreset") || "all") as DeliveryDatePreset,
      dateFrom: searchParams.get("dateFrom") || "",
      dateTo: searchParams.get("dateTo") || "",
      company: searchParams.get("company") || "",
      salesman: searchParams.get("salesman") || "",
      preparedBy: searchParams.get("preparedBy") || "",
      typeOfWork: searchParams.get("typeOfWork") || "",
      piStatus: searchParams.get("piStatus") || "",
      deliveryStatus: LIFECYCLES.includes(deliveryStatus as DeliveryLifecycle)
        ? (deliveryStatus as DeliveryLifecycle)
        : "",
      balanceOnly: searchParams.get("balanceOnly") === "1",
      sourceFile: searchParams.get("sourceFile") || "",
      pi: searchParams.get("pi") || "",
    };

    // Extract Bearer token from authorization header
    const authHeader = request.headers.get("authorization");
    const authToken = authHeader ? authHeader.replace(/^Bearer\s+/i, "").trim() : null;

    // The tracker exposes the same operational data as the reports dashboard,
    // so it stands behind the same door: a production build requires an
    // operator session, and Row Level Security decides what that session sees.
    if (process.env.NODE_ENV === "production" && !authToken) {
      return NextResponse.json(
        {
          ok: false,
          error: {
            message: "Authentication required. Please sign in to access the deliveries tracker.",
            code: "UNAUTHORIZED",
          },
        },
        {
          status: 401,
          headers: {
            "WWW-Authenticate": 'Bearer realm="Deliveries Tracker"',
            "Cache-Control": "no-store",
          },
        },
      );
    }

    const result = await fetchDeliveriesDashboardData({
      filters,
      sort,
      page,
      pageSize,
      authToken,
    });

    return NextResponse.json(
      { ok: true, data: result },
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
    console.error("[deliveries-api-route] Unhandled error:", error);
    const message = error instanceof Error ? error.message : "Internal error occurred";
    const isAuthError =
      /jwt|token|expired|unauthorized|permission denied|42501|PGRST301/i.test(message);

    return NextResponse.json(
      {
        ok: false,
        error: {
          message: isAuthError
            ? "Your operator session has expired or is unauthorized. Please sign in again."
            : "Unable to load deliveries. Please try again or adjust your filters.",
          code: isAuthError ? "UNAUTHORIZED" : "INTERNAL_ERROR",
          details: message,
        },
      },
      {
        status: isAuthError ? 401 : 500,
        headers: isAuthError
          ? {
              "WWW-Authenticate": 'Bearer error="invalid_token"',
              "Cache-Control": "no-store",
            }
          : undefined,
      },
    );
  }
}

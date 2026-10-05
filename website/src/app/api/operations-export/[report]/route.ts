import { NextResponse, type NextRequest } from "next/server";
import { createCsv, createXlsx } from "@/lib/export-formats";
import { createOperationsPdf } from "@/lib/operations-pdf";
import {
  buildOperationsExportBundle,
  exportFileBase,
  ExportConflictError,
  ExportLimitError,
  type OperationsExportFormat,
  type OperationsExportReport,
} from "@/lib/operations-exports";

export const runtime = "nodejs";
export const dynamic = "force-dynamic";
export const revalidate = 0;

const REPORTS: OperationsExportReport[] = ["proformas", "job-orders", "deliveries"];
const FORMATS: OperationsExportFormat[] = ["csv", "xlsx", "pdf"];

function jsonError(message: string, status: number, code: string) {
  return NextResponse.json(
    { ok: false, error: { message, code } },
    {
      status,
      headers: {
        "Cache-Control": "no-store",
        "X-Robots-Tag": "noindex, nofollow",
      },
    },
  );
}

export async function GET(
  request: NextRequest,
  context: { params: Promise<{ report: string }> },
) {
  const authHeader = request.headers.get("authorization");
  const authToken = authHeader?.replace(/^Bearer\s+/i, "").trim() || null;
  if (process.env.NODE_ENV === "production" && !authToken) {
    return jsonError("Authentication required. Sign in to export operational data.", 401, "UNAUTHORIZED");
  }

  const { report: requestedReport } = await context.params;
  if (!REPORTS.includes(requestedReport as OperationsExportReport)) {
    return jsonError("Unknown operations export report.", 404, "NOT_FOUND");
  }

  const params = new URL(request.url).searchParams;
  const format = params.get("format") as OperationsExportFormat | null;
  if (!format || !FORMATS.includes(format)) {
    return jsonError("Choose CSV, XLSX, or PDF export format.", 400, "INVALID_FORMAT");
  }

  try {
    const bundle = await buildOperationsExportBundle(requestedReport as OperationsExportReport, params, authToken);
    const baseName = exportFileBase(bundle);
    let bytes: Uint8Array | Buffer;
    let extension: string;
    let contentType: string;

    if (format === "csv") {
      bytes = createCsv(bundle.csvSheet);
      extension = "csv";
      contentType = "text/csv; charset=utf-8";
    } else if (format === "xlsx") {
      bytes = createXlsx(bundle);
      extension = "xlsx";
      contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    } else {
      bytes = await createOperationsPdf(bundle);
      extension = "pdf";
      contentType = "application/pdf";
      if (bytes.byteLength === 0 || bytes.byteLength > 25 * 1024 * 1024) {
        return jsonError("The generated PDF is empty or exceeds the 25 MB download limit. Narrow the filters and retry.", 413, "PDF_SIZE_LIMIT");
      }
    }

    return new Response(bytes as BodyInit, {
      status: 200,
      headers: {
        "Content-Type": contentType,
        "Content-Disposition": `attachment; filename="${baseName}.${extension}"`,
        "Content-Length": String(bytes.byteLength),
        "Cache-Control": "private, no-store, max-age=0",
        "X-Content-Type-Options": "nosniff",
        "X-Robots-Tag": "noindex, nofollow",
      },
    });
  } catch (error) {
    if (error instanceof ExportLimitError) return jsonError(error.message, error.status, "EXPORT_LIMIT");
    if (error instanceof ExportConflictError) return jsonError(error.message, error.status, "DATA_CHANGED");

    const message = error instanceof Error ? error.message : "Unable to create this export.";
    if (/jwt|token|expired|unauthorized|permission denied|42501|PGRST301/i.test(message)) {
      return jsonError("Your operator session has expired or is unauthorized. Please sign in again.", 401, "UNAUTHORIZED");
    }

    console.error("[operations-export] Export failed:", error);
    return jsonError("Unable to create this export. Try a smaller filtered result or contact the administrator.", 500, "EXPORT_FAILED");
  }
}

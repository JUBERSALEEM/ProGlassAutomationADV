import { NextResponse, type NextRequest } from "next/server";
import { createCsv, createXlsx } from "@/lib/export-formats";
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

async function proxyQuestPdf(bundle: Awaited<ReturnType<typeof buildOperationsExportBundle>>): Promise<Response> {
  const configuredUrl = process.env.QUESTPDF_EXPORT_SERVICE_URL?.trim().replace(/\/+$/, "");
  const serviceToken = process.env.QUESTPDF_EXPORT_SERVICE_TOKEN?.trim();
  if (!configuredUrl || !serviceToken) {
    throw new Error(
      "QuestPDF PDF export is not configured yet. Deploy the .NET QuestPDF export service and set QUESTPDF_EXPORT_SERVICE_URL and QUESTPDF_EXPORT_SERVICE_TOKEN in the website environment.",
    );
  }

  const parsedUrl = new URL(configuredUrl);
  if (
    !["https:", "http:"].includes(parsedUrl.protocol) ||
    (process.env.NODE_ENV === "production" && parsedUrl.protocol !== "https:") ||
    parsedUrl.username || parsedUrl.password || parsedUrl.search || parsedUrl.hash || parsedUrl.pathname !== "/"
  ) {
    throw new Error("Configure the QuestPDF service as an origin-only URL; production requires HTTPS.");
  }

  const response = await fetch(`${configuredUrl}/api/v1/operations-reports`,{
    method: "POST",
    headers: {
      Authorization: `Bearer ${serviceToken}`,
      "Content-Type": "application/json",
      Accept: "application/pdf",
    },
    body: JSON.stringify(bundle),
    cache: "no-store",
    redirect: "error",
    signal: AbortSignal.timeout(60_000),
  });

  if (!response.ok) {
    const details = (await response.text()).slice(0, 500);
    console.error("[operations-export] QuestPDF service rejected export:", response.status, details);
    throw new Error("The QuestPDF service could not create this PDF. Try a smaller filtered export or contact the administrator.");
  }

  const contentType = response.headers.get("content-type")?.toLocaleLowerCase() ?? "";
  if (!contentType.includes("application/pdf")) {
    throw new Error("The configured PDF export service returned an unexpected file type.");
  }

  const bytes = await response.arrayBuffer();
  if (bytes.byteLength === 0 || bytes.byteLength > 25 * 1024 * 1024) {
    throw new Error("The PDF service returned an empty or oversized document.");
  }
  return new Response(bytes, {
    status: 200,
    headers: {
      "Content-Type": "application/pdf",
      "Content-Length": String(bytes.byteLength),
    },
  });
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

    if (format === "pdf") {
      const pdf = await proxyQuestPdf(bundle);
      const bytes = await pdf.arrayBuffer();
      return new Response(bytes, {
        status: 200,
        headers: {
          "Content-Type": "application/pdf",
          "Content-Disposition": `attachment; filename="${baseName}.pdf"`,
          "Cache-Control": "private, no-store, max-age=0",
          "X-Content-Type-Options": "nosniff",
          "X-Robots-Tag": "noindex, nofollow",
        },
      });
    }

    const bytes = format === "csv" ? createCsv(bundle.csvSheet) : createXlsx(bundle);
    const extension = format === "csv" ? "csv" : "xlsx";
    const contentType = format === "csv"
      ? "text/csv; charset=utf-8"
      : "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
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
    if (/QuestPDF PDF export is not configured/i.test(message)) {
      return jsonError(message, 503, "PDF_SERVICE_NOT_CONFIGURED");
    }
    if (/jwt|token|expired|unauthorized|permission denied|42501|PGRST301/i.test(message)) {
      return jsonError("Your operator session has expired or is unauthorized. Please sign in again.", 401, "UNAUTHORIZED");
    }

    console.error("[operations-export] Export failed:", error);
    return jsonError(message, 500, "EXPORT_FAILED");
  }
}

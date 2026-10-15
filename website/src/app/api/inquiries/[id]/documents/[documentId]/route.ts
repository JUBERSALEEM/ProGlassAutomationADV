import { NextResponse, type NextRequest } from "next/server";
import { authorizeDocumentDownload, InquiryApiFailure } from "@/lib/inquiries-api";

export const runtime = "nodejs";
export const dynamic = "force-dynamic";

function safeDownloadName(value: string): { ascii: string; encoded: string } {
  const normalized = value.normalize("NFKC").replace(/[\u0000-\u001f\u007f"\\/]/g, "_").slice(0, 180) || "inquiry-document";
  const ascii = normalized.replace(/[^\x20-\x7e]/g, "_");
  return { ascii, encoded: encodeURIComponent(normalized).replace(/[!'()*]/g, (character) => `%${character.charCodeAt(0).toString(16).toUpperCase()}`) };
}
function errorResponse(error: unknown) {
  const failure = error instanceof InquiryApiFailure ? error : new InquiryApiFailure("INTERNAL_ERROR", "Unable to open this document.");
  if (!(error instanceof InquiryApiFailure)) console.error("[inquiry-download-route] Unexpected failure:", error);
  const status = failure.code === "UNAUTHORIZED" ? 401 : failure.code === "FORBIDDEN" ? 403 : failure.code === "NOT_FOUND" ? 404 : failure.code === "NOT_CONFIGURED" || failure.code === "TEMPORARY_FAILURE" ? 503 : 500;
  return NextResponse.json({ ok: false, error: { message: failure.message, code: failure.code } }, { status, headers: { "Cache-Control": "private, no-store", "X-Robots-Tag": "noindex, nofollow" } });
}

export async function GET(request: NextRequest, context: { params: Promise<{ id: string; documentId: string }> }) {
  const token = request.headers.get("authorization")?.replace(/^Bearer\s+/i, "").trim() || "";
  if (!token) return errorResponse(new InquiryApiFailure("UNAUTHORIZED", "Sign in to download private inquiry documents."));
  try {
    const params = await context.params;
    const file = await authorizeDocumentDownload(token, params.id, params.documentId);
    const filename = safeDownloadName(file.filename);
    return new NextResponse(file.bytes, { status: 200, headers: {
      "Content-Type": file.mime || "application/octet-stream", "Content-Length": String(file.byteSize),
      "Content-Disposition": `attachment; filename="${filename.ascii}"; filename*=UTF-8''${filename.encoded}`,
      "Cache-Control": "private, no-store", "X-Content-Type-Options": "nosniff", "X-Robots-Tag": "noindex, nofollow",
      "Content-Security-Policy": "sandbox",
    } });
  } catch (error) { return errorResponse(error); }
}

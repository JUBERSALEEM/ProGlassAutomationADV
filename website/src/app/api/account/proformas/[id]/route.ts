import { NextResponse, type NextRequest } from "next/server";

export const runtime = "nodejs";
export const dynamic = "force-dynamic";
export const revalidate = 0;

const HEADERS = { "Cache-Control": "private, no-store", "X-Robots-Tag": "noindex, nofollow" };

/** Retired customer endpoint: direct ID access must not retrieve a priced PI. */
export function GET(_request: NextRequest, _context: { params: Promise<{ id: string }> }) {
  return NextResponse.json(
    {
      ok: false,
      error: {
        message: "Priced proformas are available only through the authorized ProGlass staff workflow.",
        code: "CUSTOMER_PRICING_DISABLED",
      },
    },
    { status: 410, headers: HEADERS },
  );
}

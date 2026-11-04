import { NextResponse, type NextRequest } from "next/server";

export const runtime = "nodejs";
export const dynamic = "force-dynamic";
export const revalidate = 0;

const HEADERS = { "Cache-Control": "private, no-store", "X-Robots-Tag": "noindex, nofollow" };

/** Retired customer endpoint: customer accounts must never receive priced PI data. */
export function GET(_request: NextRequest) {
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

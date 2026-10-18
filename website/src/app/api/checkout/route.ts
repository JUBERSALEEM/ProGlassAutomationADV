import { NextResponse } from "next/server";
import { isSkuBuyable, skuById } from "@/data/plans";

/**
 * ═══════════════════════════════════════════════════════════════════════
 * CHECKOUT — the stable address in front of whichever provider is used
 * ═══════════════════════════════════════════════════════════════════════
 *
 * `GET /api/checkout?sku=<id>` 307-redirects to the hosted payment link
 * configured for that SKU in `src/data/plans.ts`. Payment links live
 * there (public, versioned) rather than in page markup so the provider
 * can change — Paddle today, Razorpay tomorrow — without a single
 * published URL changing.
 *
 * Until a SKU has both a published price and an https payment link this
 * answers 501 with an honest message. That is the shipped default:
 * deploying this route opens no checkout by itself.
 */

export const dynamic = "force-dynamic";

export function GET(request: Request) {
  const skuId = new URL(request.url).searchParams.get("sku") ?? "";
  const sku = skuById(skuId);

  if (!sku || !isSkuBuyable(sku)) {
    return NextResponse.json(
      {
        ok: false,
        configured: false,
        message:
          "Online checkout is not open for this plan yet. Request pricing via /contact and a person will answer.",
      },
      { status: 501 },
    );
  }

  return NextResponse.redirect(sku.paymentLink, 307);
}

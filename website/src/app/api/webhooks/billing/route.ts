import { NextResponse } from "next/server";
import { timingSafeEqual } from "node:crypto";
import { durationByDays, durationById, skuById } from "@/data/plans";
import { isSupabaseConfigured, supabaseServer } from "@/lib/supabase";

/**
 * ═══════════════════════════════════════════════════════════════════════
 * BILLING WEBHOOK — paid order intake (Phase 1)
 * ═══════════════════════════════════════════════════════════════════════
 *
 * A payment provider (or a relay like Make/Zapier in v1) POSTs a paid
 * order here; the order lands in `public.license_orders` through the
 * `record_license_order` RPC (website/supabase-billing.sql), where the
 * vendor's existing .NET License Generator picks it up and issues the
 * signed key. Key signing deliberately stays in the .NET tool — this
 * route records money, it never mints licences.
 *
 * Dormant by default: without BILLING_WEBHOOK_SECRET every call answers
 * 501. Authentication in v1 is that shared secret in the
 * `x-billing-secret` header, compared in constant time. When a provider
 * is chosen, replace that check with the provider's signature
 * verification (Paddle / Lemon Squeezy / Razorpay each sign differently)
 * and keep everything below it.
 *
 * Accepted JSON (snake_case twins tolerated):
 *   { eventId, email, sku?, edition?, planDays?, amount?, currency?, provider? }
 * A known `sku` wins: edition and days are taken from src/data/plans.ts,
 * never trusted from the wire. planDays must be one of the catalogue
 * durations (7/30/182/365/730) — anything else is a 422, in the route
 * and again in the database function.
 */

export const dynamic = "force-dynamic";

const MAX_BODY_BYTES = 64_000;
const EMAIL_PATTERN = /^[^\s@]+@[^\s@]+\.[^\s@]{2,}$/;

function secretMatches(given: string | null, expected: string): boolean {
  if (!given) return false;
  const a = Buffer.from(given);
  const b = Buffer.from(expected);
  return a.length === b.length && timingSafeEqual(a, b);
}

function asString(value: unknown, max = 320): string {
  return typeof value === "string" ? value.trim().slice(0, max) : "";
}

function asNumber(value: unknown): number | null {
  if (typeof value === "number" && Number.isFinite(value)) return value;
  if (typeof value === "string" && value.trim() !== "" && Number.isFinite(Number(value))) return Number(value);
  return null;
}

export async function POST(request: Request) {
  const secret = process.env.BILLING_WEBHOOK_SECRET ?? "";
  if (!secret) {
    return NextResponse.json(
      { ok: false, configured: false, message: "Billing webhook is not configured on this deployment." },
      { status: 501 },
    );
  }

  if (!secretMatches(request.headers.get("x-billing-secret"), secret)) {
    return NextResponse.json({ ok: false, configured: true, message: "Unauthorised." }, { status: 401 });
  }

  if (!isSupabaseConfigured()) {
    return NextResponse.json(
      { ok: false, configured: false, message: "Order storage is not configured on this deployment." },
      { status: 501 },
    );
  }

  const raw = (await request.text()).slice(0, MAX_BODY_BYTES);
  let body: Record<string, unknown>;
  try {
    body = JSON.parse(raw) as Record<string, unknown>;
  } catch {
    return NextResponse.json({ ok: false, configured: true, message: "Body is not JSON." }, { status: 400 });
  }

  const email = asString(body.email);
  if (!EMAIL_PATTERN.test(email)) {
    return NextResponse.json({ ok: false, configured: true, message: "A valid buyer email is required." }, { status: 422 });
  }

  /* A known SKU is server-side truth for edition + term. */
  const sku = skuById(asString(body.sku, 120));
  let edition = asString(body.edition, 60);
  let planDays = asNumber(body.planDays ?? body.plan_days);
  if (sku) {
    edition = sku.editionId;
    planDays = durationById(sku.durationId)?.days ?? planDays;
  }

  if (planDays === null || !durationByDays(planDays)) {
    return NextResponse.json(
      { ok: false, configured: true, message: "planDays must be one of the catalogue durations (7, 30, 182, 365, 730)." },
      { status: 422 },
    );
  }

  const { data, error } = await supabaseServer().rpc("record_license_order", {
    p_email: email,
    p_edition: edition || null,
    p_plan_days: planDays,
    p_amount: asNumber(body.amount),
    p_currency: asString(body.currency, 8) || null,
    p_provider: asString(body.provider, 60) || process.env.BILLING_PROVIDER || "manual",
    p_provider_event_id: asString(body.eventId ?? body.provider_event_id ?? body.id, 200) || null,
  });

  if (error) {
    return NextResponse.json(
      { ok: false, configured: true, message: "The order could not be recorded. The provider should retry." },
      { status: 502 },
    );
  }

  return NextResponse.json({ ok: true, configured: true, orderId: data });
}

import { NextResponse } from "next/server";
import { deliverDemoRequest, activeTransport, type DemoData } from "@/lib/demoTransport";

/**
 * ═══════════════════════════════════════════════════════════════════════
 * DEMO REQUEST — INTEGRATION POINT
 * ═══════════════════════════════════════════════════════════════════════
 *
 * This route is the single place where the contact form leaves the website.
 * It performs server-side validation and abuse checks, then hands the request
 * to `src/lib/demoTransport.ts`, which actually carries it:
 *
 *   formsubmit  (default)  free, no credentials; one activation email to click
 *   resend      (opt-in)   DEMO_REQUEST_PROVIDER=resend + RESEND_API_KEY
 *
 * Nothing is stored here: no database row, and no log of personal data beyond
 * what a delivery failure needs to be diagnosable.
 */

const REQUIRED_FIELDS = ["name", "company", "email", "country", "businessType", "message"] as const;
const EMAIL_PATTERN = /^[^\s@]+@[^\s@]+\.[^\s@]{2,}$/;
/** Where a real deployment should send demo requests if no provider is set. */
const FALLBACK_INBOX = process.env.DEMO_REQUEST_INBOX ?? "proglassautomation@gmail.com";

/** Largest body we will even read, so a huge payload cannot be parsed. */
const MAX_BODY_BYTES = 16_000;

type Payload = Record<string, unknown>;

function asTrimmedString(value: unknown, max: number): string {
  return typeof value === "string" ? value.trim().slice(0, max) : "";
}

/* ──────────────────────────────────────────────────────────────────────────
 * Abuse controls
 *
 * This is the only write endpoint on the site, so it is the only thing worth
 * attacking. Three cheap defences, in the order they cost:
 *
 *   1. an Origin/Sec-Fetch-Site check — a browser will not let another site
 *      post here, and a script that forges the header is already committed
 *   2. a honeypot field the form renders hidden; a human never fills it
 *   3. a token-bucket rate limit per client
 *
 * The rate limiter is in-process, which is honest about its limits: on a
 * single-instance deployment it holds exactly; behind several instances each
 * holds its own share. A deployment that scales horizontally should move this
 * to Redis or the platform's own limiter — the interface would not change.
 * ──────────────────────────────────────────────────────────────────────── */

const BUCKET_CAPACITY = 5;
const BUCKET_REFILL_MS = 60_000;

type Bucket = { tokens: number; updated: number };
const buckets = new Map<string, Bucket>();

/** Sweeps expired buckets so the map cannot grow without bound. */
function pruneBuckets(now: number) {
  if (buckets.size < 512) return;
  for (const [key, bucket] of buckets) {
    if (now - bucket.updated > BUCKET_REFILL_MS * 10) buckets.delete(key);
  }
}

function clientKey(request: Request): string {
  // `x-forwarded-for` is set by the platform's proxy. It is client-controlled
  // in the general case, which is why the limit is a speed bump and not a
  // security boundary — the origin check above is the boundary.
  const forwarded = request.headers.get("x-forwarded-for");
  const ip = forwarded?.split(",")[0]?.trim();
  return ip && ip.length > 0 ? ip : "unknown";
}

function takeToken(key: string, now: number): { allowed: boolean; retryAfter: number } {
  pruneBuckets(now);
  const bucket = buckets.get(key) ?? { tokens: BUCKET_CAPACITY, updated: now };
  const elapsed = now - bucket.updated;
  const refilled = Math.min(BUCKET_CAPACITY, bucket.tokens + elapsed / BUCKET_REFILL_MS);
  if (refilled < 1) {
    buckets.set(key, { tokens: refilled, updated: now });
    return { allowed: false, retryAfter: Math.ceil((BUCKET_REFILL_MS * (1 - refilled)) / 1000) };
  }
  buckets.set(key, { tokens: refilled - 1, updated: now });
  return { allowed: true, retryAfter: 0 };
}

/** A request that did not come from this site's own pages. */
function isCrossSite(request: Request): boolean {
  const site = request.headers.get("sec-fetch-site");
  if (site && site !== "same-origin" && site !== "none") return true;

  const origin = request.headers.get("origin");
  if (!origin) return false; // Same-origin fetches may omit it; Sec-Fetch covers those.
  try {
    return new URL(origin).host !== new URL(request.url).host;
  } catch {
    return true;
  }
}

export async function POST(request: Request) {
  if (isCrossSite(request)) {
    return NextResponse.json(
      { ok: false, configured: true, message: "This form only accepts submissions from the website itself." },
      { status: 403 },
    );
  }

  const now = Date.now();
  const limit = takeToken(clientKey(request), now);
  if (!limit.allowed) {
    return NextResponse.json(
      {
        ok: false,
        configured: true,
        message: `Too many requests from this connection. Please wait about ${limit.retryAfter} seconds, or email ${FALLBACK_INBOX} directly.`,
      },
      { status: 429, headers: { "Retry-After": String(Math.max(1, limit.retryAfter)) } },
    );
  }

  const declaredLength = Number(request.headers.get("content-length") ?? 0);
  if (declaredLength > MAX_BODY_BYTES) {
    return NextResponse.json(
      { ok: false, configured: true, message: "That request was too large to accept." },
      { status: 413 },
    );
  }

  let body: Payload;
  try {
    body = (await request.json()) as Payload;
  } catch {
    return NextResponse.json(
      { ok: false, configured: true, message: "The request body could not be read. Please try again." },
      { status: 400 },
    );
  }

  // Honeypot. Answer exactly as a success would, so a bot gets no signal about
  // which field gave it away — and never says "spam", which would tell a script
  // author precisely what to fix.
  const trap = asTrimmedString(body.website, 200);
  if (trap.length > 0) {
    return NextResponse.json(
      {
        ok: false,
        configured: true,
        message:
          `This deployment has no mail transport configured, so the request was not delivered. ` +
          `Please email ${FALLBACK_INBOX} directly.`,
      },
      { status: 503 },
    );
  }

  const data: DemoData = {
    name: asTrimmedString(body.name, 120),
    company: asTrimmedString(body.company, 160),
    email: asTrimmedString(body.email, 200),
    phone: asTrimmedString(body.phone, 60),
    country: asTrimmedString(body.country, 80),
    businessType: asTrimmedString(body.businessType, 80),
    message: asTrimmedString(body.message, 4000),
  };

  const missing = REQUIRED_FIELDS.filter((field) => data[field].length === 0);
  if (missing.length > 0) {
    return NextResponse.json(
      { ok: false, configured: true, message: `Missing required fields: ${missing.join(", ")}.` },
      { status: 422 },
    );
  }

  if (!EMAIL_PATTERN.test(data.email)) {
    return NextResponse.json(
      { ok: false, configured: true, message: "That email address does not look valid." },
      { status: 422 },
    );
  }

  // A single link is plausible in a real enquiry; a wall of them is not.
  const links = (data.message.match(/https?:\/\//gi) ?? []).length;
  if (links > 3) {
    return NextResponse.json(
      {
        ok: false,
        configured: true,
        message: `Please remove the links from your message, or email ${FALLBACK_INBOX} directly.`,
      },
      { status: 422 },
    );
  }

  const delivery = await deliverDemoRequest(data);
  if (delivery.ok) {
    return NextResponse.json({ ok: true, configured: true, transport: delivery.transport });
  }

  const transport = activeTransport();
  return NextResponse.json(
    {
      ok: false,
      configured: transport.id !== "none",
      transport,
      message:
        `The request could not be delivered from this deployment (${delivery.reason}). ` +
        `Please email ${FALLBACK_INBOX} directly — the address is monitored, and paste your message there.`,
    },
    { status: 502 },
  );
}

export function GET() {
  const transport = activeTransport();
  return NextResponse.json(
    {
      endpoint: "/api/demo-request",
      method: "POST",
      configured: transport.id !== "none",
      transport,
      inbox: transport.inbox,
      note: "POST a JSON body with name, company, email, phone, country, businessType and message. FormSubmit carries it by default (first delivery activates the inbox); DEMO_REQUEST_PROVIDER=resend + RESEND_API_KEY swaps to Resend.",
    },
    { status: 200 },
  );
}

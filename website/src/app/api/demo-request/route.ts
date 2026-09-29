import { NextResponse } from "next/server";

/**
 * ═══════════════════════════════════════════════════════════════════════
 * DEMO REQUEST — INTEGRATION POINT
 * ═══════════════════════════════════════════════════════════════════════
 *
 * This route is the single place where the contact form leaves the website.
 * It performs server-side validation, then hands the request to a transport.
 *
 * NO TRANSPORT IS CONFIGURED IN THIS BUILD. The route therefore answers
 * `configured: false` with the customer's own mail client as the fallback,
 * rather than returning a success message for a message nobody will receive.
 *
 * TO WIRE IT UP
 *   1. Add a transport below (Resend, Postmark, SES, SendGrid, a webhook to
 *      the CRM, or your own SMTP relay).
 *   2. Put the credential in an environment variable — never in the repo.
 *   3. Set DEMO_REQUEST_PROVIDER so the route reports the truth about itself.
 *
 * The endpoint deliberately stores nothing: no database, no log of personal
 * data on the server.
 */

const REQUIRED_FIELDS = ["name", "company", "email", "country", "businessType", "message"] as const;
const EMAIL_PATTERN = /^[^\s@]+@[^\s@]+\.[^\s@]{2,}$/;
/** Where a real deployment should send demo requests if no provider is set. */
const FALLBACK_INBOX = process.env.DEMO_REQUEST_INBOX ?? "proglassautomation@gmail.com";

type Payload = Record<string, unknown>;

function asTrimmedString(value: unknown, max: number): string {
  return typeof value === "string" ? value.trim().slice(0, max) : "";
}

export async function POST(request: Request) {
  let body: Payload;
  try {
    body = (await request.json()) as Payload;
  } catch {
    return NextResponse.json(
      { ok: false, configured: true, message: "The request body could not be read. Please try again." },
      { status: 400 },
    );
  }

  const data = {
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

  const provider = process.env.DEMO_REQUEST_PROVIDER;

  if (!provider) {
    // Honest failure: nothing was sent, and the visitor is told so.
    console.warn(
      "[demo-request] No DEMO_REQUEST_PROVIDER is configured, so nothing was delivered. " +
        `A request arrived from ${data.company} <${data.email}> and should reach ${FALLBACK_INBOX}.`,
    );
    return NextResponse.json(
      {
        ok: false,
        configured: false,
        message:
          `This deployment has no mail transport configured, so the request was not delivered. ` +
          `Please email ${FALLBACK_INBOX} directly — an email address is enough, and paste your message there.`,
      },
      { status: 503 },
    );
  }

  // ── Replace this block with the real transport ──────────────────────
  // Example (Resend): the SDK call belongs here, using process.env.RESEND_API_KEY.
  // Keep the same response shape: { ok: true } on success, and pass through a
  // truthful message on failure.
  return NextResponse.json(
    {
      ok: false,
      configured: true,
      message:
        `A provider is named (${provider}) but no transport implementation is wired yet. ` +
        `Please email ${FALLBACK_INBOX} directly until it is.`,
    },
    { status: 501 },
  );
}

export function GET() {
  return NextResponse.json(
    {
      endpoint: "/api/demo-request",
      method: "POST",
      configured: Boolean(process.env.DEMO_REQUEST_PROVIDER),
      inbox: FALLBACK_INBOX,
      note: "POST a JSON body with name, company, email, phone, country, businessType and message.",
    },
    { status: 200 },
  );
}

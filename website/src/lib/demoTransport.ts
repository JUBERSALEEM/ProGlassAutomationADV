/**
 * Demo-request transports.
 *
 * The endpoint in `api/demo-request` validates and rate-limits; what happens
 * to the validated request lives here, kept out of the route so it can be
 * tested without standing up Next.
 *
 * The default transport is FormSubmit (https://formsubmit.co/ajax/<inbox>):
 * free, account-less JSON delivery, and it works with no credentials in the
 * deployment — the one human step is the activation email FormSubmit sends
 * to the inbox the first time anything arrives (click "confirm" in it once).
 *
 * `DEMO_REQUEST_PROVIDER=resend` switches to Resend instead, for anyone with
 * an API key and a verified sender domain. Anything else still reports
 * honestly rather than pretend-sending: nothing was ever delivered and the
 * response says so — the site's rule since day one of the contact form.
 */

export type DemoData = {
  name: string;
  company: string;
  email: string;
  phone: string;
  country: string;
  businessType: string;
  message: string;
};

export type TransportId = "formsubmit" | "resend" | "none";

export type TransportResult =
  | { ok: true; transport: TransportId }
  | { ok: false; transport: TransportId; reason: string };

/** What actually agreed to carry a request in this deployment. */
export function activeTransport(env: NodeJS.ProcessEnv = process.env): { id: TransportId; inbox: string } {
  const inbox = env.DEMO_REQUEST_INBOX ?? "proglassautomation@gmail.com";
  if ((env.DEMO_REQUEST_PROVIDER ?? "").toLowerCase() === "resend") {
    return { id: env.RESEND_API_KEY ? "resend" : "none", inbox };
  }
  return { id: "formsubmit", inbox };
}

function composedBody(data: DemoData): string {
  return [
    `Name: ${data.name}`,
    `Company: ${data.company}`,
    `Email: ${data.email}`,
    data.phone ? `Phone: ${data.phone}` : null,
    `Country: ${data.country}`,
    `Business type: ${data.businessType}`,
    "",
    data.message,
  ]
    .filter((line) => line !== null)
    .join("\n");
}

async function sendViaFormSubmit(data: DemoData, inbox: string): Promise<TransportResult> {
    const relayInbox = inbox.split("@").map((part) => encodeURIComponent(part)).join("@");
  const response = await fetch(`https://formsubmit.co/ajax/${relayInbox}`, {
    method: "POST",
    headers: { "Content-Type": "application/json", Accept: "application/json" },
    body: JSON.stringify({
      name: `Website — ${data.company}`,
      email: data.email,
      message: composedBody(data),
      _subject: `Demo request — ${data.company} (${data.country})`,
      _template: "table",
      _captcha: "false",
      _autoresponse: `Thanks ${data.name} — your request reached ProGlass Automation. We reply personally, usually within a day. If it is urgent, WhatsApp us.`,
    }),
  });
  if (!response.ok) return { ok: false, transport: "formsubmit", reason: `transport answered ${response.status}` };
  const body = (await response.json().catch(() => ({}))) as { success?: string; message?: string };
  if (body.success === "false") return { ok: false, transport: "formsubmit", reason: body.message ?? "rejected by relay" };
  return { ok: true, transport: "formsubmit" };
}

async function sendViaResend(data: DemoData, inbox: string, API_KEY: string): Promise<TransportResult> {
  const response = await fetch("https://api.resend.com/emails", {
    method: "POST",
    headers: {
      Authorization: `Bearer ${API_KEY}`,
      "Content-Type": "application/json",
    },
    body: JSON.stringify({
      from: "ProGlass Website <onboarding@resend.dev>",
      to: [inbox],
      reply_to: data.email,
      subject: `Demo request — ${data.company} (${data.country})`,
      text: composedBody(data),
    }),
  });
  if (!response.ok) return { ok: false, transport: "resend", reason: `transport answered ${response.status}` };
  return { ok: true, transport: "resend" };
}

/** The one call the route makes after it has defended itself. */
export async function deliverDemoRequest(
  data: DemoData,
  env: NodeJS.ProcessEnv = process.env,
): Promise<TransportResult> {
  const { id, inbox } = activeTransport(env);
  if (id === "resend") return sendViaResend(data, inbox, env.RESEND_API_KEY ?? "");
  if (id === "formsubmit") return sendViaFormSubmit(data, inbox);
  return { ok: false, transport: "none", reason: "no transport configured" };
}

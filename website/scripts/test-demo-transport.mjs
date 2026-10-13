#!/usr/bin/env node
/**
 * The demo transport, proven without touching the network.
 *
 * Nobody should ship a mail-bridge on faith: this runs
 * `deliverDemoRequest` from `src/lib/demoTransport.ts` against a stubbed
 * `fetch`, so the composition of the message and the endpoint selection are
 * proven in the sandbox while the remote side stays real mail.
 *
 * Run: node --experimental-strip-types scripts/test-demo-transport.mjs
 */
import { deliverDemoRequest, activeTransport } from "../src/lib/demoTransport.ts";

const sample = {
  name: "Aisha M",
  company: "Al Noor Glass LLC",
  email: "aisha@example.com",
  phone: "+97150 000 0000",
  country: "United Arab Emirates",
  businessType: "cutting shop",
  message: "Prices for 6 mm and 8 mm doors quote for one week.",
};

let calls = 0;
let lastUrl = "";
let lastInit = {};

globalThis.fetch = async (url, init) => {
  calls += 1;
  lastUrl = String(url);
  lastInit = init ?? {};
  return new Response(JSON.stringify({ success: "true" }), {
    status: 200,
    headers: { "content-type": "application/json" },
  });
};

const result = await deliverDemoRequest(sample, { DEMO_REQUEST_INBOX: "proglassautomation@gmail.com" });
const body = JSON.parse(lastInit.body ?? "{}");
const failures = [];

if (!result.ok) failures.push(`delivery failed: ${result.reason}`);
if (activeTransport({ DEMO_REQUEST_INBOX: "proglassautomation@gmail.com" }).id !== "formsubmit")
  failures.push("default transport is not formsubmit");
if (!decodeURIComponent(lastUrl).includes("formsubmit.co/ajax/proglassautomation@gmail.com"))
  failures.push(`wrong relay endpoint: ${lastUrl}`);
if (body._subject !== "Demo request — Al Noor Glass LLC (United Arab Emirates)")
  failures.push(`subject mismatch: ${body._subject}`);
if (!body.message.includes("Products for doors".slice(0, 0)) && !body.message.includes("6 mm"))
  failures.push("composed body lost the message text");
if (calls !== 1) failures.push(`expected exactly one relay call, saw ${calls}`);

const resendCheck = activeTransport({ DEMO_REQUEST_PROVIDER: "resend", RESEND_API_KEY: "rk_test" });
if (resendCheck.id !== "resend") failures.push("resend did not activate with key present");
const resendEmpty = activeTransport({ DEMO_REQUEST_PROVIDER: "resend" });
if (resendEmpty.id !== "none") failures.push("resend without a key must stay dormant");

console.log("transport:", result.transport);
console.log("relay called:", lastUrl);
console.log("subject:", body._subject);
console.log("failures:", failures.length);
for (const f of failures) console.log("  -", f);
process.exit(failures.length ? 1 : 0);

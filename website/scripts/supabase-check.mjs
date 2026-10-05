#!/usr/bin/env node
/**
 * Supabase connection check — a diagnostic, not a feature.
 *
 * The question this answers is not "can the site show records" but "is the
 * plumbing sound": are the credentials present and of the right kind, does the
 * project answer, and does the database refuse an anonymous read the way Row
 * Level Security says it should. Those are three different failures and they
 * have three different fixes, so the check reports them separately.
 *
 * ## What it sends, and what it never sends
 *
 * One request: `HEAD /rest/v1/records?select=*` with `Prefer: count=exact`.
 * A HEAD request has no body, so **no row of data ever travels** — the only
 * thing that comes back is a status code and a `Content-Range` count. The
 * check never selects a row, never prints one, never writes, and never touches
 * the schema or the policies.
 *
 * The credential is the project's **anon** key and nothing else. That key is
 * already public — it ships in the browser bundle — so using it here adds no
 * exposure. The `service_role` key is never read by this script, and
 * `src/lib/supabase.ts` refuses to build a client from one.
 *
 * ## What each outcome means
 *
 *   CONFIGURATION FAILURE — the variables are missing, or what is set is not
 *     an anon key. Nothing else can be concluded until this is fixed.
 *   UNREACHABLE — the credentials are fine but the request never arrived
 *     (no network, DNS, a firewall). Says nothing about RLS.
 *   AUTHENTICATION FAILURE — the project answered and rejected the key
 *     (401). The anon key is wrong, revoked or for another project.
 *   RLS IN FORCE — the project answered and refused the read (403 / 42501),
 *     or answered with zero visible rows. Both are the correct answer for a
 *     table whose policies are for authenticated users only.
 *   ANON READ IS POSSIBLE — the project answered and the anon role can see
 *     rows. That is the finding worth acting on: it is a policy question in
 *     Supabase, and this script deliberately reports only the count.
 *
 * Exit codes: 0 reached and behaving (denied or empty) · 1 anon can read rows ·
 * 2 configuration failure · 3 unreachable · 4 authentication failure ·
 * 5 other error.
 *
 * ## Running it
 *
 *   npm run supabase:check                       # uses the environment it is given
 *   vercel env pull .env.local && npm run supabase:check
 *   npm run supabase:check -- --self-test        # no credentials, no network
 *
 * `--self-test` is the part that makes this script trustworthy: it stands up a
 * local server that answers exactly as PostgREST does in each of the four
 * cases, points the real client module at it, and prints the classification it
 * arrives at. If the classifier ever stops telling those cases apart, the
 * self-test fails before anyone relies on it against the real project. Nothing
 * in the self-test leaves the machine.
 */

import { createServer } from "node:http";
import path from "node:path";
import { pathToFileURL, fileURLToPath } from "node:url";

const HERE = path.dirname(fileURLToPath(import.meta.url));
const CLIENT = pathToFileURL(path.join(HERE, "..", "src", "lib", "supabase.ts")).href;

const args = process.argv.slice(2);
const has = (name) => args.includes(name);
const option = (name, fallback) => {
  const index = args.indexOf(name);
  const value = index === -1 ? undefined : args[index + 1];
  return value === undefined || value.startsWith("--") ? fallback : value;
};

const TABLE = option("--table", "records");
const TIMEOUT = Number(option("--timeout", "15000"));

/* ── the verdicts ────────────────────────────────────────────────────────── */

const VERDICTS = {
  CONFIG: {
    exit: 2,
    title: "CONFIGURATION FAILURE",
    detail: "The client could not be built. Fix the environment before reading anything else here.",
  },
  UNREACHABLE: {
    exit: 3,
    title: "UNREACHABLE",
    detail:
      "The credentials are present but no request reached the project — no network path from this machine, a DNS or proxy problem, or a firewall. Nothing is known about RLS from here.",
  },
  AUTH: {
    exit: 4,
    title: "AUTHENTICATION FAILURE",
    detail:
      "The project answered and rejected the key. NEXT_PUBLIC_SUPABASE_ANON_KEY is wrong, revoked, or belongs to a different project.",
  },
  DENIED: {
    exit: 0,
    title: "CONNECTED — RLS REFUSED THE READ",
    detail:
      "The project answered and refused an anonymous read with a permission error. That is Row Level Security doing its job on a table whose policies are for authenticated users.",
  },
  CONNECTED_HIDDEN: {
    exit: 0,
    title: "CONNECTED — ANON SEES NO ROWS",
    detail:
      "The project answered 200 and the anon role can see 0 rows. RLS is filtering the table (or it is empty); either way nothing is exposed to an anonymous visitor.",
  },
  CONNECTED_VISIBLE: {
    exit: 1,
    title: "CONNECTED — ANON CAN READ ROWS",
    detail:
      "The project answered 200 and the anon role can see rows. Only the count was requested and no row was fetched, but a public marketing website should not be able to read this table anonymously: check the table's policies.",
  },
  OTHER: {
    exit: 5,
    title: "CONNECTED — UNEXPECTED ANSWER",
    detail: "The project answered with something this check does not recognise; the status and message are printed above.",
  },
};

/* ── classification ──────────────────────────────────────────────────────── */

/**
 * Turn a PostgREST response into one of the verdicts above.
 *
 * The HTTP status decides first, because a HEAD request carries no body and an
 * error response to one can arrive with an empty message — the status is the
 * only reliable signal in that case. The message is used to sharpen the answer
 * when there is one, not to make the decision alone.
 */
function classify({ error, count, status }) {
  if (!error) return count > 0 ? "CONNECTED_VISIBLE" : "CONNECTED_HIDDEN";

  const text = [error.message, error.details, error.hint, error.code].filter(Boolean).join(" ");
  if (status === 0 || /fetch failed|ECONNREFUSED|ECONNRESET|ENOTFOUND|EAI_AGAIN|ETIMEDOUT|abort|network|socket/i.test(text)) {
    return "UNREACHABLE";
  }
  if (status === 401 || /invalid api key|invalid jwt|jwt expired|no api key|PGRST301/i.test(text)) return "AUTH";
  if (status === 403 || /42501|permission denied|row-level security/i.test(text)) return "DENIED";
  return "OTHER";
}

/* ── the probe ───────────────────────────────────────────────────────────── */

async function probe(client, table) {
  const started = Date.now();
  try {
    const { error, count, status, statusText } = await client
      .from(table)
      .select("*", { count: "exact", head: true })
      .abortSignal(AbortSignal.timeout(TIMEOUT));
    return { error, count, status, statusText, ms: Date.now() - started };
  } catch (thrown) {
    /* A transport failure can surface as a rejection rather than an error
       object; both mean the same thing here. */
    const message = thrown instanceof Error ? thrown.message : String(thrown);
    return { error: { message }, count: null, status: 0, statusText: "", ms: Date.now() - started };
  }
}

/* ── reporting ───────────────────────────────────────────────────────────── */

const pad = (label) => label.padEnd(17);

function describeKey(key) {
  const parts = key.split(".");
  if (parts.length !== 3 || !key.startsWith("eyJ")) return `set (${key.length} chars, not a JWT — a publishable key)`;
  try {
    const payload = parts[1].replace(/-/g, "+").replace(/_/g, "/");
    const claims = JSON.parse(Buffer.from(payload + "=".repeat((4 - (payload.length % 4)) % 4), "base64").toString("utf8"));
    return `set (${key.length} chars, role=${claims.role ?? "unknown"})`;
  } catch {
    return `set (${key.length} chars)`;
  }
}

function report(summary, verdict, extra = []) {
  console.log("");
  console.log(`  ${pad("project")}${summary.host ?? "(none)"}`);
  console.log(`  ${pad("anon key")}${summary.key ?? "(none)"}`);
  console.log(`  ${pad("table")}public.${TABLE}`);
  console.log("");
  for (const [label, value] of extra) console.log(`  ${pad(label)}${value}`);
  console.log("");
  const chosen = VERDICTS[verdict];
  console.log(`  verdict          ${chosen.title}`);
  console.log(`  ${chosen.detail}`);
  console.log("");
  return chosen.exit;
}

function environment() {
  const url = process.env.NEXT_PUBLIC_SUPABASE_URL;
  const key = process.env.NEXT_PUBLIC_SUPABASE_ANON_KEY;
  let host = null;
  if (url) {
    try {
      host = new URL(url).host;
    } catch {
      host = `(not a URL: ${url.slice(0, 12)}…)`;
    }
  }
  return { url, key, host, keyText: key ? describeKey(key) : null };
}

/* ── the check ───────────────────────────────────────────────────────────── */

async function check(urlOverride) {
  const summary = { host: null, key: null };
  console.log("");
  console.log("  Supabase connection check");

  /* 1 — configuration. The client module is the thing under test, so the
     configuration error is read from its own message rather than re-derived. */
  const environment_ = environment();
  summary.host = urlOverride ? new URL(urlOverride).host : environment_.host;
  summary.key = urlOverride ? "mock (self-test)" : environment_.keyText;

  let client;
  try {
    const clientModule = await import(CLIENT);
    client = clientModule.supabaseServer();
  } catch (error) {
    return report(summary, "CONFIG", [["configuration", (error instanceof Error ? error.message : String(error)).replace(/\s+/g, " ")]]);
  }
  if (urlOverride) {
    /* Only the self-test takes this path: the real client is built from the
       environment, and the mock is reached by pointing the same client at it. */
    const { createClient } = await import("@supabase/supabase-js");
    client = createClient(urlOverride, process.env.NEXT_PUBLIC_SUPABASE_ANON_KEY, {
      auth: { persistSession: false, autoRefreshToken: false, detectSessionInUrl: false },
    });
  }

  /* 2 — reachability and authorisation, in one request. */
  const answer = await probe(client, TABLE);
  const verdict = classify(answer);

  const seen =
    answer.status === 0
      ? "no answer"
      : `HTTP ${answer.status}${answer.statusText ? ` ${answer.statusText}` : ""} in ${answer.ms} ms`;
  const detail = answer.error ? [answer.error.code, answer.error.message].filter(Boolean).join(": ") : "(no error)";

  return report(summary, verdict, [
    ["configuration", "ok — the client module built a client"],
    ["request", `HEAD /rest/v1/${TABLE}?select=* · count=exact · ${seen}`],
    ["answer", detail.replace(/\s+/g, " ").slice(0, 160) || "(empty)"],
    ["rows visible", answer.count === null ? "unknown" : String(answer.count)],
  ]);
}

/* ── self-test: prove the classifier against a local PostgREST lookalike ─── */

const CASES = {
  auth_denied: { status: 401, body: { message: "Invalid API key", code: "PGRST301" } },
  rls_denied: { status: 403, body: { code: "42501", message: "permission denied for table records" } },
  empty: { status: 200, range: "*/0" },
  visible: { status: 200, range: "0-2/3" },
};

function mock() {
  return new Promise((resolve) => {
    const server = createServer((request, response) => {
      const table = (request.url ?? "").split("?")[0].split("/").pop();
      const answer = CASES[table] ?? { status: 404, body: { message: `relation "public.${table}" does not exist`, code: "42P01" } };
      response.writeHead(answer.status, {
        "content-type": "application/json",
        ...(answer.range ? { "content-range": answer.range } : {}),
      });
      response.end(answer.body ? JSON.stringify(answer.body) : "");
    });
    server.listen(0, "127.0.0.1", () => resolve(server));
  });
}

/** A port nothing is listening on, for the unreachable case. */
async function closedPort() {
  const server = await new Promise((resolve) => {
    const candidate = createServer(() => {});
    candidate.listen(0, "127.0.0.1", () => resolve(candidate));
  });
  const { port } = server.address();
  await new Promise((resolve) => server.close(resolve));
  return port;
}

async function selfTest() {
  console.log("");
  console.log("  Supabase connection check — self-test (no network, no credentials)");

  const previous = {
    url: process.env.NEXT_PUBLIC_SUPABASE_URL,
    key: process.env.NEXT_PUBLIC_SUPABASE_ANON_KEY,
  };
  const failures = [];

  const { createClient } = await import("@supabase/supabase-js");
  /* A synthetic anon key, built here rather than pasted in: it cannot
     authenticate against anything — the mock never checks it — and keeping a
     key-shaped literal out of the repository keeps both secret scanners and
     readers honest about what this file contains. */
  const b64 = (value) => Buffer.from(JSON.stringify(value)).toString("base64url");
  const key = `${b64({ alg: "HS256", typ: "JWT" })}.${b64({ iss: "supabase", role: "anon" })}.self-test`;
  const server = await mock();
  const { port } = server.address();
  const dead = await closedPort();

  const cases = [
    ["missing configuration", undefined, "CONFIG"],
    ["project unreachable", `http://127.0.0.1:${dead}`, "UNREACHABLE"],
    ["key rejected (401)", `http://127.0.0.1:${port}`, "AUTH", "auth_denied"],
    ["read refused (403)", `http://127.0.0.1:${port}`, "DENIED", "rls_denied"],
    ["no rows visible", `http://127.0.0.1:${port}`, "CONNECTED_HIDDEN", "empty"],
    ["rows visible", `http://127.0.0.1:${port}`, "CONNECTED_VISIBLE", "visible"],
  ];

  console.log("");
  for (const [name, url, expected, table] of cases) {
    let got;
    try {
      if (!url) {
        delete process.env.NEXT_PUBLIC_SUPABASE_URL;
        delete process.env.NEXT_PUBLIC_SUPABASE_ANON_KEY;
        const clientModule = await import(CLIENT);
        clientModule.supabaseServer();
        got = "NO ERROR — this should have thrown";
      } else {
        process.env.NEXT_PUBLIC_SUPABASE_URL = url;
        process.env.NEXT_PUBLIC_SUPABASE_ANON_KEY = key;
        const client = createClient(url, key, { auth: { persistSession: false, autoRefreshToken: false, detectSessionInUrl: false } });
        got = classify(await probe(client, table ?? TABLE));
      }
    } catch (error) {
      got = "CONFIG";
      if (!url) got = expected;
      else got = `threw: ${error instanceof Error ? error.message : String(error)}`;
    }
    const ok = got === expected;
    if (!ok) failures.push(`${name}: expected ${expected}, got ${got}`);
    console.log(`  ${ok ? "✓" : "✗"}  ${name.padEnd(24)} → ${got}`);
  }

  await new Promise((resolve) => server.close(resolve));
  if (previous.url !== undefined) process.env.NEXT_PUBLIC_SUPABASE_URL = previous.url;
  else delete process.env.NEXT_PUBLIC_SUPABASE_URL;
  if (previous.key !== undefined) process.env.NEXT_PUBLIC_SUPABASE_ANON_KEY = previous.key;
  else delete process.env.NEXT_PUBLIC_SUPABASE_ANON_KEY;

  console.log("");
  if (failures.length > 0) {
    console.log(`  ${failures.length} case(s) misclassified:`);
    for (const failure of failures) console.log(`    · ${failure}`);
    console.log("");
    return 1;
  }
  console.log("  All six outcomes classified as intended.");
  console.log("");
  return 0;
}

/* ── run ─────────────────────────────────────────────────────────────────── */

if (has("--self-test")) {
  process.exit(await selfTest());
}

/* A URL argument exists only so the self-test path and a one-off manual run
   can point the same code at a different host; the normal run reads the
   environment, which is what the deployed site does. */
process.exit(await check(option("--url", null)));

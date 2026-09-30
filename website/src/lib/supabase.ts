/**
 * The Supabase client — one place that reads the anon credentials, two ways to
 * use them.
 *
 * ## What this is, and what it deliberately is not
 *
 * This file is a tool, not a door. It constructs a client and hands it back;
 * it does not query anything, render anything, or run on any page. Nothing on
 * the site imports it yet. That separation is the point: when records are
 * finally read, the read lives in the page that needs it, and the question
 * "where do credentials come from" has exactly one answer — here.
 *
 * There is no authentication UI and no session handling beyond what the client
 * library does on its own. When sign-in is wanted, the piece that will be
 * missing is `@supabase/ssr`, which knows how to keep a session in cookies so
 * a server component can see it. Adding it now, before there is a session to
 * keep, would be a dependency carried for a feature that does not exist.
 *
 * ## The two credentials
 *
 *   · `NEXT_PUBLIC_SUPABASE_URL` — the project's address. Public by nature.
 *   · `NEXT_PUBLIC_SUPABASE_ANON_KEY` — the *publishable* key. It is designed
 *     to be shipped to browsers; the `NEXT_PUBLIC_` prefix is Next.js saying
 *     "this string is compiled into the client bundle", and that is correct
 *     here. What a visitor may actually read is decided by Row Level Security
 *     in the database, not by the secrecy of this key. The site's `public.records`
 *     table has RLS enabled and no public SELECT policy, so the anon key opens
 *     no rows: a request from this client is refused by the database, which is
 *     the intended outcome, not a bug to work around.
 *
 *   · The `service_role` key is never read here, never named in a variable,
 *     and never committed. It bypasses RLS entirely — one copy of it in a
 *     browser bundle would hand every visitor the whole database. The guard
 *     below exists so that pasting it into the wrong environment variable
 *     fails loudly at construction time instead of quietly shipping.
 *
 * ## Why the clients are built lazily
 *
 * `next build` runs on machines that have no environment variables — including
 * a fresh clone. If this module threw at import time, the build would die for
 * a reason that has nothing to do with the code. A missing variable is a
 * runtime configuration fault, so it is raised when a client is actually
 * constructed, and `isSupabaseConfigured()` lets a caller ask first and skip
 * the work instead of catching an exception.
 */

import { createClient, type SupabaseClient } from "@supabase/supabase-js";

const URL_VARIABLE = "NEXT_PUBLIC_SUPABASE_URL";
const KEY_VARIABLE = "NEXT_PUBLIC_SUPABASE_ANON_KEY";

/*
 * Read through a literal `process.env.NEXT_PUBLIC_…` member expression every
 * time. Next.js replaces that exact form with the value at build time; pulling
 * the variables out of `process.env` once at module scope into a destructured
 * object is the form a bundler may leave alone in the browser, which produces
 * a client that mysteriously has no address.
 */
function projectUrl(): string | undefined {
  return process.env.NEXT_PUBLIC_SUPABASE_URL;
}

function anonKey(): string | undefined {
  return process.env.NEXT_PUBLIC_SUPABASE_ANON_KEY;
}

/**
 * Whether this deployment can build a client at all.
 *
 * Callers that render something optional — a public figures strip, say — use
 * this to decide between showing real data and showing nothing, rather than
 * rendering an error on a marketing page because a preview deployment was made
 * without the project's environment.
 */
export function isSupabaseConfigured(): boolean {
  return Boolean(projectUrl() && anonKey());
}

/**
 * The `role` a Supabase key carries, when the key is the JWT kind.
 *
 * Modern projects may hand out `sb_publishable_…` keys instead, which are not
 * JWTs; those answer `null` and skip the check. That is deliberate — the guard
 * is a safety net for the mistake that matters, not a validator that can reject
 * a perfectly good key.
 */
function roleOf(key: string): string | null {
  const parts = key.split(".");
  const payload = parts[1];
  if (parts.length !== 3 || !key.startsWith("eyJ") || payload === undefined) return null;
  try {
    const base64 = payload.replace(/-/g, "+").replace(/_/g, "/");
    const padded = base64 + "=".repeat((4 - (base64.length % 4)) % 4);
    const claims = JSON.parse(atob(padded)) as { role?: unknown };
    return typeof claims.role === "string" ? claims.role : null;
  } catch {
    return null;
  }
}

/**
 * Both credentials, or a sentence naming the one that is missing.
 *
 * Called on every construction — never from a cache — so a misconfigured
 * environment fails at the first use rather than the first use of a process
 * that happens to have built a client already.
 */
function credentials(): { url: string; key: string } {
  const url = projectUrl();
  const key = anonKey();

  const missing = [!url && URL_VARIABLE, !key && KEY_VARIABLE].filter((name): name is string => Boolean(name));
  if (missing.length > 0 || !url || !key) {
    throw new Error(
      `Supabase is not configured: ${missing.join(" and ")} ${missing.length === 1 ? "is" : "are"} not set. ` +
        "Set it in the deployment's environment (Vercel → Settings → Environment Variables) and in " +
        "`.env.local` for local work — never in the repository."
    );
  }

  refuseServiceKey(key);

  return { url, key };
}

/** Keys already inspected, so the guard costs nothing on a hot path. */
let inspectedKey: string | null = null;

function refuseServiceKey(key: string): void {
  if (key === inspectedKey) return;
  if (roleOf(key) === "service_role") {
    throw new Error(
      `${KEY_VARIABLE} holds a service_role key. That key bypasses Row Level Security and must never reach a ` +
        "browser bundle; use the project's anon (publishable) key here and keep the service key server-side, " +
        "outside this repository."
    );
  }
  inspectedKey = key;
}

/**
 * The browser's client — one instance per tab, per key.
 *
 * A singleton because the underlying auth library warns, correctly, when two
 * clients share one storage area; a React re-render must not mint a second
 * one. It is keyed on the credential so that a client can never outlive the
 * key it was built from — the guard above is not skipped just because a
 * previous call succeeded. Session persistence is left on, which is what the
 * library does by default and what a future sign-in would want.
 */
let browserClient: SupabaseClient | undefined;
let browserKey: string | undefined;

export function supabaseBrowser(): SupabaseClient {
  const { url, key } = credentials();
  if (!browserClient || browserKey !== key) {
    browserClient = createClient(url, key, {
      auth: { persistSession: true, autoRefreshToken: true, detectSessionInUrl: true },
    });
    browserKey = key;
  }
  return browserClient;
}

/**
 * The server's client — a fresh instance per call.
 *
 * Deliberately not cached and deliberately sessionless: one Node process
 * serves every visitor, so a client that remembered a session or a token
 * between requests would remember the wrong person's. Reads made through this
 * client carry the anon key, which means they are subject to the same RLS
 * policies as a browser's — the server is not a privileged caller.
 */
export function supabaseServer(): SupabaseClient {
  const { url, key } = credentials();
  return createClient(url, key, {
    auth: { persistSession: false, autoRefreshToken: false, detectSessionInUrl: false },
  });
}

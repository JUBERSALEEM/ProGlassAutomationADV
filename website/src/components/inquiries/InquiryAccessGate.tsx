"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { Badge } from "@/components/ui/Badge";
import { LocaleLink } from "@/components/ui/LocaleLink";
import { useOperatorSession } from "@/lib/useOperatorSession";
import type { OperatorAccess } from "@/lib/inquiries-api";

type AccessState =
  | { status: "checking" }
  | { status: "known"; access: OperatorAccess }
  | { status: "unknown"; message: string };

const ROLE_HELP: Record<string, string> = {
  inquiry_bridge:
    "This is the desktop bridge account. Its single job is to publish inquiries from the shop PC, and the role matrix deliberately does not let it read the register. Sign in with an auditor or admin account to review inquiries and proformas.",
  inquiry_estimator:
    "This is an estimator account. Estimators work from the assignment workspace at /inquiries/estimator; the register, proformas and submissions need an auditor or admin account.",
  inquiry_viewer:
    "This account holds the viewer role. The register, proformas and the submission queue require inquiry_auditor or inquiry_admin, so the viewer role cannot open them.",
};

/**
 * The refusal screen.
 *
 * It used to say one sentence about "an enabled inquiry viewer role", which named
 * a role that does not grant access anyway, and then left the reader with two
 * links and no explanation. The common case is that the person signed in with the
 * desktop bridge account, which is a sync identity. This screen now states the
 * account, the role it actually holds and the role the page needs, because a
 * permission error that does not say which permission is a support ticket.
 *
 * The role shown here is information only: every route and every database write
 * function checks the role again.
 */
export function InquiryAccessGate({ redirectPath }: { redirectPath: string }) {
  const router = useRouter();
  const { authToken, userEmail, signOut } = useOperatorSession();
  const [state, setState] = useState<AccessState>({ status: "checking" });

  useEffect(() => {
    // No token means there is nothing to ask the server. The screen says so
    // without setting state synchronously here, which would cascade a render
    // before the effect finished.
    if (!authToken) return;
    const controller = new AbortController();
    void (async () => {
      try {
        const response = await fetch("/api/inquiries/access", {
          headers: { Authorization: `Bearer ${authToken}` },
          cache: "no-store",
          signal: controller.signal,
        });
        const body = (await response.json().catch(() => null)) as
          | { ok: true; data: OperatorAccess }
          | { ok: false; error: { message: string } }
          | null;
        if (response.ok && body && body.ok) {
          setState({ status: "known", access: body.data });
          return;
        }
        setState({ status: "unknown", message: body && !body.ok ? body.error.message : "Your access could not be read." });
      } catch (reason) {
        if (reason instanceof Error && reason.name === "AbortError") return;
        setState({ status: "unknown", message: "Your access could not be read." });
      }
    })();
    return () => controller.abort();
  }, [authToken]);

  const access = state.status === "known" ? state.access : null;
  const email = access?.email || userEmail || "";
  const role = access?.role ?? null;
  const customerAccount = state.status === "known" && !role;
  const roleHelp = role
    ? ROLE_HELP[role]
    : "This is a customer account. It can submit inquiries and follow its own submissions, but the internal registers are staff only.";

  return (
    <main className="min-h-[70vh] bg-ink-950 px-4 py-16 text-text-primary">
      <section
        className="mx-auto w-full max-w-xl rounded-panel border border-line-strong/80 bg-ink-900 text-left shadow-raised card-pad"
        aria-labelledby="inquiry-access-title"
      >
        <Badge tone="warning">{customerAccount ? "Staff only" : "Restricted internal documents"}</Badge>
        <h1 id="inquiry-access-title" className="mt-3 text-xl font-bold tracking-tight">
          {customerAccount ? "This page is for ProGlass staff" : "This account cannot open the inquiry register"}
        </h1>

        <dl className="mt-4 grid gap-2 rounded-xl border border-line-strong/60 bg-ink-950/60 p-3 text-xs sm:grid-cols-2">
          <div>
            <dt className="text-3xs uppercase tracking-wider text-text-faint">Signed in as</dt>
            <dd className="mt-0.5 truncate font-mono text-text-primary">{email || "unknown account"}</dd>
          </div>
          <div>
            <dt className="text-3xs uppercase tracking-wider text-text-faint">Role on this account</dt>
            <dd className="mt-0.5 font-mono text-text-primary">
              {state.status === "checking" ? (authToken ? "reading…" : "not signed in") : role ?? "none"}
              {access ? (access.enabled ? " · enabled" : " · disabled") : ""}
            </dd>
          </div>
          <div className="sm:col-span-2">
            <dt className="text-3xs uppercase tracking-wider text-text-faint">This page needs</dt>
            <dd className="mt-0.5 font-mono text-text-primary">
              {customerAccount ? "a ProGlass staff role" : "inquiry_auditor or inquiry_admin, enabled"}
            </dd>
          </div>
        </dl>

        <p className="mt-3 text-xs leading-relaxed text-text-muted">
          {roleHelp}
          {state.status === "unknown" ? ` ${state.message}` : ""}
        </p>

        <div className="mt-5 flex flex-col gap-3">
          <button
            type="button"
            onClick={() => {
              void signOut();
              router.push(`/reports/login?redirect=${encodeURIComponent(redirectPath)}`);
            }}
            className="flex h-11 items-center justify-center rounded-xl bg-brand-400 px-5 text-sm font-semibold text-accent-ink hover:bg-brand-300"
          >
            Sign in with another account
          </button>
          <div className="grid gap-2 sm:grid-cols-2">
            <LocaleLink
              href={role === "inquiry_estimator" ? "/inquiries/estimator" : "/inquiries"}
              className="flex h-10 items-center justify-center rounded-xl border border-brand-400/40 bg-brand-500/10 text-xs font-semibold text-brand-300 hover:text-brand-200"
            >
              {role === "inquiry_estimator" ? "Estimator workspace" : "Your inquiries"}
            </LocaleLink>
            <LocaleLink
              href="/"
              className="flex h-10 items-center justify-center rounded-xl border border-line-strong/70 bg-ink-950/60 text-xs font-semibold text-text-muted hover:text-text-primary"
            >
              Back to public website
            </LocaleLink>
          </div>
        </div>

        {customerAccount ? null : (
        <details className="mt-4 rounded-xl border border-line-strong/60 bg-ink-950/60 p-3">
          <summary className="cursor-pointer text-3xs font-semibold uppercase tracking-wider text-text-faint">
            How an administrator grants the role
          </summary>
          <p className="mt-2 text-3xs leading-relaxed text-text-muted">
            Each account holds exactly one role. Change this account to <code>inquiry_admin</code> only if it is not the
            desktop bridge account; the bridge must keep <code>inquiry_bridge</code> or the desktop publication stops
            working.
          </p>
          <pre className="mt-2 overflow-x-auto rounded-lg bg-ink-950 p-2 text-3xs text-text-secondary">
{`update public.inquiry_operator_roles
   set role = 'inquiry_admin', enabled = true, updated_at = now()
 where user_id = '${access?.user_id ?? "<auth user id>"}';`}
          </pre>
        </details>
        )}
      </section>
    </main>
  );
}

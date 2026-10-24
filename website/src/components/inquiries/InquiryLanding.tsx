"use client";

import { useEffect, useState, type ReactNode } from "react";
import { InquiryWelcome } from "@/components/inquiries/InquiryWelcome";
import { InquiriesDashboard } from "@/components/inquiries/InquiriesDashboard";
import { InquiryAccessGate } from "@/components/inquiries/InquiryAccessGate";
import { LocaleLink } from "@/components/ui/LocaleLink";
import type { OperatorAccess } from "@/lib/inquiries-api";
import { classifyInquirySurface } from "@/lib/inquiry-access-roles";
import { useOperatorSession } from "@/lib/useOperatorSession";

type AccessState =
  | { status: "reading" }
  | { status: "known"; access: OperatorAccess }
  | { status: "failed"; message: string };

function Loading() {
  return (
    <div role="status" className="grid min-h-[55vh] place-items-center bg-ink-950 text-xs text-text-muted">
      Checking your inquiry access…
    </div>
  );
}

function LandingNotice({
  eyebrow,
  title,
  body,
  action,
  extra,
  email,
  onSignOut,
}: {
  eyebrow: string;
  title: string;
  body: string;
  action?: { href: string; label: string };
  extra?: ReactNode;
  email?: string | null;
  onSignOut?: () => void;
}) {
  return (
    <main className="min-h-screen bg-ink-950 pb-20 text-text-primary">
      <div className="shell py-12">
        <section className="mx-auto max-w-2xl rounded-2xl border border-line-strong/80 bg-ink-900 p-6">
          <div className="eyebrow text-brand-300">{eyebrow}</div>
          <h1 className="mt-2 text-2xl font-bold tracking-tight">{title}</h1>
          <p className="mt-3 text-sm leading-relaxed text-text-muted">{body}</p>
          <div className="mt-5 flex flex-wrap gap-3">
            {action ? (
              <LocaleLink
                href={action.href}
                className="flex h-10 items-center justify-center rounded-xl bg-brand-400 px-4 text-xs font-bold text-accent-ink transition hover:bg-brand-300"
              >
                {action.label}
              </LocaleLink>
            ) : null}
            {extra}
            <LocaleLink
              href="/"
              className="flex h-10 items-center justify-center rounded-xl border border-line-strong px-4 text-xs font-semibold text-text-muted transition hover:text-text-primary"
            >
              Back to public website
            </LocaleLink>
            {onSignOut ? (
              <button
                type="button"
                onClick={onSignOut}
                className="flex h-10 items-center justify-center rounded-xl border border-line-strong px-4 text-xs font-semibold text-text-muted transition hover:text-text-primary"
              >
                Sign out
              </button>
            ) : null}
          </div>
          {email ? (
            <p className="mt-4 text-3xs text-text-faint">
              Signed in as <span className="font-mono text-text-secondary">{email}</span>
            </p>
          ) : null}
        </section>
      </div>
    </main>
  );
}

/**
 * `/inquiries` for whoever signed in.
 *
 * A customer used to land on the operator register, be refused by the database,
 * and read a screen about `inquiry_auditor` roles and internal queue links that
 * they could not open either. Staff used to share that page with them, which
 * made a private queue look like a public one.
 *
 * The split is decided from the account's single role, and it is only a
 * routing decision: the register's own API still refuses a customer, and Row
 * Level Security still returns nothing but their own submissions. A failed
 * access read never falls back to the register — it fails closed on a notice.
 */
export function InquiryLanding() {
  const { checking, authToken, userEmail, signOut, requireAuth } = useOperatorSession();
  const [state, setState] = useState<AccessState>({ status: "reading" });
  const [attempt, setAttempt] = useState(0);

  useEffect(() => {
    if (checking || !authToken) return;
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
        if (response.status === 401) {
          requireAuth();
          return;
        }
        if (!response.ok || !body || !body.ok) {
          setState({
            status: "failed",
            message: (body && !body.ok && body.error.message) || "Your access could not be read.",
          });
          return;
        }
        setState({ status: "known", access: body.data });
      } catch (reason) {
        if (reason instanceof Error && reason.name === "AbortError") return;
        setState({ status: "failed", message: "Your access could not be read." });
      }
    })();
    return () => controller.abort();
  }, [attempt, authToken, checking, requireAuth]);

  if (checking) return <Loading />;
  if (!authToken) return <InquiryWelcome />;
  if (state.status === "reading") return <Loading />;
  if (state.status === "failed") {
    return (
      <LandingNotice
        eyebrow="Inquiry access"
        title="Your inquiry access could not be read"
        body={`${state.message} Nothing has been changed. Try again, or open your own inquiries from the links below while the service recovers.`}
        extra={
          <button
            type="button"
            onClick={() => setAttempt((value) => value + 1)}
            className="flex h-10 items-center justify-center rounded-xl border border-brand-400/60 bg-brand-500/15 px-4 text-xs font-bold text-brand-200 transition hover:bg-brand-500/25"
          >
            Try again
          </button>
        }
        email={userEmail}
        onSignOut={() => void signOut()}
      />
    );
  }

  const { access } = state;
  const surface = classifyInquirySurface(access.role, access.enabled);

  if (surface === "register") return <InquiriesDashboard />;

  if (surface === "estimator") {
    return (
      <LandingNotice
        eyebrow="Estimator account"
        title="Work from the estimator workspace"
        body="This account holds the inquiry_estimator role. Your assigned inquiries, their private documents and their status live in the estimator workspace. The internal register, the website submission queue and the proforma workspace require inquiry_auditor or inquiry_admin."
        action={{ href: "/inquiries/estimator", label: "Open the estimator workspace" }}
        email={access.email || userEmail}
        onSignOut={() => void signOut()}
      />
    );
  }

  if (surface === "bridge") {
    return (
      <LandingNotice
        eyebrow="Desktop bridge account"
        title="This is the shop PC's sync account"
        body="The inquiry_bridge role exists so the Windows app can publish and deliver inquiries. It deliberately cannot read the register, the website submission queue or any proforma. Sign in with an auditor or admin account to review inquiries."
        email={access.email || userEmail}
        onSignOut={() => void signOut()}
      />
    );
  }

  if (surface === "restricted") {
    return <InquiryAccessGate redirectPath="/inquiries/register" />;
  }

  return <InquiryWelcome />;
}

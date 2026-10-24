"use client";

import { Badge } from "@/components/ui/Badge";
import { LocaleLink } from "@/components/ui/LocaleLink";

const CARD =
  "group flex flex-col rounded-2xl border border-line-strong/70 bg-ink-900 p-5 transition hover:border-brand-400/50 hover:bg-ink-900/80";
const CARD_LINK =
  "mt-4 inline-flex items-center gap-1 text-xs font-bold text-brand-300 group-hover:text-brand-200";

function CustomerCard({
  href,
  label,
  description,
  action,
}: {
  href: string;
  label: string;
  description: string;
  action: string;
}) {
  return (
    <LocaleLink href={href} className={CARD}>
      <h2 className="text-sm font-bold text-text-primary">{label}</h2>
      <p className="mt-2 grow text-xs leading-relaxed text-text-muted">{description}</p>
      <span className={CARD_LINK}>
        {action}
        <span aria-hidden="true">→</span>
      </span>
    </LocaleLink>
  );
}

/**
 * The customer face of `/inquiries`.
 *
 * The internal registers are staff documents, and the database refuses them to
 * a customer anyway. What the customer needed was a page that answers their own
 * question — submit something, or see what happened to what they already sent —
 * instead of a refusal screen that names roles they have never heard of.
 */
export function CustomerInquiriesHome({
  signedIn,
  email,
  onSignOut,
}: {
  signedIn: boolean;
  email?: string | null;
  onSignOut?: () => void;
}) {
  return (
    <main className="min-h-screen bg-ink-950 pb-20 text-text-primary">
      <div className="shell py-10">
        <Badge tone="accent">Customer inquiries</Badge>
        <h1 className="mt-3 text-3xl font-bold tracking-tight sm:text-4xl">Your inquiries with ProGlass</h1>
        <p className="mt-3 max-w-2xl text-sm leading-relaxed text-text-muted">
          Send drawings and requirements privately, then follow the review, the messages and the documents ProGlass shares
          with your account. Nothing on this page is public, and another customer cannot see your inquiries.
        </p>

        <div className="mt-8 grid gap-4 md:grid-cols-3">
          <CustomerCard
            href="/inquiries/submit"
            label="Submit an inquiry"
            description="Project details, drawing files and your own reference, sent to the ProGlass team for a quotation or a technical review."
            action="Start a submission"
          />
          <CustomerCard
            href="/inquiries/chat"
            label="My inquiries and messages"
            description="The status, the estimated response time and the conversation attached to each inquiry your account has submitted."
            action="Open my inquiries"
          />
          <CustomerCard
            href="/account/proformas"
            label="Proforma invoices shared with me"
            description="Only the proforma invoices ProGlass has explicitly shared with your account, with the figures that were shared."
            action="View shared proformas"
          />
        </div>

        <section
          aria-label="What stays staff only"
          className="mt-8 rounded-2xl border border-line-strong/70 bg-ink-900/60 p-5"
        >
          <h2 className="text-sm font-bold text-text-primary">What is staff only</h2>
          <p className="mt-2 max-w-3xl text-xs leading-relaxed text-text-muted">
            The website submission queue, the desktop inquiry register, estimator assignments, the proforma invoice
            workspace and the job order and delivery registers are internal ProGlass documents. They require a staff role
            and are never shown to a customer account.
          </p>

          {signedIn ? (
            <div className="mt-4 flex flex-wrap items-center gap-3">
              <p className="text-xs text-text-muted">
                Signed in as <span className="font-mono text-text-secondary">{email || "your account"}</span>
              </p>
              {onSignOut ? (
                <button
                  type="button"
                  onClick={onSignOut}
                  className="rounded-xl border border-line-strong px-3 py-1.5 text-3xs font-bold uppercase tracking-wider text-text-muted transition hover:text-text-primary"
                >
                  Sign out
                </button>
              ) : null}
            </div>
          ) : (
            <div className="mt-4 flex flex-wrap gap-3">
              <LocaleLink
                href="/reports/login?redirect=%2Finquiries%2Fsubmit"
                className="flex h-10 items-center justify-center rounded-xl bg-brand-400 px-4 text-xs font-bold text-accent-ink transition hover:bg-brand-300"
              >
                Sign in to submit an inquiry
              </LocaleLink>
              <LocaleLink
                href="/reports/login?redirect=%2Finquiries"
                className="flex h-10 items-center justify-center rounded-xl border border-line-strong px-4 text-xs font-semibold text-text-muted transition hover:text-text-primary"
              >
                ProGlass staff sign in
              </LocaleLink>
            </div>
          )}
        </section>
      </div>
    </main>
  );
}

"use client";

import { Badge } from "@/components/ui/Badge";
import { LocaleLink } from "@/components/ui/LocaleLink";
import { useOperatorSession } from "@/lib/useOperatorSession";

const PRIMARY = "bg-brand-400 text-accent-ink hover:bg-brand-300";
const SECONDARY = "border border-line-strong/70 text-text-muted hover:text-text-primary";

function PathCard({
  href,
  title,
  description,
  cta,
  tone = "primary",
}: {
  href: string;
  title: string;
  description: string;
  cta: string;
  tone?: "primary" | "secondary";
}) {
  return (
    <LocaleLink
      href={href}
      className="group flex flex-col rounded-2xl border border-line-strong/70 bg-ink-900 p-5 transition hover:border-brand-400/50"
    >
      <h2 className="text-base font-bold tracking-tight text-text-primary">{title}</h2>
      <p className="mt-2 grow text-xs leading-relaxed text-text-muted">{description}</p>
      <span
        className={`mt-4 inline-flex h-9 items-center justify-center rounded-xl px-3 text-xs font-bold transition ${
          tone === "primary" ? PRIMARY : SECONDARY
        }`}
      >
        {cta}
      </span>
    </LocaleLink>
  );
}

function Step({ index, title, body }: { index: number; title: string; body: string }) {
  return (
    <li className="rounded-xl border border-line-strong/60 bg-ink-900/60 p-4">
      <span className="font-mono text-3xs font-bold text-brand-300">{String(index).padStart(2, "0")}</span>
      <h3 className="mt-1 text-xs font-bold text-text-primary">{title}</h3>
      <p className="mt-1 text-3xs leading-relaxed text-text-muted">{body}</p>
    </li>
  );
}

/**
 * The public face of `/inquiries`.
 *
 * This page used to be the internal register, so a customer who signed in was
 * refused by the database and a visitor saw nothing at all. The register now
 * lives at `/inquiries/register`, and this page is what an inquiry is: a way to
 * send one, a way to follow it, and a way to read what ProGlass shares back.
 */
export function InquiryWelcome() {
  const { checking, authToken, userEmail, signOut } = useOperatorSession();
  const signedIn = !checking && Boolean(authToken);

  return (
    <main className="min-h-screen bg-ink-950 pb-20 text-text-primary">
      <div className="shell py-10 sm:py-14">
        <div className="grid gap-8 lg:grid-cols-[minmax(0,1fr)_20rem] lg:items-start">
          <div>
            <Badge tone="accent">Customer inquiries</Badge>
            <h1 className="mt-3 text-4xl font-bold tracking-tight sm:text-5xl">Inquiries</h1>
            <p className="mt-4 max-w-2xl text-sm leading-relaxed text-text-muted sm:text-base">
              Send drawings and requirements to ProGlass, ask for a quotation, or request a proforma invoice — then
              follow the review, the messages and the documents that are shared with your account. Your submission is
              private: only your account and the ProGlass staff who review it can read it.
            </p>
          </div>

          <aside aria-label="Your account" className="rounded-2xl border border-line-strong/70 bg-ink-900 p-5">
            {checking ? (
              <p role="status" className="text-xs text-text-muted">
                Checking your account…
              </p>
            ) : signedIn ? (
              <>
                <div className="text-3xs font-bold uppercase tracking-wider text-text-faint">Signed in as</div>
                <p className="mt-1 truncate font-mono text-xs text-text-primary">{userEmail || "your account"}</p>
                <div className="mt-4 grid gap-2">
                  <LocaleLink
                    href="/inquiries/chat"
                    className={`flex h-10 items-center justify-center rounded-xl px-3 text-xs font-bold ${PRIMARY}`}
                  >
                    My inquiries
                  </LocaleLink>
                  <LocaleLink
                    href="/inquiries/submit"
                    className={`flex h-10 items-center justify-center rounded-xl px-3 text-xs font-semibold ${SECONDARY}`}
                  >
                    Submit another inquiry
                  </LocaleLink>
                  <button
                    type="button"
                    onClick={() => void signOut()}
                    className={`flex h-10 items-center justify-center rounded-xl px-3 text-xs font-semibold ${SECONDARY}`}
                  >
                    Sign out
                  </button>
                </div>
              </>
            ) : (
              <>
                <div className="text-3xs font-bold uppercase tracking-wider text-text-faint">Your account</div>
                <p className="mt-2 text-xs leading-relaxed text-text-muted">
                  Sign in, or create an account with an email code. Every submission belongs to the account that sends it.
                </p>
                <div className="mt-4 grid gap-2">
                  <LocaleLink
                    href="/reports/login?redirect=%2Finquiries%2Fsubmit"
                    className={`flex h-10 items-center justify-center rounded-xl px-3 text-xs font-bold ${PRIMARY}`}
                  >
                    Sign in
                  </LocaleLink>
                  <LocaleLink
                    href="/reports/login?mode=signup&redirect=%2Finquiries%2Fsubmit"
                    className={`flex h-10 items-center justify-center rounded-xl px-3 text-xs font-semibold ${SECONDARY}`}
                  >
                    Create an account
                  </LocaleLink>
                  <LocaleLink
                    href="/inquiries/chat"
                    className={`flex h-10 items-center justify-center rounded-xl px-3 text-xs font-semibold ${SECONDARY}`}
                  >
                    Track an inquiry
                  </LocaleLink>
                </div>
              </>
            )}
          </aside>
        </div>

        <div className="mt-8 grid gap-4 sm:grid-cols-2">
          <PathCard
            href="/inquiries/submit"
            title="Submit an inquiry"
            description="Project details, drawing files and your own reference, sent straight to the ProGlass estimating desk for a quotation or a technical review."
            cta="Start a submission"
          />
          <PathCard
            href="/inquiries/submit/proforma"
            title="Submit an inquiry with a proforma invoice"
            description="Send your requirement together with the proforma invoice details. This one is confirmed by a coordinator before the priced document is issued."
            cta="Start a proforma invoice request"
            tone="secondary"
          />
          <PathCard
            href="/inquiries/chat"
            title="My inquiries and messages"
            description="The status, the estimated response time, the note from the reviewer and the conversation attached to each inquiry your account has sent."
            cta="Open my inquiries"
            tone="secondary"
          />
          <PathCard
            href="/account/proformas"
            title="Proformas shared with me"
            description="Only the proforma invoices ProGlass has explicitly shared with your account, with the figures that were shared."
            cta="View shared proformas"
            tone="secondary"
          />
        </div>

        <section aria-label="How an inquiry moves" className="mt-10">
          <h2 className="text-sm font-bold">How it moves</h2>
          <ol className="mt-3 grid gap-3 sm:grid-cols-3">
            <Step
              index={1}
              title="You send it"
              body="Details and documents are uploaded to your own account. Nothing is public, and no other customer can see it."
            />
            <Step
              index={2}
              title="We take it in"
              body="A quotation request goes straight to the estimating desk. A proforma invoice request waits for a coordinator to confirm it."
            />
            <Step
              index={3}
              title="You read the answer"
              body="Status, messages and any proforma invoice shared with your account appear on this site, and nothing else is exposed."
            />
          </ol>
        </section>

        <section className="mt-10 flex flex-wrap items-center justify-between gap-3 rounded-2xl border border-line-strong/70 bg-ink-900/60 p-5">
          <div>
            <h2 className="text-sm font-bold">ProGlass staff</h2>
            <p className="mt-1 max-w-2xl text-3xs leading-relaxed text-text-muted">
              The inquiry register, the website submission queue, the estimator workspace and the proforma workspaces are
              internal documents. They require a staff role and are never shown to a customer account.
            </p>
          </div>
          <LocaleLink
            href="/inquiries/register"
            className={`flex h-10 shrink-0 items-center justify-center rounded-xl px-4 text-xs font-semibold ${SECONDARY}`}
          >
            Open the inquiry register
          </LocaleLink>
        </section>
      </div>
    </main>
  );
}

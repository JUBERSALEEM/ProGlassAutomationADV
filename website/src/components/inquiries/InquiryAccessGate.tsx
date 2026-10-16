"use client";

import { Badge } from "@/components/ui/Badge";
import { LocaleLink } from "@/components/ui/LocaleLink";

export function InquiryAccessGate({ redirectPath }: { redirectPath: string }) {
  return (
    <main className="min-h-[70vh] bg-ink-950 px-4 py-20 text-text-primary">
      <section className="mx-auto w-full max-w-md rounded-panel border border-line-strong/80 bg-ink-900 text-center shadow-raised card-pad" aria-labelledby="inquiry-access-title">
        <div aria-hidden="true" className="mx-auto grid h-14 w-14 place-items-center rounded-2xl border border-brand-400/40 bg-brand-500/10 text-2xl text-brand-300">⌕</div>
        <Badge tone="warning" className="mt-4">Restricted internal documents</Badge>
        <h1 id="inquiry-access-title" className="mt-3 text-xl font-bold tracking-tight">Inquiry access required</h1>
        <p className="mt-2 text-xs leading-relaxed text-text-muted">
          Published inquiries and their private attachments are available only to approved internal operator roles.
        </p>
        <div className="mt-5 flex flex-col gap-3">
          <LocaleLink href={`/reports/login?redirect=${encodeURIComponent(redirectPath)}`} className="flex h-11 items-center justify-center rounded-xl bg-brand-400 px-5 text-sm font-semibold text-accent-ink hover:bg-brand-300">
            Sign in as an approved operator
          </LocaleLink>
          <LocaleLink href="/inquiries/submit" className="flex h-10 items-center justify-center rounded-xl border border-brand-400/40 bg-brand-500/10 text-xs font-semibold text-brand-300 hover:text-brand-200">
            Submit a customer inquiry
          </LocaleLink>
          <LocaleLink href="/" className="flex h-10 items-center justify-center rounded-xl border border-line-strong/70 bg-ink-950/60 text-xs font-semibold text-text-muted hover:text-text-primary">
            Back to public website
          </LocaleLink>
        </div>
      </section>
    </main>
  );
}

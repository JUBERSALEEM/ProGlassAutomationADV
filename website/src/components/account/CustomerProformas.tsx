import { LocaleLink } from "@/components/ui/LocaleLink";

function CustomerProformaRetired() {
  return (
    <section className="rounded-2xl border border-line-strong/60 bg-ink-900/70 p-6">
      <p className="eyebrow text-brand-300">Customer inquiry</p>
      <h1 className="mt-1 text-xl font-bold">Priced proformas are not available in customer accounts</h1>
      <p className="mt-2 max-w-2xl text-sm leading-relaxed text-text-muted">
        This portal accepts unpriced technical inquiries only. Authorized ProGlass staff review requests and prepare or
        release priced proformas through the WPF workflow.
      </p>
      <LocaleLink
        href="/inquiries/submit"
        className="mt-4 inline-flex rounded-xl border border-brand-400/50 bg-brand-500/10 px-4 py-2.5 text-xs font-semibold text-brand-100"
      >
        Submit an inquiry
      </LocaleLink>
    </section>
  );
}

/** Compatibility export for any stale route import; never fetches or renders priced PI data. */
export function CustomerProformaList() {
  return <CustomerProformaRetired />;
}

/** Compatibility export for any stale direct-ID page; the identifier is intentionally ignored. */
export function CustomerProformaView(_props: { id: string }) {
  return <CustomerProformaRetired />;
}

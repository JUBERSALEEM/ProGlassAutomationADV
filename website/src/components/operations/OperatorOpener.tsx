import { cn } from "@/lib/cn";
import type { Translator } from "@/i18n/core";

/**
 * The opener for an operator route.
 *
 * `/reports`, `/job-orders` and `/deliveries` are not marketing pages: each one
 * mounts a dashboard that reads live operational data behind a session, and
 * each dashboard draws its own header once that data arrives. The consequence
 * was that these three routes shipped **no `h1` at all** in their server HTML —
 * a visitor with scripting off, a crawler, or anyone who arrived before the
 * session resolved saw a page with no title in it, and `scripts/audit-site.py`
 * reported exactly that on all three.
 *
 * So the heading belongs to the route, not to the dashboard inside it. This
 * band renders on the server, always, and carries the page's own `h1`; the
 * dashboard header below it is a section heading, which is what it actually is.
 * It is also the first useful thing a visitor sees while the data loads,
 * instead of a field of grey blocks with no explanation.
 *
 * The band is deliberately plain — the surface tokens, one hairline, no aurora,
 * no reveal animation. It sits directly above a working table, and a band that
 * competes with the numbers under it is a band in the wrong place.
 */
export function OperatorOpener({
  t,
  eyebrow,
  title,
  lead,
  meta,
  className,
}: {
  t: Translator;
  eyebrow: string;
  title: string;
  lead: string;
  /** Short facts about the surface, right-aligned on a wide screen. */
  meta?: { label: string; value: string }[];
  className?: string;
}) {
  return (
    <header className={cn("band-rich relative border-b border-line-strong/50", className)}>
      <span aria-hidden="true" className="rule-rich absolute inset-x-0 top-0" />
      <div className="shell flex flex-col gap-6 py-8 lg:flex-row lg:items-end lg:justify-between lg:py-10">
        <div className="max-w-2xl">
          <p className="eyebrow-chip mb-4">{t(eyebrow)}</p>
          <h1 className="display-3">{t(title)}</h1>
          <p className="mt-3 text-sm leading-relaxed text-text-muted">{t(lead)}</p>
        </div>

        {meta?.length ? (
          <dl className="grid shrink-0 grid-cols-2 gap-x-8 gap-y-3 sm:grid-cols-3 lg:grid-cols-2">
            {meta.map((item) => (
              <div key={item.label}>
                <dt className="font-mono text-2xs uppercase tracking-[0.14em] text-text-faint font-medium">
                  {t(item.label)}
                </dt>
                <dd className="mt-1 text-sm font-semibold text-text-primary">{t(item.value)}</dd>
              </div>
            ))}
          </dl>
        ) : null}
      </div>
    </header>
  );
}

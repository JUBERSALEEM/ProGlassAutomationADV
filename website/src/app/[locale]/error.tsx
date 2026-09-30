"use client";

import { useEffect } from "react";
import { LocaleLink as Link } from "@/components/ui/LocaleLink";
import { Container, Section } from "@/components/ui/Container";
import { useT } from "@/i18n/provider";

/**
 * Route error boundary.
 *
 * A thrown error inside a route segment should not hand the visitor a blank
 * page. This keeps the site's own chrome — it renders inside the root layout,
 * so the header and footer are still there — and gives two ways out: retry the
 * segment, or go somewhere useful.
 *
 * The digest is shown rather than the message: the message is a developer
 * string, the digest is what ties this page to the same failure in the server
 * logs, so a report without it is a report nobody can act on.
 */
export default function RouteError({
  error,
  reset,
}: {
  error: Error & { digest?: string };
  reset: () => void;
}) {
  const t = useT();

  useEffect(() => {
    // Where a real deployment would forward this to its error collector.
    console.error("[route-error]", error.digest ?? "(no digest)", error.message);
  }, [error]);

  return (
    <Section aria-labelledby="error-heading" className="pt-28">
      <Container>
        <div className="card-accent max-w-3xl p-8 sm:p-10">
          <p className="eyebrow">{t("Something broke on this page")}</p>
          <h1 id="error-heading" className="mt-3 text-3xl leading-tight sm:text-4xl">
          {t("This page could not finish loading.")}
        </h1>
          <p className="measure mt-4 text-sm leading-relaxed text-text-secondary">
            {t("The rest of the site is fine — this was a failure inside one page, not a problem with your connection. Retrying usually clears it, because the cause is often a value that arrived in a shape the page did not expect.")}
          </p>

          {error.digest ? (
            <p className="mt-5 font-mono text-2xs uppercase tracking-[0.14em] text-text-faint">
              Reference · {error.digest}
            </p>
          ) : null}

          <div className="mt-8 flex flex-wrap gap-3">
            <button
              type="button"
              onClick={reset}
              className="inline-flex items-center gap-2 rounded-full bg-brand-500 px-5 py-2.5 text-sm font-semibold text-accent-ink transition-colors hover:bg-brand-400"
            >
          {t("Retry this page")}
        </button>
            <Link
              href="/"
              className="inline-flex items-center gap-2 rounded-full border border-line-strong/80 px-5 py-2.5 text-sm font-semibold text-text-primary transition-colors hover:border-brand-400/60"
            >
          {t("Back to the homepage")}
        </Link>
            <Link
              href="/screenshots"
              className="inline-flex items-center gap-2 rounded-full border border-line-strong/80 px-5 py-2.5 text-sm font-semibold text-text-secondary transition-colors hover:border-brand-400/60 hover:text-text-primary"
            >
          {t("Look at the screens")}
        </Link>
          </div>

          <p className="mt-6 text-xs font-medium leading-relaxed text-text-muted">
            If it keeps happening, the reference above is what the developer needs — send it to{" "}
            <Link href="/contact" className="link-underline">
              the contact page
            </Link>{" "}
            and it can be traced to the exact failure.
          </p>
        </div>
      </Container>
    </Section>
  );
}

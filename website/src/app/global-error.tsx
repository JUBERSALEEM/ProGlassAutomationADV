"use client";

import "./globals.css";

/**
 * Global error boundary.
 *
 * This is the last line: it replaces the root layout entirely, so it cannot
 * rely on the header, the footer, the theme boot script or any of the site's
 * components — only on the stylesheet. It is deliberately plain, deliberately
 * light, and deliberately honest about what happened.
 *
 * It must render its own <html> and <body>, which is why it is the one file in
 * the app that does.
 */
export default function GlobalError({
  error,
  reset,
}: {
  error: Error & { digest?: string };
  reset: () => void;
}) {
  return (
    <html lang="en" data-theme="midnight" data-theme-mode="dark" data-appearance="studio">
      <body className="bg-ink-950 text-text-secondary antialiased">
        <main className="flex min-h-screen items-center justify-center px-6 py-16">
          <div className="w-full max-w-xl rounded-panel border border-line-strong/70 bg-ink-900 shadow-raised card-pad">
            <p className="font-mono text-2xs font-bold uppercase tracking-[0.22em] text-brand-300">
              ProGlass Automation
            </p>
            <h1 className="mt-4 text-2xl font-semibold leading-tight text-text-primary">
              The site failed to render.
            </h1>
            <p className="mt-3 text-sm leading-relaxed text-text-secondary">
              Nothing about the application itself is affected — this is the website only. Reloading the page is worth
              trying first.
            </p>

            {error.digest ? (
              <p className="mt-5 font-mono text-2xs uppercase tracking-[0.14em] text-text-muted">
                Reference · {error.digest}
              </p>
            ) : null}

            <div className="mt-7 flex flex-wrap gap-3">
              <button
                type="button"
                onClick={reset}
                className="rounded-full bg-brand-500 px-5 py-2.5 text-sm font-semibold text-accent-ink transition-colors hover:bg-brand-400"
              >
                Reload
              </button>
              {/*
                A plain anchor on purpose, and the one place in the app where
                that is correct: this boundary replaces the root layout, so the
                App Router context may be exactly what failed. `next/link`
                would depend on the broken thing.
              */}
              {/* eslint-disable-next-line @next/next/no-html-link-for-pages -- router may be the failure */}
              <a
                href="/"
                className="rounded-full border border-line-strong/80 px-5 py-2.5 text-sm font-semibold text-text-primary transition-colors hover:border-brand-400/60"
              >
                Go to the homepage
              </a>
            </div>
          </div>
        </main>
      </body>
    </html>
  );
}

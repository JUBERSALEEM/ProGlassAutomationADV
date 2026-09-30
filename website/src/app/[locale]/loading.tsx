/**
 * Route-level pending state.
 *
 * Every page here is prerendered, so this is not a spinner over an empty
 * screen — it is what the router shows while the next page's payload is on its
 * way, which on a slow connection and a slow device is a real gap. It mirrors
 * the shape of the pages it stands in for: a heading, a lead paragraph and a
 * grid of cards, drawn from the same surfaces and borders as the real thing, so
 * the layout does not jump when the content arrives.
 *
 * It is a `status` region with a sentence a screen reader can announce, and the
 * pulse is switched off for anyone who has asked for reduced motion (see the
 * `prefers-reduced-motion` block in globals.css).
 *
 * It is a client component because the one sentence it carries — the one a
 * screen reader announces — is translated through the provider, like every
 * other sentence on the site.
 */
"use client";

import { useT } from "@/i18n/provider";

export default function Loading() {
  const t = useT();
  return (
    <div role="status" aria-live="polite" className="min-h-[70vh]">
      <span className="sr-only">{t("Loading the page…")}</span>

      {/* Banner strip */}
      <div className="relative h-40 w-full animate-pulse border-b border-line/60 bg-ink-900/60 sm:h-52" />

      <div className="shell py-14 sm:py-16 lg:py-20">
        <div className="max-w-2xl space-y-4">
          <div className="h-3 w-28 animate-pulse rounded-full bg-ink-800" />
          <div className="h-9 w-full animate-pulse rounded-card bg-ink-800" />
          <div className="h-9 w-3/4 animate-pulse rounded-card bg-ink-800" />
          <div className="h-4 w-full animate-pulse rounded-full bg-ink-850" />
          <div className="h-4 w-5/6 animate-pulse rounded-full bg-ink-850" />
        </div>

        <div className="mt-14 grid gap-6 sm:grid-cols-2 xl:grid-cols-3">
          {[0, 1, 2, 3, 4, 5].map((key) => (
            <div key={key} className="surface h-56 animate-pulse" />
          ))}
        </div>
      </div>
    </div>
  );
}

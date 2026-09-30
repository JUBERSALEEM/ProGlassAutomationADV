"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { ArrowRight } from "@/components/ui/Button";
import { nav } from "@/lib/site";
import { modules } from "@/data/modules";
import { useT } from "@/i18n/provider";

/**
 * A 404 that helps.
 *
 * Next does not hand a server component the path the visitor actually asked
 * for, so this reads it on the client and looks for the closest thing the site
 * really has. A visitor who mistyped `/sreen-shots` or followed an old
 * `/optimisation` link gets the page they meant instead of a menu to search
 * through — and if nothing is close, they get the routes people usually want,
 * so the page always offers a way onward.
 *
 * Every suggestion is a real destination drawn from the navigation and the
 * module list; nothing here is generated copy.
 */

type Target = { label: string; href: string; note: string };

const TARGETS: Target[] = [
  ...nav.primary.map((item) => ({
    label: item.label,
    href: item.href,
    note: "Page",
  })),
  // The six modules the features page actually anchors, so these land where
  // the visitor expects rather than at the top of a long page.
  ...modules
    .filter((module) =>
      ["proforma", "optimization", "sheet-store", "delivery-note", "job-profitability", "reports-hub"].includes(
        module.id,
      ),
    )
    .map((module) => ({ label: module.name, href: `/features#${module.id}`, note: "Module" })),
  { label: "Blog", href: "/blog", note: "Page" },
  { label: "Resources", href: "/resources", note: "Page" },
  { label: "Contact", href: "/contact", note: "Page" },
  { label: "Licence", href: "/legal/license", note: "Page" },
];

/** Letters only, so punctuation and casing do not decide the match. */
function normalise(value: string): string {
  return value
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, " ")
    .trim();
}

/** Levenshtein distance, two rows instead of a matrix — the inputs are short. */
function distance(a: string, b: string): number {
  if (a === b) return 0;
  let previous = Array.from({ length: b.length + 1 }, (_, index) => index);
  for (let i = 1; i <= a.length; i += 1) {
    const current = [i];
    for (let j = 1; j <= b.length; j += 1) {
      const cost = a[i - 1] === b[j - 1] ? 0 : 1;
      current[j] = Math.min(current[j - 1]! + 1, previous[j]! + 1, previous[j - 1]! + cost);
    }
    previous = current;
  }
  return previous[b.length]!;
}

function closest(pathname: string): Target[] {
  const wanted = normalise(pathname);
  if (!wanted) return [];

  const scored = TARGETS.map((target) => {
    const candidate = normalise(target.href.replace(/^\/features#/, "features "));
    const bare = candidate.split(" ").filter(Boolean).pop() ?? candidate;
    const edit = distance(wanted, bare);
    // A shared prefix counts for something: `/develop` should find `/developer`.
    const shared = bare.startsWith(wanted) || wanted.startsWith(bare) ? 2 : 0;
    return { target, edit, shared };
  })
    .filter((entry) => entry.edit <= Math.max(2, Math.ceil(wanted.length / 3)) || entry.shared > 0)
    .sort((a, b) => a.edit - b.edit || b.shared - a.shared);

  // One suggestion per destination path: a module and its page are different
  // places, so both may appear, but the same href twice never should.
  const seen = new Set<string>();
  const picks: Target[] = [];
  for (const entry of scored) {
    if (seen.has(entry.target.href)) continue;
    seen.add(entry.target.href);
    picks.push(entry.target);
    if (picks.length === 3) break;
  }
  return picks;
}

export function NotFoundSuggestions() {
  const t = useT();
  const pathname = usePathname() ?? "";
  const suggestions = closest(pathname);

  return (
    <div className="mt-10">
      <p className="eyebrow">Where you were going</p>

      {suggestions.length > 0 ? (
        <>
          <p className="mt-3 text-sm leading-relaxed text-text-muted">
            You asked for <span className="font-mono text-text-secondary">{pathname}</span>. The closest pages on this
            site are:
          </p>
          <ul className="mt-4 grid gap-3 sm:grid-cols-3">
            {suggestions.map((entry) => (
              <li key={entry.href}>
                <Link
                  href={entry.href}
                  className="card card-interactive flex items-center justify-between gap-3 px-4 py-3 text-sm text-text-primary"
                >
                  <span className="min-w-0">
                    <span className="block truncate font-medium">{t(entry.label)}</span>
                    <span className="mono-label mt-1 block">{t(entry.note)}</span>
                  </span>
                  <ArrowRight />
                </Link>
              </li>
            ))}
          </ul>
        </>
      ) : pathname ? (
        <p className="mt-3 text-sm leading-relaxed text-text-muted">
          Nothing on this site matches <span className="font-mono text-text-secondary">{pathname}</span>. These are the
          routes people usually want.
        </p>
      ) : (
        /*
          Before hydration the path is not known yet — this page is prerendered
          as a static 404 — so it says something true rather than naming a
          request it cannot see.
        */
        <p className="mt-3 text-sm leading-relaxed text-text-muted">
          {t("The address you followed does not lead anywhere on this site. These are the routes people usually want.")}
        </p>
      )}
    </div>
  );
}

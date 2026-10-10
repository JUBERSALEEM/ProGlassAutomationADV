import Link from "next/link";
import { nav, site } from "@/lib/site";
import { Logo } from "@/components/brand/Logo";
import { GlassCubeEmblem } from "@/components/brand/GlassCubeEmblem";
import type { Translator } from "@/i18n/core";
import { BuildStampRow } from "@/components/layout/BuildStamp";

function Column({
  title,
  links,
  t,
}: {
  title: string;
  links: readonly { label: string; href: string }[];
  /** The page's translator — the footer renders on the server. */
  t: Translator;
}) {
  return (
    <div>
      <h2 className="eyebrow mb-4">{title}</h2>
      {/*
        Card-shaped links, the mobile sheet's own treatment, moved up to the
        footer: bare text lists read as filler; a card carries its own edge,
        holds its hover, and tells a thumb exactly where to press. Same
        labels, same routes — only the shape of the door changed, so nothing
        here invents a page.
      */}
      <ul className="space-y-2">
        {links.map((link) => (
          <li key={link.href}>
            <Link
              href={link.href}
              className="card-interactive group flex items-center justify-between gap-2 rounded-card border-2 border-line-strong/60 bg-ink-900/70 px-3 py-2 text-sm font-medium text-text-secondary transition-colors duration-200 hover:border-brand-400/60 hover:text-text-primary"
            >
              <span>{t(link.label)}</span>
              <svg
                aria-hidden="true"
                viewBox="0 0 16 16"
                className="-me-1 h-3.5 w-3.5 shrink-0 opacity-0 transition-all duration-200 group-hover:me-0 group-hover:translate-x-0.5 group-hover:opacity-100"
                fill="none"
                stroke="currentColor"
                strokeWidth="1.75"
                strokeLinecap="round"
                strokeLinejoin="round"
              >
                <path d="M2.5 8h11M9.5 4l4 4-4 4" />
              </svg>
            </Link>
          </li>
        ))}
      </ul>
    </div>
  );
}

/**
 * The site's footer.
 *
 * It is a band of its own rather than a strip at the bottom of the page: the
 * well colour, an aurora of the family's own pools at low strength, and the
 * brand emblem behind the address. The reason for the emblem is that the footer
 * is the last thing on the page and the one place a visitor who scrolled
 * straight to the bottom learns who made this — so it carries the mark the
 * application opens with, not only its name.
 */
export function Footer({ t }: { t: Translator }) {
  return (
    <footer className="band-deep relative overflow-hidden">
      {/* The family's own light, at the strength a footer needs: enough to say
          this is a surface and not the end of the page, not enough to compete
          with the content above it. */}
      <div aria-hidden="true" className="pointer-events-none absolute inset-0 opacity-45">
        <div className="aurora-field" />
      </div>
      {/* The emblem, large, behind the address column. */}
      <div
        aria-hidden="true"
        className="pointer-events-none absolute -bottom-16 -end-10 hidden opacity-[0.07] lg:block"
      >
        <GlassCubeEmblem size={360} label={null} />
      </div>

      <span aria-hidden="true" className="rule-rich absolute inset-x-0 -top-px" />

      <div className="shell relative py-16 lg:py-20">
        <div className="grid gap-12 lg:grid-cols-[1.4fr_2.6fr]">
          <div className="max-w-sm">
            <Logo size={34} />
            <p className="mt-5 text-sm leading-relaxed text-text-muted">
              {t("Industrial glass-fabrication software for Windows. Estimation, production preparation, stock, dispatch,")}{" "}
              {t("VAT invoicing, statements and reporting — on one machine, in one application.")}
            </p>

            {/* The one fact the footer exists to state: this is one person's
                work, and their name and address are on it. */}
            <p className="mt-6 border-s-2 border-brand-500/60 ps-4 text-sm leading-relaxed text-text-secondary">
              {t("Written and maintained by")}{" "}
              <Link href="/developer" className="link-grow font-semibold text-brand-200 hover:text-brand-50">
                {site.developerName}
              </Link>
              {t(", a glass estimator in")} {site.developerLocation}.{" "}
              <Link href="/collaborate" className="link-grow font-medium text-brand-300 hover:text-brand-200">
                {t("Work on it with him")}
              </Link>
              .
            </p>

            <dl className="mt-7 space-y-1.5 font-mono text-2xs uppercase tracking-[0.14em] text-text-faint font-medium">
              <div className="flex gap-2">
                <dt>{t("Version")}</dt>
                <dd className="text-text-muted">{site.version}</dd>
              </div>
              <div className="flex gap-2">
                <dt>{t("Platform")}</dt>
                <dd className="text-text-muted">{t(site.platform)}</dd>
              </div>
              <div className="flex gap-2">
                <dt>{t("Built with")}</dt>
                <dd className="text-text-muted">{site.framework}</dd>
              </div>
            </dl>
          </div>

          <div className="grid grid-cols-2 gap-8 sm:grid-cols-4">
            <Column title={t("Product")} links={nav.footer.product} t={t} />
            <Column title={t("Company")} links={nav.footer.company} t={t} />
            <Column title={t("Resources")} links={nav.footer.resources} t={t} />
            <Column title={t("Legal")} links={nav.footer.legal} t={t} />
          </div>
        </div>

        <div className="mt-14 flex flex-col gap-4 border-t border-line/60 pt-7 sm:flex-row sm:items-center sm:justify-between">
          <p className="text-xs text-text-faint font-medium">{t(site.copyrightLine)}</p>
          <p className="max-w-xl text-xs leading-relaxed text-text-faint font-medium">
            {t(
              "ProGlass Automation is desktop software. There is no cloud account, no subscription dashboard and nothing to sign in to on this website.",
            )}
          </p>
          {/*
            The build stamp, on every page including the ones that are only ever
            reached by a search engine. The top panel carries the same fact for
            anyone who is looking at the interface; this is the copy a visitor
            finds when they have scrolled to the bottom asking "and how current
            is this?".
          */}
          <BuildStampRow t={t} className="sm:text-right" />
        </div>
      </div>
    </footer>
  );
}

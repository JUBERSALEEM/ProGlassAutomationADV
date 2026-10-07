import Link from "next/link";
import type { ReactNode } from "react";
import { Container } from "@/components/ui/Container";
import { Reveal } from "@/components/ui/Reveal";
import { Spotlight } from "@/components/ui/Spotlight";
import { PageBanner, type BannerArt } from "@/components/layout/PageBanner";
import type { Translator } from "@/i18n/core";

/**
 * Shared page opener. Every interior page uses it so the site has one
 * entrance rhythm, while colour, illustration and content stay page-specific.
 */
export function PageHero({
  /** The page's translator — this component renders on the server. */
  t,
  eyebrow,
  title,
  lead,
  actions,
  meta,
  breadcrumb,
  banner,
  children,
}: {
  /** The page's translator — this component renders on the server. */
  t: Translator;
  eyebrow: string;
  title: ReactNode;
  lead: ReactNode;
  actions?: ReactNode;
  meta?: { label: string; value: string }[];
  breadcrumb?: { label: string; href: string }[];
  /** Which banner drawing opens this page. */
  banner?: BannerArt;
  children?: ReactNode;
}) {
  return (
    <section className="relative overflow-hidden border-b border-line/60 pb-14 pt-12 sm:pb-16 sm:pt-16 lg:pb-20 lg:pt-20">
      {/*
        The opener's light.

        One aurora field carrying the family's own two accent washes across the
        band, plus a single wide orb low on the page for the ground under the
        title. Three separate `filter: blur(110px)` circles used to do this, and
        a blur that wide on a 20 rem element is a multi-pass kernel re-run
        whenever anything near it changes — which, on a page whose whole top
        third is this, is every frame. `.orb` draws the same falloff from one
        gradient: same picture, one rasterisation, then free.

        The field drifts on a transform only, so it composites; it is
        `contain: paint`, so a pixel invalidated inside it cannot make the
        browser re-examine the page around it.
      */}
      <div aria-hidden="true" className="glow-layer pointer-events-none absolute inset-0 overflow-hidden">
        <span className="orb aurora-drift -bottom-24 left-[30%] h-[30rem] w-[46rem]" />
        <div className="aurora-field aurora-drift" />
      </div>
      {/* One hairline of accent along the top edge, so a page opener is a
          threshold rather than a place the content happens to start. */}
      <span aria-hidden="true" className="rule-rich absolute inset-x-0 top-0" />

      {banner ? <PageBanner art={banner} /> : null}

      <Container className="relative">
        {breadcrumb ? (
          <nav aria-label={t("Breadcrumb")} className="mb-6">
            <ol className="flex flex-wrap items-center gap-2 font-mono text-2xs uppercase tracking-[0.14em] text-text-faint font-medium">
              {breadcrumb.map((crumb, index) => (
                <li key={crumb.href} className="flex items-center gap-2">
                  {index > 0 ? <span aria-hidden="true">/</span> : null}
                  <Link href={crumb.href} className="transition-colors hover:text-brand-300">
                    {t(crumb.label)}
                  </Link>
                </li>
              ))}
            </ol>
          </nav>
        ) : null}

        <div className="grid gap-10 lg:grid-cols-[minmax(0,1.15fr)_minmax(0,0.85fr)] lg:items-end lg:gap-14">
          <div>
            <Reveal>
              <p className="eyebrow-chip mb-5">{eyebrow}</p>
            </Reveal>
            <Reveal delay={60}>
              <h1 className="display-1">{title}</h1>
            </Reveal>
            <Reveal delay={120}>
              <p className="lede-lg mt-5 max-w-2xl">{lead}</p>
            </Reveal>
            {actions ? (
              <Reveal delay={180} className="mt-8 flex flex-wrap items-center gap-3">
                {actions}
              </Reveal>
            ) : null}
          </div>

          {meta ? (
            <Reveal delay={140}>
              <dl className="grid grid-cols-2 gap-4">
                {meta.map((item) => (
                  /* The page's own facts, in the site's surface system: on
                     Crystal these are small panes of glass beside the title,
                     and in the other families they are the plain cards they
                     have always been. */
                  <Spotlight key={t(item.label)} className="surface edge-conic p-4">
                    <dt className="font-mono text-2xs uppercase tracking-[0.14em] text-text-faint font-medium">{t(item.label)}</dt>
                    <dd className="mt-1.5 text-sm font-medium text-text-secondary">{t(item.value)}</dd>
                  </Spotlight>
                ))}
              </dl>
            </Reveal>
          ) : null}
        </div>

        {children ? <Reveal delay={200} className="mt-12">{children}</Reveal> : null}
      </Container>
    </section>
  );
}

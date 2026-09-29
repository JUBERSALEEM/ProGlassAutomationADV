import Link from "next/link";
import type { ReactNode } from "react";
import { Container } from "@/components/ui/Container";
import { Reveal } from "@/components/ui/Reveal";
import { PageBanner, type BannerArt } from "@/components/layout/PageBanner";
import { site } from "@/lib/site";

/**
 * Breadcrumb structured data for the same trail the visitor can see.
 * Emitted only when the page supplies a breadcrumb, so the markup never
 * describes a hierarchy the page does not show.
 */
function breadcrumbSchema(trail: { label: string; href: string }[]) {
  return {
    "@context": "https://schema.org",
    "@type": "BreadcrumbList",
    itemListElement: trail.map((crumb, index) => ({
      "@type": "ListItem",
      position: index + 1,
      name: crumb.label,
      item: `${site.url}${crumb.href === "/" ? "" : crumb.href}`,
    })),
  };
}

/**
 * Shared page opener. Every interior page uses it so the site has one
 * entrance rhythm, while colour, illustration and content stay page-specific.
 */
export function PageHero({
  eyebrow,
  title,
  lead,
  actions,
  meta,
  breadcrumb,
  banner,
  children,
}: {
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
      {breadcrumb ? (
        <script
          type="application/ld+json"
          dangerouslySetInnerHTML={{ __html: JSON.stringify(breadcrumbSchema(breadcrumb)) }}
        />
      ) : null}
      <div aria-hidden="true" className="pointer-events-none absolute inset-0">
        <div className="animate-drift-slow absolute -top-32 left-[10%] h-80 w-80 rounded-full bg-brand-500/14 blur-[110px]" />
        <div className="absolute -top-20 right-[6%] h-64 w-64 rounded-full bg-glass-cyan/10 blur-[100px]" />
        <div className="absolute bottom-0 left-[46%] h-52 w-52 rounded-full bg-glass-rose/8 blur-[110px]" />
      </div>

      {banner ? <PageBanner art={banner} /> : null}

      <Container className="relative">
        {breadcrumb ? (
          <nav aria-label="Breadcrumb" className="mb-6">
            <ol className="flex flex-wrap items-center gap-2 font-mono text-2xs uppercase tracking-[0.14em] text-text-faint">
              {breadcrumb.map((crumb, index) => (
                <li key={crumb.href} className="flex items-center gap-2">
                  {index > 0 ? <span aria-hidden="true">/</span> : null}
                  <Link href={crumb.href} className="transition-colors hover:text-brand-300">
                    {crumb.label}
                  </Link>
                </li>
              ))}
            </ol>
          </nav>
        ) : null}

        <div className="grid gap-10 lg:grid-cols-[minmax(0,1.15fr)_minmax(0,0.85fr)] lg:items-end lg:gap-14">
          <div>
            <Reveal>
              <p className="eyebrow mb-4 flex items-center gap-3">
                <span aria-hidden="true" className="h-px w-8 bg-brand-400/60" />
                {eyebrow}
              </p>
            </Reveal>
            <Reveal delay={60}>
              <h1 className="text-[2rem] leading-[1.08] sm:text-[2.5rem] lg:text-[2.85rem]">{title}</h1>
            </Reveal>
            <Reveal delay={120}>
              <p className="lede mt-5 max-w-2xl">{lead}</p>
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
                  <div key={item.label} className="rounded-card border border-line/60 bg-ink-900/50 p-4">
                    <dt className="font-mono text-2xs uppercase tracking-[0.14em] text-text-faint">{item.label}</dt>
                    <dd className="mt-1.5 text-sm font-medium text-text-secondary">{item.value}</dd>
                  </div>
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

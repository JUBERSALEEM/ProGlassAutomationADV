import type { ReactNode } from "react";
import Link from "next/link";
import { Container, Section } from "@/components/ui/Container";
import { PageHero } from "@/components/layout/PageHero";
import type { BannerArt } from "@/components/layout/PageBanner";
import type { Translator } from "@/i18n/core";

export type LegalSection = { heading: string; paragraphs: string[]; list?: string[] };

export function LegalPage({
  /** The page's translator — this component renders on the server. */
  t,
  eyebrow,
  title,
  lead,
  updated,
  sections,
  footer,
  banner,
}: {
  /** The page's translator — this component renders on the server. */
  t: Translator;
  eyebrow: string;
  title: string;
  lead: string;
  updated: string;
  sections: LegalSection[];
  footer?: ReactNode;
  banner?: BannerArt;
}) {
  return (
    <>
      <PageHero
      t={t}
        eyebrow={eyebrow}
        title={title}
        banner={banner}
        lead={lead}
        breadcrumb={[
          { label: t("Home"), href: "/" },
          { label: eyebrow, href: `#` },
        ]}
        meta={[
          { label: t("Last updated"), value: updated },
          { label: t("Applies to"), value: t("This website and the desktop application") },
        ]}
      />

      <Section aria-label={title}>
        <Container>
          <div className="measure">
            <ol className="mb-10 space-y-1.5 border-l border-line-strong/60 pl-5">
              {sections.map((section, index) => (
                <li key={`${section.heading}-${index}`}>
                  <a
                    href={`#section-${index + 1}`}
                    className="text-sm text-text-muted transition-colors hover:text-brand-200"
                  >
                    <span className="mr-2 font-mono text-2xs tabular-nums text-text-faint font-medium">
                      {String(index + 1).padStart(2, "0")}
                    </span>
                    {section.heading}
                  </a>
                </li>
              ))}
            </ol>

            <div className="space-y-10">
              {sections.map((section, index) => (
                <section key={`${section.heading}-${index}`} id={`section-${index + 1}`} className="scroll-mt-28">
                  <h2 className="flex items-baseline gap-3 text-lg font-semibold text-text-primary sm:text-xl">
                    <span className="font-mono text-sm text-brand-300">
                      {String(index + 1).padStart(2, "0")}
                    </span>
                    {section.heading}
                  </h2>
                  <div className="flow mt-4">
                    {section.paragraphs.map((paragraph) => (
                      <p key={paragraph} className="text-sm leading-relaxed text-text-secondary sm:text-base">
                        {paragraph}
                      </p>
                    ))}
                    {section.list ? (
                      <ul className="space-y-2.5 pl-1">
                        {section.list.map((item) => (
                          <li key={item} className="flex gap-3 text-sm leading-relaxed text-text-muted">
                            <span aria-hidden="true" className="mt-2 h-1 w-1 shrink-0 rounded-full bg-brand-400/70" />
                            {item}
                          </li>
                        ))}
                      </ul>
                    ) : null}
                  </div>
                </section>
              ))}
            </div>

            {/* A page that supplies its own closing actions keeps them; the rest
                get this default so no legal page dead-ends. */}
            {footer ? (
              <div className="mt-12 border-t border-line/60 pt-8">{footer}</div>
            ) : (
            <div className="card-accent mt-12 p-7">
              <h2 className="text-lg font-semibold text-text-primary sm:text-xl">
                {t("Read something more concrete next.")}
              </h2>
              <p className="mt-3 text-sm leading-relaxed text-text-secondary">
                {t("The licence page covers what a licence key unlocks and what the free tier includes. If you would rather")}
                {t("ask a person, the contact form reaches the developer directly.")}
              </p>
              <div className="mt-6 flex flex-wrap gap-3 text-sm">
                <Link
                  href="/legal/license"
                  className="rounded-full border border-brand-400/40 bg-brand-500/12 px-4 py-2 font-medium text-brand-100 transition-colors hover:border-brand-300/60 hover:text-brand-50"
                >
                  {t("How licensing works")}
                </Link>
                <Link
                  href="/contact"
                  className="rounded-full border border-line-strong/70 px-4 py-2 font-medium text-text-secondary transition-colors hover:border-brand-400/50 hover:text-text-primary"
                >
                  {t("Contact the developer")}
                </Link>
              </div>
            </div>
            )}
          </div>
        </Container>
      </Section>
    </>
  );
}

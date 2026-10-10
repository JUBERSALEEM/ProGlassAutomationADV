import type { Metadata } from "next";
import { LocaleLink as Link } from "@/components/ui/LocaleLink";
import { Container, Section } from "@/components/ui/Container";
import { PageHero } from "@/components/layout/PageHero";
import { footerIconFor } from "@/data/footerIcons";
import { ArrowRight, ButtonLink } from "@/components/ui/Button";
import { Reveal } from "@/components/ui/Reveal";
import { SectionHeading } from "@/components/ui/SectionHeading";
import { GlassCard, GlassMetric, GlassMetricRow } from "@/components/ui/Glass";
import { ProductShowcaseViewer } from "@/components/gallery/ProductShowcaseViewer";
import { capabilityGroups } from "@/data/screenshots";
import { RouteSchema } from "@/components/seo/RouteSchema";
import { pageMetadata } from "@/lib/metadata";
import { translatorFor } from "@/i18n/server";
import { PageOverlay } from "@/i18n/PageOverlay";

type PageProps = { params: Promise<{ locale: string }> };

export async function generateMetadata({ params }: PageProps): Promise<Metadata> {
  const t = await translatorFor(params);
  return pageMetadata({
    locale: t.locale,
    /*
     * 39 characters, because `metadata.title.template` appends
     * " · ProGlass Automation" and a title that overruns 62 characters is one
     * a search result truncates. The old one ran to 56 on its own and 79 with
     * the suffix, which the site's own audit reported as the longest title on
     * the site — for the page whose subject is the interface.
     */
    title: t("The interface, screen by screen"),
    description: t(
      "Walk every ProGlass Automation screen: estimation, the 3D glass calculators, 2D cutting optimization, the eight production stages and the reports.",
    ),
    path: "/screenshots",
    keywords: [
      t("glass software screenshots"),
      t("glass ERP interface"),
      t("proforma invoice screen"),
      t("glass optimization layout"),
      t("glass fabrication ERP"),
    ],
    ogEyebrow: t("Inside ProGlass Automation"),
  });
}

export default async function ScreenshotsPage({ params }: PageProps) {
  const t = await translatorFor(params);
  const locale = t.locale;
  const totalCurated = capabilityGroups.reduce((acc, g) => acc + g.screenshotIds.length, 0);

  return (
    <PageOverlay locale={t.locale} route="screenshots">
    <>
      {/* Structured data for this route */}
      <RouteSchema
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        locale={locale as any}
        path="/screenshots"
        title={t("The interface, screen by screen")}
        description={t(
          "Explore the complete ProGlass Automation ERP interface: commercial estimation, 3D glass calculators, 2D guillotine cutting optimization, 8-stage production tracking, and financial analytics.",
        )}
      />

      <PageHero
        t={t}
        glyph={footerIconFor("/screenshots")!}
        eyebrow={t("Product Showcase")}
        title={t("Inside ProGlass Automation.")}
        lead={t(
          "Explore the complete desktop ERP interface engineered specifically for architectural glass fabrication. From multi-tier commercial estimation and 2D cutting optimization to 8-stage shop-floor execution and financial reporting — explore interactive previews of each operational capability.",
        )}
        banner="production"
        breadcrumb={[
          { label: t("Home"), href: "/" },
          { label: t("Screenshots"), href: "/screenshots" },
        ]}
        meta={[
          { label: t("Capability Suites"), value: `${capabilityGroups.length}` },
          { label: t("Curated Previews"), value: `${totalCurated}` },
          { label: t("Fabrication Stages"), value: "8" },
          { label: t("ERP Engine"), value: t("Native Windows") },
        ]}
        actions={
          <>
            <ButtonLink href="/contact" size="lg" trailing={<ArrowRight />}>
              {t("Request a Demo")}
            </ButtonLink>
            <ButtonLink href="/features" variant="secondary" size="lg">
              {t("Feature detail")}
            </ButtonLink>
          </>
        }
      />

      {/* ── Capability Scope Overview ────────────────────────────── */}
      <Section aria-label={t("Product capabilities overview")}>
        <Container>
          <Reveal>
            <GlassCard tier="primary">
              <GlassMetricRow className="gap-y-6">
                <GlassMetric
                  value={capabilityGroups.length}
                  label={t("Customer-Facing Capability Groups")}
                  hint={t(
                    "Estimation & Commercial, Glass Calculators, Optimization, Production, Inventory & Dispatch, and Business & Finance.",
                  )}
                />
                <GlassMetric
                  value={4}
                  label={t("3D Architectural Build-ups")}
                  hint={t(
                    "High-precision 3D parametric models for SGU, DGU, Laminated, and Composite acoustic glass units.",
                  )}
                />
                <GlassMetric
                  value={totalCurated}
                  label={t("Interactive Module Previews")}
                  hint={t(
                    "Authentic application chrome, working tabs, parametric calculations, and live fabrication data.",
                  )}
                />
              </GlassMetricRow>
            </GlassCard>
          </Reveal>

          <Reveal delay={80}>
            <p className="mt-5 max-w-4xl text-xs leading-relaxed text-text-faint font-medium">
              {t(
                "Each interactive module preview draws the application's native window chrome and layouts around real industrial logic. Explore the 6 capability suites below with arrow controls, keyboard navigation, or touch gestures.",
              )}
            </p>
          </Reveal>
        </Container>
      </Section>

      {/* ── Interactive Product Showcase Viewer ──────────────────── */}
      <Section tone="raised" aria-label={t("Interactive Product Showcase")}>
        <Container>
          <SectionHeading
            eyebrow={t("Inside ProGlass")}
            title={t("Interactive Capability Viewer.")}
            lead={t(
              "Select a capability suite to explore screens. Use the arrow controls or keyboard Left/Right arrows to walk through each operational station.",
            )}
          />

          <div className="mt-8">
            <ProductShowcaseViewer />
          </div>

          <div className="surface mt-10 flex flex-col gap-4 p-6 sm:flex-row sm:items-center sm:justify-between">
            <p className="max-w-2xl text-sm leading-relaxed text-text-secondary">
              {t(
                "The previews above display live modular workflows with sample parameters. To see how a real 22-line commercial glazing contract processes end-to-end through every module,",
              )}{" "}
              <Link href="/worked-example" className="link-underline text-brand-200">
                {t("explore the complete Worked Example")}
              </Link>
              .
            </p>
            <ButtonLink href="/worked-example" variant="secondary" size="sm" trailing={<ArrowRight />}>
              {t("Worked example")}
            </ButtonLink>
          </div>
        </Container>
      </Section>

      {/* ── CTA ──────────────────────────────────────────────────── */}
      <Section aria-label={t("Request a demo")}>
        <Container>
          <Reveal>
            <div className="glass-primary flex flex-col gap-6 p-8 sm:flex-row sm:items-center sm:justify-between sm:p-10">
              <div>
                <h2 className="display-2">{t("The best screenshot is your own data.")}</h2>
                <p className="mt-3 max-w-xl text-sm leading-relaxed text-text-muted">
                  {t(
                    "A customized demonstration walks through your factory's exact screens with your glass specifications, standard sheet stock, and pricing formulas.",
                  )}
                </p>
              </div>
              <div className="flex shrink-0 flex-wrap gap-3">
                <ButtonLink href="/contact" size="lg" trailing={<ArrowRight />}>
                  {t("Request a Demo")}
                </ButtonLink>
                <ButtonLink href="/optimization" variant="secondary" size="lg">
                  {t("Optimization detail")}
                </ButtonLink>
              </div>
            </div>
          </Reveal>
        </Container>
      </Section>
    </>
    </PageOverlay>
  );
}

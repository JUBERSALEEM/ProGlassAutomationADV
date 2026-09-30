import type { Metadata } from "next";
import { LocaleLink as Link } from "@/components/ui/LocaleLink";
import { Container, Section } from "@/components/ui/Container";
import { PageHero } from "@/components/layout/PageHero";
import { ArrowRight, ButtonLink } from "@/components/ui/Button";
import { Reveal } from "@/components/ui/Reveal";
import { SectionHeading } from "@/components/ui/SectionHeading";
import { GlassCard, GlassMetric, GlassMetricRow } from "@/components/ui/Glass";
import { ScreenshotGallery } from "@/components/gallery/ScreenshotGallery";
import { screenshotCategories, screenshots } from "@/data/screenshots";
import { RouteSchema } from "@/components/seo/RouteSchema";
import { pageMetadata } from "@/lib/metadata";
import { translatorFor } from "@/i18n/server";

type PageProps = { params: Promise<{ locale: string }> };

export async function generateMetadata({ params }: PageProps): Promise<Metadata> {
  const t = await translatorFor(params);
  return pageMetadata({
  locale: t.locale,
  title: t("Screenshots — the ProGlass Automation interface"),
  description: t("The ProGlass Automation interface: dashboards, the proforma wizard, glass calculators, the optimization workspace, sheet store, delivery and reports."),
  path: "/screenshots",
  keywords: ["glass software screenshots", t("glass ERP interface"), "proforma invoice screen", "glass optimization layout"],
  ogEyebrow: t("The interface"),
  });
}

export default async function ScreenshotsPage({ params }: PageProps) {
  const t = await translatorFor(params);
  const locale = t.locale;
  const published = screenshots.filter((s) => s.source === "capture").length;
  const repoAssets = screenshots.filter((s) => s.source === "repo").length;
  const pending = screenshots.filter((s) => s.source === "pending").length;

  return (

    <>
    {/* Structured data for this route — see lib/schema.ts. */}
    <RouteSchema
      locale={locale}
      path="/screenshots"
      title={t("Screenshots — the ProGlass Automation interface")}
      description={t("The ProGlass Automation interface: dashboards, the proforma wizard, glass calculators, the optimization workspace, sheet store, delivery and reports.")}
    />
      <PageHero
      t={t}
        eyebrow={t("Screenshots")}
        title={t("The interface, screen by screen.")}
        lead={t("A working application is judged by its list screens and its data grids, so those come first. Every card states exactly what it is showing, and nothing on this page presents a mock-up as a capture of the product.")}
        banner="production"
        breadcrumb={[
          { label: t("Home"), href: "/" },
          { label: t("Screenshots"), href: "/screenshots" },
        ]}
        meta={[
          { label: t("Screens documented"), value: `${screenshots.length}` },
          { label: t("Categories"), value: `${screenshotCategories.length - 1}` },
          { label: t("Application assets"), value: `${repoAssets}` },
          { label: t("Captures pending"), value: `${pending}` },
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

      {/* ── Honest status note ───────────────────────────────────── */}
      <Section aria-label={t("Screenshot status")}>
        <Container>
          <Reveal>
            <GlassCard tier="primary">
              <GlassMetricRow className="gap-y-6">
                <GlassMetric
                  value={published}
                  label={t("Published screen captures")}
                  hint={t("Photographs of the running application. There are none yet.")}
                />
                <GlassMetric
                  value={repoAssets}
                  label={t("Illustrations from the application")}
                  hint={t("Real assets that ship inside the product — the SGU, DGU, lamination and DGU + lamination renders.")}
                />
                <GlassMetric
                  value={pending}
                  label={t("Interactive previews")}
                  hint={t("Real window chrome, real module labels and real fields, with sample data you can sort, filter and step through. Clearly labelled as a preview, never presented as a screen capture.")}
                />
              </GlassMetricRow>
            </GlassCard>
          </Reveal>

          <Reveal delay={80}>
            <p className="mt-5 max-w-4xl text-xs leading-relaxed text-text-faint font-medium">
              This website was built from the application&apos;s source tree, in an environment where the Windows desktop
              application could not be launched, so no photographs of a running build exist yet. Rather than generating
              images and captioning them as screenshots, each card draws the application&apos;s own window chrome around a
              working preview of that module — and says on the card that it is a preview. The navigation groups, module
              labels, tab names, field names and charge rows come from the application itself:{" "}
              <span className="font-mono">Views/Personal/GlassShell.xaml</span>,{" "}
              <span className="font-mono">Themes/Languages/en-US.xaml</span>{t("and the module views. Where a module has something to compute — the optimizer, the production queues — the preview computes it live.")}</p>
          </Reveal>
        </Container>
      </Section>

      {/* ── Gallery ──────────────────────────────────────────────── */}
      <Section tone="raised" aria-label={t("Screenshot gallery")}>
        <Container>
          <SectionHeading
            eyebrow={t("Gallery")}
            title={t("Filter by module.")}
            lead={t("Select a category to narrow the gallery. Click any card for a larger view with its highlights and the source path inside the application.")}
          />
          <div className="mt-10">
            <ScreenshotGallery />
          </div>

          <div className="surface mt-8 flex flex-col gap-4 p-6 sm:flex-row sm:items-center sm:justify-between">
            <p className="max-w-2xl text-sm leading-relaxed text-text-secondary">
{t("The previews above use sample data. To see what the same screens hold when one job runs all the way through them, the")}{" "}
              <Link href="/worked-example" className="link-underline text-brand-200">
                worked example
              </Link>{" "}
              {t("shows the real seeded workflow — its documents, its 22 lines and its totals.")}
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
                <h2 className="text-2xl leading-tight sm:text-3xl">{t("The best screenshot is your own data.")}</h2>
                <p className="mt-3 max-w-xl text-sm leading-relaxed text-text-muted">{t("A demonstration walks through your screens with your glass types, your sheet sizes and your margins.")}</p>
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
  );
}

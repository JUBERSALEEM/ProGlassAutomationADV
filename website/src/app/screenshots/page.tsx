import type { Metadata } from "next";
import { Container, Section } from "@/components/ui/Container";
import { PageHero } from "@/components/layout/PageHero";
import { ArrowRight, ButtonLink } from "@/components/ui/Button";
import { Reveal } from "@/components/ui/Reveal";
import { SectionHeading } from "@/components/ui/SectionHeading";
import { ScreenshotGallery } from "@/components/gallery/ScreenshotGallery";
import { screenshotCategories, screenshots } from "@/data/screenshots";
import { pageMetadata } from "@/lib/metadata";

export const metadata: Metadata = pageMetadata({
  title: "Screenshots — the ProGlass Automation interface",
  description:
    "The ProGlass Automation interface: dashboards, the proforma wizard, glass calculators, the optimization workspace, sheet store, delivery and reports.",
  path: "/screenshots",
  keywords: ["glass software screenshots", "glass ERP interface", "proforma invoice screen", "glass optimization layout"],
});

export default function ScreenshotsPage() {
  const published = screenshots.filter((s) => s.source === "capture").length;
  const repoAssets = screenshots.filter((s) => s.source === "repo").length;
  const pending = screenshots.filter((s) => s.source === "pending").length;

  return (
    <>
      <PageHero
        eyebrow="Screenshots"
        title="The interface, screen by screen."
        lead="A working application is judged by its list screens and its data grids, so those come first. Every card states exactly what it is showing, and nothing on this page presents a mock-up as a capture of the product."
        banner="production"
        breadcrumb={[
          { label: "Home", href: "/" },
          { label: "Screenshots", href: "/screenshots" },
        ]}
        meta={[
          { label: "Screens documented", value: `${screenshots.length}` },
          { label: "Categories", value: `${screenshotCategories.length - 1}` },
          { label: "Application assets", value: `${repoAssets}` },
          { label: "Captures pending", value: `${pending}` },
        ]}
        actions={
          <>
            <ButtonLink href="/contact" size="lg" trailing={<ArrowRight />}>
              Request a Demo
            </ButtonLink>
            <ButtonLink href="/features" variant="secondary" size="lg">
              Feature detail
            </ButtonLink>
          </>
        }
      />

      {/* ── Honest status note ───────────────────────────────────── */}
      <Section aria-label="Screenshot status">
        <Container>
          <Reveal>
            <div className="surface-raised grid gap-6 p-6 sm:grid-cols-3 sm:p-7">
              <div>
                <p className="font-mono text-2xl font-semibold text-text-primary">{published}</p>
                <p className="mt-2 text-sm font-medium text-text-secondary">Published screen captures</p>
                <p className="mt-1 text-xs leading-relaxed text-text-faint font-medium">
                  Photographs of the running application. There are none yet.
                </p>
              </div>
              <div>
                <p className="font-mono text-2xl font-semibold text-text-primary">{repoAssets}</p>
                <p className="mt-2 text-sm font-medium text-text-secondary">Illustrations from the application</p>
                <p className="mt-1 text-xs leading-relaxed text-text-faint font-medium">
                  Real assets that ship inside the product — the SGU, DGU, lamination and DGU + lamination renders.
                </p>
              </div>
              <div>
                <p className="font-mono text-2xl font-semibold text-text-primary">{pending}</p>
                <p className="mt-2 text-sm font-medium text-text-secondary">Interactive previews</p>
                <p className="mt-1 text-xs leading-relaxed text-text-faint font-medium">
                  Real window chrome, real module labels and real fields, with sample data you can sort, filter and
                  step through. Clearly labelled as a preview, never presented as a screen capture.
                </p>
              </div>
            </div>
          </Reveal>

          <Reveal delay={80}>
            <p className="mt-5 max-w-4xl text-xs leading-relaxed text-text-faint font-medium">
              This website was built from the application&apos;s source tree, in an environment where the Windows desktop
              application could not be launched, so no photographs of a running build exist yet. Rather than generating
              images and captioning them as screenshots, each card draws the application&apos;s own window chrome around a
              working preview of that module — and says on the card that it is a preview. The navigation groups, module
              labels, tab names, field names and charge rows come from the application itself:{" "}
              <span className="font-mono">Views/Personal/GlassShell.xaml</span>,{" "}
              <span className="font-mono">Themes/Languages/en-US.xaml</span> and the module views. Where a module has
              something to compute — the optimizer, the production queues — the preview computes it live.
            </p>
          </Reveal>
        </Container>
      </Section>

      {/* ── Gallery ──────────────────────────────────────────────── */}
      <Section tone="raised" aria-label="Screenshot gallery">
        <Container>
          <SectionHeading
            eyebrow="Gallery"
            title="Filter by module."
            lead="Select a category to narrow the gallery. Click any card for a larger view with its highlights and the source path inside the application."
          />
          <div className="mt-10">
            <ScreenshotGallery />
          </div>
        </Container>
      </Section>

      {/* ── CTA ──────────────────────────────────────────────────── */}
      <Section aria-label="Request a demo">
        <Container>
          <Reveal>
            <div className="plate flex flex-col gap-6 p-8 sm:flex-row sm:items-center sm:justify-between sm:p-10">
              <div>
                <h2 className="text-2xl leading-tight sm:text-3xl">The best screenshot is your own data.</h2>
                <p className="mt-3 max-w-xl text-sm leading-relaxed text-text-muted">
                  A demonstration walks through your screens with your glass types, your sheet sizes and your margins.
                </p>
              </div>
              <div className="flex shrink-0 flex-wrap gap-3">
                <ButtonLink href="/contact" size="lg" trailing={<ArrowRight />}>
                  Request a Demo
                </ButtonLink>
                <ButtonLink href="/optimization" variant="secondary" size="lg">
                  Optimization detail
                </ButtonLink>
              </div>
            </div>
          </Reveal>
        </Container>
      </Section>
    </>
  );
}

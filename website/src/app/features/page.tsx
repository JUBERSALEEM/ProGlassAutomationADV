import type { Metadata } from "next";
import Link from "next/link";
import { Container, Section } from "@/components/ui/Container";
import { PageHero } from "@/components/layout/PageHero";
import { ArrowRight, ButtonLink } from "@/components/ui/Button";
import { Reveal } from "@/components/ui/Reveal";
import { SectionHeading } from "@/components/ui/SectionHeading";
import { ModuleShowcase } from "@/components/product/ModuleShowcase";
import { AppFrame } from "@/components/product/AppFrame";
import { LiveSurface } from "@/components/product/LiveSurface";
import { Accordion } from "@/components/ui/Accordion";
import { ModuleBrowser } from "@/components/product/ModuleBrowser";
import { modules } from "@/data/modules";
import { featureGroups } from "@/data/features";
import { pricingRules } from "@/data/pricingRules";
import { RouteSchema } from "@/components/seo/RouteSchema";
import { pageMetadata } from "@/lib/metadata";

export const metadata: Metadata = pageMetadata({
  title: "Features — every module in ProGlass Automation",
  description:
    "Inside ProGlass Automation: proforma estimation, SGU/DGU/lamination pricing, job orders, cutting optimization, sheet store, production, delivery and reports.",
  path: "/features",
  keywords: [
    "glass estimation software features",
    "glass ERP modules",
    "proforma invoice software",
    "glass stock management software",
    "glass production management",
  ],
  ogEyebrow: "Every module",
});

export default function FeaturesPage() {
  /* The six modules that decide whether the software fits a business. */
  const showcase = modules.filter((module) =>
    ["proforma", "optimization", "sheet-store", "delivery-note", "job-profitability", "reports-hub"].includes(
      module.id,
    ),
  );

  return (

    <>
    {/* Structured data for this route — see lib/schema.ts. */}
    <RouteSchema
      path="/features"
      title="Features — every module in ProGlass Automation"
      description="Inside ProGlass Automation: proforma estimation, SGU/DGU/lamination pricing, job orders, cutting optimization, sheet store, production, delivery and reports."
    />
      <PageHero
        eyebrow="Features"
        title="Every module, named the way the application names it."
        lead="ProGlass Automation is built as one document chain carried by twenty-five view areas. This page walks the modules that make up the working day: what each one does, which screens it opens, and where in the product it sits."
        banner="product"
        breadcrumb={[
          { label: "Home", href: "/" },
          { label: "Features", href: "/features" },
        ]}
        meta={[
          { label: "Navigation targets", value: `${modules.length} across 9 groups` },
          { label: "Build types", value: "SGU · DGU · LAM · DGU+LAM" },
          { label: "Platform", value: "Windows (WPF)" },
          { label: "Data", value: "Local SQLite" },
        ]}
        actions={
          <>
            <ButtonLink href="/optimization" size="lg" trailing={<ArrowRight />}>
              The optimization engine
            </ButtonLink>
            <ButtonLink href="/screenshots" variant="secondary" size="lg">
              View screenshots
            </ButtonLink>
          </>
        }
      />

      {/* ── Module index ─────────────────────────────────────────── */}
      <Section aria-labelledby="index-heading">
        <Container>
          <SectionHeading
            id="index-heading"
            eyebrow="Index"
            title="The whole application, grouped as its own rail groups it."
            lead="Every navigation target in the product, in the nine groups the sidebar actually draws, filterable by group and searchable by capability. Nothing here has been re-worded for the website."
          />

          <div className="mt-10">
            <ModuleBrowser />
          </div>

          <div className="surface mt-8 flex flex-col gap-4 p-6 sm:flex-row sm:items-center sm:justify-between">
            <p className="max-w-2xl text-sm leading-relaxed text-text-secondary">
              A feature list only means something once a job runs through it. The{" "}
              <Link href="/worked-example" className="link-underline text-brand-200">
                worked example
              </Link>{" "}
              takes one seeded job across the modules above and shows every figure it produces.
            </p>
            <ButtonLink href="/worked-example" variant="secondary" size="sm" trailing={<ArrowRight />}>
              Worked example
            </ButtonLink>
          </div>
        </Container>
      </Section>

      {/* ── In-depth module showcase ─────────────────────────────── */}
      <Section tone="raised" aria-labelledby="showcase-heading">
        <Container>
          <SectionHeading
            id="showcase-heading"
            eyebrow="In depth"
            title="Six modules that carry the business."
            lead="If you only read part of this page, these are the ones that decide whether the software fits: estimation, optimization, stock, dispatch, margin and reporting."
          />
          <div className="mt-14">
            <ModuleShowcase modules={showcase} />
          </div>
        </Container>
      </Section>

      {/*
        ── The pricing engine, at full size ───────────────────────
        The Proforma Invoice preview is a card elsewhere on the site: a small
        frame in a grid of them. This is the same live surface with room to
        work in, because the specification grid and the profit-and-loss screen
        are the two things a shop actually wants to see before it believes
        anything else on this page — and neither reads at card size.

        It opens on step 5 with the specification already priced, the three
        screens with an engine behind them one click away, and the grid's own
        commands working: type a size down and the SQM column stops at the
        0.5 m² floor while the rate holds; type one up past 4 m² and that row
        alone takes the specification's surcharge.
      */}
      <Section tone="sunken" aria-labelledby="pricing-engine-heading" id="pricing-engine">
        <Container>
          <SectionHeading
            id="pricing-engine-heading"
            eyebrow="Proforma invoice — the pricing engine"
            title="Step 5 and Step 9, working."
            lead="The specification grid the quotation is priced from, and the profit-and-loss step that asks whether the price is good enough. Everything below is live: the grid prices as you type, and the profit-and-loss screen reads those same rows."
          />

          <Reveal className="mt-10">
            <AppFrame
              title="Proforma Invoice — Step 5 · Specification"
              railActive="Proforma Invoice"
              tall
              note="Interactive preview — the grid's own commands and the application's own arithmetic, sample data, not a screen capture."
            >
              <LiveSurface
                moduleKey="ProformaInvoice"
                name="Proforma Invoice"
                capabilities={[
                  "Step 5 specification grid",
                  "0.5 m² minimum billable area",
                  "Surcharge rate at 4 m² and above",
                  "Step 9 profit and loss",
                ]}
              />
            </AppFrame>
          </Reveal>

          {/*
            The two rules, stated where the grid is. Same source as the section
            on the worked example — `pricingRules` — so the wording here and the
            arithmetic in the frame above cannot drift apart.
          */}
          <div className="mt-6 grid gap-5 lg:grid-cols-2">
            {pricingRules.map((rule, index) => (
              <Reveal key={rule.id} delay={index * 60}>
                <div className="plate h-full p-6">
                  <div className="flex flex-wrap items-baseline justify-between gap-2">
                    <p className="eyebrow">{rule.label}</p>
                    <p className="font-mono text-sm font-semibold text-brand-200">{rule.value}</p>
                  </div>
                  <h3 className="mt-3 text-lg font-semibold text-text-primary">{rule.headline}</h3>
                  <p className="mt-3 text-sm font-medium leading-relaxed text-text-muted">{rule.body}</p>
                  <p className="mt-4 font-mono text-2xs text-text-faint">{rule.evidence}</p>
                </div>
              </Reveal>
            ))}
          </div>

          <Reveal className="mt-6">
            <div className="flex flex-wrap gap-3">
              <ButtonLink href="/worked-example#pricing-rules" variant="secondary" size="md" trailing={<ArrowRight />}>
                Both rules on a real job
              </ButtonLink>
              <ButtonLink href="/contact" variant="ghost" size="md">
                Request a Demo
              </ButtonLink>
            </div>
          </Reveal>
        </Container>
      </Section>

      {/* ── Feature groups ───────────────────────────────────────── */}
      <Section aria-labelledby="groups-heading">
        <Container>
          <SectionHeading
            id="groups-heading"
            eyebrow="Capability groups"
            title="What the application is good at, grouped by the problem."
            lead="Each group below lists the specific behaviours it covers. The evidence line names the part of the source tree that backs it."
          />

          <div className="mt-10 grid gap-6 lg:grid-cols-2">
            {featureGroups.map((group, index) => (
              <Reveal key={group.id} id={group.id} delay={index * 60} className="surface-raised p-6 sm:p-7">
                <h3 className="text-lg font-semibold text-text-primary">{group.title}</h3>
                <p className="mt-3 text-sm leading-relaxed text-text-muted">{group.lead}</p>
                <ul className="mt-5 space-y-3.5 border-t border-line/60 pt-5">
                  {group.items.map((item) => (
                    <li key={item.title} className="flex gap-3">
                      <span aria-hidden="true" className="mt-2 h-1 w-1 shrink-0 rounded-full bg-brand-400/80" />
                      <div>
                        <p className="text-sm font-medium text-text-secondary">{item.title}</p>
                        <p className="mt-0.5 text-sm leading-relaxed text-text-muted">{item.detail}</p>
                      </div>
                    </li>
                  ))}
                </ul>
                <p className="mono-label mt-5 border-t border-line/60 pt-4">{group.evidence}</p>
              </Reveal>
            ))}
          </div>
        </Container>
      </Section>

      {/* ── What it is not ───────────────────────────────────────── */}
      <Section tone="sunken" aria-labelledby="not-heading">
        <Container>
          <div className="grid gap-10 lg:grid-cols-[minmax(0,0.9fr)_minmax(0,1.1fr)] lg:gap-14">
            <SectionHeading
              id="not-heading"
              eyebrow="Scope"
              title="What ProGlass is not."
              lead="A feature list that never says no is a feature list nobody can trust. These are the things this application does not claim to do."
            />
            <Reveal delay={80}>
              <ul className="space-y-3">
                {[
                  "It is not a cloud service. There is no web dashboard, no tenant and no online account.",
                  "It is not a CRM. Inquiries and customers are recorded so quotations can be built from them, not for pipeline or campaign management.",
                  "It does not file tax returns. The VAT Submission report prepares the figures for a period; filing is done by a person.",
                  "It does not drive your cutting machine directly. The optimizer exports machine-readable files — Bottero .OPT, Intermac .ISO, Lisec .DAT and generic DXF — for the equipment's own software to run.",
                  "There is no mobile application and no mobile interface.",
                  "The AI assistant is read-heavy by design: it can look things up across the business, but it has exactly one permission-gated write.",
                ].map((item) => (
                  <li key={item} className="flex gap-3 rounded-card border border-line/60 bg-ink-900/50 p-4">
                    <span aria-hidden="true" className="mt-2 grid h-3.5 w-3.5 shrink-0 place-items-center">
                      <span className="h-1 w-1 rounded-full bg-state-danger/80" />
                    </span>
                    <p className="text-sm leading-relaxed text-text-muted">{item}</p>
                  </li>
                ))}
              </ul>
            </Reveal>
          </div>
        </Container>
      </Section>

      {/* ── CTA ──────────────────────────────────────────────────── */}
      <Section aria-label="Next steps">
        <Container>
          <Reveal>
            <div className="plate flex flex-col gap-6 p-8 sm:flex-row sm:items-center sm:justify-between sm:p-10">
              <div>
                <h2 className="text-2xl leading-tight sm:text-3xl">Want the same walkthrough on your own data?</h2>
                <p className="mt-3 max-w-xl text-sm leading-relaxed text-text-muted">
                  A demonstration uses a real job: a multi-section quotation, its cutting list and the delivery behind it.
                </p>
              </div>
              <div className="flex shrink-0 flex-wrap gap-3">
                <ButtonLink href="/contact" size="lg" trailing={<ArrowRight />}>
                  Request a Demo
                </ButtonLink>
                <ButtonLink href="/how-it-works" variant="secondary" size="lg">
                  How it works
                </ButtonLink>
              </div>
            </div>
          </Reveal>
        </Container>
      </Section>

      {/* ── FAQ ──────────────────────────────────────────────────── */}
      <Section tone="raised" aria-label="Frequently asked questions">
        <Container>
          <Accordion
            className="measure"
            items={[
              {
                question: "Do I need to buy the optimization engine to use the rest?",
                answer:
                  "No. Basic covers a single user with inquiries and the proforma wizard. Business covers up to five users and the full document chain — sheet store, job orders, delivery notes, tax invoices, statements and reporting — without the cutting-optimization engine or the production-stage modules. Those two are the Factory tier.",
              },
              {
                question: "Can several people work in the same data at once?",
                answer:
                  "The application keeps its data in local SQLite files and can be pointed at a shared database file for a small LAN setup. Concurrent-write behaviour depends on your network share; the application configures every connection with write-ahead logging and a busy timeout so simultaneous readers do not block each other.",
              },
              {
                question: "Is there an export if I want to leave?",
                answer:
                  "Documents and report screens export to PDF, Excel and CSV depending on the module, and backup sets copy the database files themselves. There is no lock-in format: your data is SQLite on your own disk.",
              },
            ]}
          />
        </Container>
      </Section>
    </>
  );
}

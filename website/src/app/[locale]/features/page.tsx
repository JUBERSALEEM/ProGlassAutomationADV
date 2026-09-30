import type { Metadata } from "next";
import { LocaleLink as Link } from "@/components/ui/LocaleLink";
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
import { logFacts } from "@/data/activityLog";
import { stageRoutes, stageRouting } from "@/data/stageQueue";
import { pricingRules } from "@/data/pricingRules";
import { RouteSchema } from "@/components/seo/RouteSchema";
import { pageMetadata } from "@/lib/metadata";
import { translatorFor } from "@/i18n/server";

type PageProps = { params: Promise<{ locale: string }> };

export async function generateMetadata({ params }: PageProps): Promise<Metadata> {
  const t = await translatorFor(params);
  return pageMetadata({
  locale: t.locale,
  title: t("Features — every module in ProGlass Automation"),
  description: t("Inside ProGlass Automation: proforma estimation, SGU/DGU/lamination pricing, job orders, cutting optimization, sheet store, production, delivery and reports."),
  path: "/features",
  keywords: ["glass estimation software features", t("glass ERP modules"), "proforma invoice software", "glass stock management software", "glass production management"],
  ogEyebrow: t("Every module"),
  });
}

export default async function FeaturesPage({ params }: PageProps) {
  const t = await translatorFor(params);
  const locale = t.locale;
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
      locale={locale}
      path="/features"
      title={t("Features — every module in ProGlass Automation")}
      description={t("Inside ProGlass Automation: proforma estimation, SGU/DGU/lamination pricing, job orders, cutting optimization, sheet store, production, delivery and reports.")}
    />
      <PageHero
      t={t}
        eyebrow={t("Features")}
        title={t("Every module, named the way the application names it.")}
        lead={t("ProGlass Automation is built as one document chain carried by twenty-five view areas. This page walks the modules that make up the working day: what each one does, which screens it opens, and where in the product it sits.")}
        banner="product"
        breadcrumb={[
          { label: t("Home"), href: "/" },
          { label: t("Features"), href: "/features" },
        ]}
        meta={[
          { label: t("Navigation targets"), value: `${modules.length} across 9 groups` },
          { label: t("Build types"), value: t("SGU · DGU · LAM · DGU+LAM") },
          { label: t("Platform"), value: t("Windows (WPF)") },
          { label: t("Data"), value: t("Local SQLite") },
        ]}
        actions={
          <>
            <ButtonLink href="/optimization" size="lg" trailing={<ArrowRight />}>
          {t("The optimization engine")}
        </ButtonLink>
            <ButtonLink href="/screenshots" variant="secondary" size="lg">
          {t("View screenshots")}
        </ButtonLink>
          </>
        }
      />

      {/* ── Module index ─────────────────────────────────────────── */}
      <Section aria-labelledby="index-heading">
        <Container>
          <SectionHeading
            id="index-heading"
            eyebrow={t("Index")}
            title={t("The whole application, grouped as its own rail groups it.")}
            lead={t("Every navigation target in the product, in the nine groups the sidebar actually draws, filterable by group and searchable by capability. Nothing here has been re-worded for the website.")}
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
          {t("Worked example")}
        </ButtonLink>
          </div>
        </Container>
      </Section>

      {/* ── In-depth module showcase ─────────────────────────────── */}
      <Section tone="raised" aria-labelledby="showcase-heading">
        <Container>
          <SectionHeading
            id="showcase-heading"
            eyebrow={t("In depth")}
            title={t("Six modules that carry the business.")}
            lead={t("If you only read part of this page, these are the ones that decide whether the software fits: estimation, optimization, stock, dispatch, margin and reporting.")}
          />
          <div className="mt-14">
            <ModuleShowcase
      t={t} modules={showcase} />
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
            eyebrow={t("Proforma invoice — the pricing engine")}
            title={t("Step 5 and Step 9, working.")}
            lead={t("The specification grid the quotation is priced from, and the profit-and-loss step that asks whether the price is good enough. Everything below is live: the grid prices as you type, and the profit-and-loss screen reads those same rows.")}
          />

          <Reveal className="mt-10">
            <AppFrame
      t={t}
              title={t("Proforma Invoice — Step 5 · Specification")}
              railActive="Proforma Invoice"
              tall
              note={t("Interactive preview — the grid's own commands and the application's own arithmetic, sample data, not a screen capture.")}
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
                    <p className="eyebrow">{t(rule.label)}</p>
                    <p className="font-mono text-sm font-semibold text-brand-200">{rule.value}</p>
                  </div>
                  <h3 className="mt-3 text-lg font-semibold text-text-primary">{rule.headline}</h3>
                  <p className="mt-3 text-sm font-medium leading-relaxed text-text-muted">{t(rule.body)}</p>
                  <p className="mt-4 font-mono text-2xs text-text-faint">{t(rule.evidence)}</p>
                </div>
              </Reveal>
            ))}
          </div>

          <Reveal className="mt-6">
            <div className="flex flex-wrap gap-3">
              <ButtonLink href="/worked-example#pricing-rules" variant="secondary" size="md" trailing={<ArrowRight />}>
          {t("Both rules on a real job")}
        </ButtonLink>
              <ButtonLink href="/contact" variant="ghost" size="md">
          {t("Request a Demo")}
        </ButtonLink>
            </div>
          </Reveal>
        </Container>
      </Section>

      {/*
        ── Logs & Activity, at full size ───────────────────────────
        The rail calls this module "Logs & Activity" and navigates with
        CommandParameter="Diagnostics". It is two services underneath — the
        application log (`AppLogService`) and the business audit trail
        (`AuditService`) — and the surface here is the real one: the log
        terminal with five severity levels and the last 500 entries, the error
        and warning counters, and the audit trail filtered by the canonical
        section names.

        It is given full size for the same reason the pricing engine is: a log
        is read line by line, and the correlation ID that ties one business
        operation together only means anything when the lines are legible.
      */}
      <Section aria-labelledby="logs-activity-heading" id="showcase-logs-activity">
        <Container>
          <SectionHeading
            id="logs-activity-heading"
            eyebrow={t("System — Logs & Activity")}
            title={t("What it did, and who changed what.")}
            lead={t("Two services, deliberately not one. The application log records what happened — startup, licence, seeding, production, errors — and the audit trail records the business events, with the before and after of every field. The correlation ID on a traceable entry follows one operation from the inquiry to the statement, across modules and across days.")}
          />

          <Reveal className="mt-10">
            <AppFrame
      t={t}
              title={t("Logs & Activity — Diagnostics")}
              railActive="Logs & Activity"
              tall
              note={t("Interactive preview — filter by severity, switch to the audit trail and filter by section, on sample rows built from this repository's seeded workflow.")}
            >
              <LiveSurface
                moduleKey="Diagnostics"
                name="Logs & Activity"
                capabilities={[
                  "Rolling log, five severity levels",
                  "Error and warning counters",
                  "Open log folder on disk",
                  "Audit trail by canonical section",
                  "Correlation ID per operation",
                ]}
              />
            </AppFrame>
          </Reveal>

          <div className="mt-6 grid gap-5 lg:grid-cols-3">
            {logFacts.map((fact, index) => (
              <Reveal key={fact.id} delay={index * 60}>
                <div className="plate h-full p-6">
                  <p className="eyebrow">{t(fact.label)}</p>
                  <h3 className="mt-3 text-lg font-semibold text-text-primary">{fact.headline}</h3>
                  <p className="mt-3 text-sm font-medium leading-relaxed text-text-muted">{t(fact.body)}</p>
                  <p className="mt-4 font-mono text-2xs text-text-faint">{t(fact.evidence)}</p>
                </div>
              </Reveal>
            ))}
          </div>

          <Reveal className="mt-6">
            <div className="flex flex-wrap gap-3">
              <ButtonLink href="/features#system-center" variant="secondary" size="md" trailing={<ArrowRight />}>
          {t("System Center")}
        </ButtonLink>
              <ButtonLink href="/contact" variant="ghost" size="md">
          {t("Request a Demo")}
        </ButtonLink>
            </div>
          </Reveal>
        </Container>
      </Section>

      {/*
        ── The production floor, at full size ──────────────────────
        Eight separate stage pages, four bars of shared chrome, and one rule
        holding them together: only the pieces a stage completes arrive in the
        next stage, and a floor write is clamped to what arrived. The preview
        opens on Cutting — where every piece enters, on the full Step-5
        envelope — and any chip in the chain bar opens that stage instead.

        The waiting-upstream strip and the done-piece editor are both real
        behaviour rather than decoration: a stage with nothing arrived says so,
        and typing a done quantity larger than the arrived quantity clamps, the
        way `SetDoneQtyAsync` does.
      */}
      <Section tone="sunken" aria-labelledby="production-floor-heading" id="showcase-production-floor">
        <Container>
          <SectionHeading
            id="production-floor-heading"
            eyebrow={t("Production — the floor")}
            title={t("Eight stages, one chain, and a clamp that holds it together.")}
            lead={t("Production runs exclusively from job orders, and a job order only exists for a confirmed proforma — a draft never reaches the floor. From there the job is distributed across the eight stages: every piece enters at Cutting, and only the pieces a station completes arrive at the next one. A station cannot record work it never received.")}
          />

          <Reveal className="mt-10">
            <AppFrame
      t={t}
              title={t("Production — stage queue")}
              railActive="Production"
              tall
              note={t("Interactive preview — pick a stage from the chain bar, release a job order (and watch a draft be refused), and try to record more pieces than arrived.")}
            >
              <LiveSurface
                moduleKey="Cutting"
                name="Cutting"
                capabilities={["Stage queue", "Shared production chrome", "Piece flow"]}
              />
            </AppFrame>
          </Reveal>

          <div className="mt-6 grid gap-5 lg:grid-cols-2">
            <Reveal>
              <div className="plate h-full p-6">
                <p className="eyebrow">{t("Routing by build family")}</p>
                <ul className="mt-4 space-y-3">
                  {stageRoutes.map((route) => (
                    <li key={route.family}>
                      <p className="flex flex-wrap items-baseline gap-2">
                        <span className="font-mono text-sm font-semibold text-brand-200">{route.family}</span>
                        <span className="font-mono text-2xs text-text-faint">{route.carries}</span>
                      </p>
                      <p className="mt-1 text-sm font-medium leading-relaxed text-text-muted">{route.route}</p>
                    </li>
                  ))}
                </ul>
                <p className="mt-4 font-mono text-2xs text-text-faint">{t("Views/Production/README.md — scope lock: four Step-4 families, no triple glazing")}</p>
              </div>
            </Reveal>

            <Reveal delay={60}>
              <div className="plate h-full p-6">
                <p className="eyebrow">{t("Step 6 charges that raise floor work")}</p>
                <table className="mt-4 w-full border-collapse text-left">
                  <caption className="sr-only">{t("Production charges and the stage each one arrives at.")}</caption>
                  <thead>
                    <tr className="border-b border-line/60">
                      <th scope="col" className="pb-1.5 font-mono text-2xs uppercase tracking-[0.1em] text-text-faint font-medium">{t("Charge")}</th>
                      <th scope="col" className="pb-1.5 font-mono text-2xs uppercase tracking-[0.1em] text-text-faint font-medium">{t("Arrives at")}</th>
                      <th scope="col" className="pb-1.5 text-right font-mono text-2xs uppercase tracking-[0.1em] text-text-faint font-medium">{t("Counted in")}</th>
                    </tr>
                  </thead>
                  <tbody>
                    {stageRouting.map((entry) => (
                      <tr key={entry.charge} className="border-b border-line/30 last:border-0">
                        <td className="py-1 font-mono text-xs text-text-secondary">{entry.charge}</td>
                        <td className="py-1 text-xs font-medium text-text-muted">{entry.stage}</td>
                        <td className="py-1 text-right text-xs font-medium text-text-faint">{entry.quantity}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
                <p className="mt-4 font-mono text-2xs text-text-faint">{t("Services/Production/ProductionDistributionEngine.cs — the plan is pure and deterministic")}</p>
              </div>
            </Reveal>
          </div>

          <Reveal className="mt-6">
            <div className="flex flex-wrap gap-3">
              <ButtonLink href="/how-it-works" variant="secondary" size="md" trailing={<ArrowRight />}>
          {t("Where the floor sits in the chain")}
        </ButtonLink>
              <ButtonLink href="/contact" variant="ghost" size="md">
          {t("Request a Demo")}
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
            eyebrow={t("Capability groups")}
            title={t("What the application is good at, grouped by the problem.")}
            lead={t("Each group below lists the specific behaviours it covers. The evidence line names the part of the source tree that backs it.")}
          />

          <div className="mt-10 grid gap-6 lg:grid-cols-2">
            {featureGroups.map((group, index) => (
              <Reveal key={group.id} id={group.id} delay={index * 60} className="surface-raised p-6 sm:p-7">
                <h3 className="text-lg font-semibold text-text-primary">{t(group.title)}</h3>
                <p className="mt-3 text-sm leading-relaxed text-text-muted">{t(group.lead)}</p>
                <ul className="mt-5 space-y-3.5 border-t border-line/60 pt-5">
                  {group.items.map((item) => (
                    <li key={item.title} className="flex gap-3">
                      <span aria-hidden="true" className="mt-2 h-1 w-1 shrink-0 rounded-full bg-brand-400/80" />
                      <div>
                        <p className="text-sm font-medium text-text-secondary">{t(item.title)}</p>
                        <p className="mt-0.5 text-sm leading-relaxed text-text-muted">{t(item.detail)}</p>
                      </div>
                    </li>
                  ))}
                </ul>
                <p className="mono-label mt-5 border-t border-line/60 pt-4">{t(group.evidence)}</p>
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
              eyebrow={t("Scope")}
              title={t("What ProGlass is not.")}
              lead={t("A feature list that never says no is a feature list nobody can trust. These are the things this application does not claim to do.")}
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
      <Section aria-label={t("Next steps")}>
        <Container>
          <Reveal>
            <div className="plate flex flex-col gap-6 p-8 sm:flex-row sm:items-center sm:justify-between sm:p-10">
              <div>
                <h2 className="text-2xl leading-tight sm:text-3xl">{t("Want the same walkthrough on your own data?")}</h2>
                <p className="mt-3 max-w-xl text-sm leading-relaxed text-text-muted">{t("A demonstration uses a real job: a multi-section quotation, its cutting list and the delivery behind it.")}</p>
              </div>
              <div className="flex shrink-0 flex-wrap gap-3">
                <ButtonLink href="/contact" size="lg" trailing={<ArrowRight />}>
          {t("Request a Demo")}
        </ButtonLink>
                <ButtonLink href="/how-it-works" variant="secondary" size="lg">
          {t("How it works")}
        </ButtonLink>
              </div>
            </div>
          </Reveal>
        </Container>
      </Section>

      {/* ── FAQ ──────────────────────────────────────────────────── */}
      <Section tone="raised" aria-label={t("Frequently asked questions")}>
        <Container>
          <Accordion
            className="measure"
            items={[
              {
                question: t("Do I need to buy the optimization engine to use the rest?"),
                answer: t("No. Basic covers a single user with inquiries and the proforma wizard. Business covers up to five users and the full document chain — sheet store, job orders, delivery notes, tax invoices, statements and reporting — without the cutting-optimization engine or the production-stage modules. Those two are the Factory tier."),
              },
              {
                question: t("Can several people work in the same data at once?"),
                answer: t("The application keeps its data in local SQLite files and can be pointed at a shared database file for a small LAN setup. Concurrent-write behaviour depends on your network share; the application configures every connection with write-ahead logging and a busy timeout so simultaneous readers do not block each other."),
              },
              {
                question: t("Is there an export if I want to leave?"),
                answer: t("Documents and report screens export to PDF, Excel and CSV depending on the module, and backup sets copy the database files themselves. There is no lock-in format: your data is SQLite on your own disk."),
              },
            ]}
          />
        </Container>
      </Section>
    </>
  );
}

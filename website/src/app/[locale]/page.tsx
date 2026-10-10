import type { Metadata } from "next";
import Image from "next/image";
import { LocaleLink as Link } from "@/components/ui/LocaleLink";
import { Container, Section } from "@/components/ui/Container";
import { ArrowRight, ButtonLink } from "@/components/ui/Button";
import { Badge, IndexChip } from "@/components/ui/Badge";
import { Reveal } from "@/components/ui/Reveal";
import { SectionHeading } from "@/components/ui/SectionHeading";
import { StatBadge, StatCounter } from "@/components/ui/Stat";
import { Accordion } from "@/components/ui/Accordion";
import { GlassCard, GlassDivider, GlassMetric, GlassMetricRow } from "@/components/ui/Glass";
import { ProductHero } from "@/components/hero/ProductHero";
import { DeveloperStory } from "@/components/hero/DeveloperStory";
import { Spotlight } from "@/components/ui/Spotlight";
import { GlassCubeEmblem } from "@/components/brand/GlassCubeEmblem";
import { WorkflowRail } from "@/components/product/WorkflowRail";
import { AppearancePreview } from "@/components/layout/AppearancePreview";
import { ModuleShowcase } from "@/components/product/ModuleShowcase";
import { ProductSlider } from "@/components/product/ProductSlider";
import { SheetPlanStrip } from "@/components/optimization/SheetPlanStrip";
import { ScreenshotFrame } from "@/components/gallery/ScreenshotFrame";
import { cn } from "@/lib/cn";
import { groupNotes, moduleGroups, modules, modulesByGroup } from "@/data/modules";
import { groupIcons } from "@/data/groupIcons";
import { Icon } from "@/components/ui/Icon";
import { glass3d } from "@/data/assets";
import { featureGroups } from "@/data/features";
import { faq } from "@/data/faq";
import { quotationTotals, seedParties, seededDocuments } from "@/data/workedExample";
import { screenshots, screenshotById } from "@/data/screenshots";
import { RouteSchema } from "@/components/seo/RouteSchema";
import { pageMetadata } from "@/lib/metadata";
import { site } from "@/lib/site";
import type { Translator } from "@/i18n/core";
import { translatorFor } from "@/i18n/server";
import { PageOverlay } from "@/i18n/PageOverlay";

type PageProps = { params: Promise<{ locale: string }> };

export async function generateMetadata({ params }: PageProps): Promise<Metadata> {
  const t = await translatorFor(params);
  return pageMetadata({
  locale: t.locale,
  title: t("Glass business management software for Windows"),
  description: t("Windows desktop software for glass fabrication: SGU, DGU and lamination estimation, cutting optimization, sheet store, production, delivery and invoices."),
  path: "/",
  keywords: ["glass estimation software", t("glass ERP software"), "glass cutting optimization software", t("DGU calculator"), t("SGU calculator"), "glass stock management", "glass production management"],
  });
}

const heroModules = modules.filter((m) =>
  ["proforma", "optimization", "sheet-store", "delivery-note"].includes(m.id),
);

const sliderShots = [
  screenshotById("glass-calculator-sgu"),
  screenshotById("glass-calculator-dgu"),
  screenshotById("glass-calculator-lam"),
  screenshotById("glass-calculator-dgulam"),
  screenshotById("proforma-wizard"),
].filter((s): s is NonNullable<typeof s> => Boolean(s));

export default async function HomePage({ params }: PageProps) {
  const t = await translatorFor(params);
  const locale = t.locale;
  return (
    <PageOverlay locale={t.locale} route="">
    <>
    {/* Structured data for this route — see lib/schema.ts. */}
    <RouteSchema
      locale={locale}
      path="/"
      title={t("Glass business management software for Windows")}
      description={t("Windows desktop software for glass fabrication: SGU, DGU and lamination estimation, cutting optimization, sheet store, production, delivery and invoices.")}
      // The questions this page actually renders, so the markup and the page agree.
      faq={faq.filter((item) => item.group === "Product").map(({ question, answer }) => ({ question, answer }))}
    />
      <ProductHero
      t={t} />

      {/* ═══ 01b · The facts strip ═══════════════════════════════════
          Four facts about what kind of software this is. It is a band of its
          own rather than a row inside the hero because it is the first thing a
          visitor reads after the claim, and a claim followed immediately by
          four constraints reads as honest in a way a claim followed by four
          more claims does not. */}
      <div className="band-solid relative overflow-hidden">
        <div aria-hidden="true" className="pointer-events-none absolute inset-0 bg-sheen-violet" />
        <Container className="relative">
          <ul className="grid divide-line/50 sm:grid-cols-2 sm:divide-x lg:grid-cols-4">
            {[
              { value: t("Windows desktop"), label: t("Installed software, not a browser tab") },
              { value: t("Local SQLite"), label: t("Your data on your machine, 16 databases") },
              { value: t("No account"), label: t("Nothing to sign up for, nothing to log into") },
              { value: `${site.locales} languages`, label: t("Interface translated, not just labelled") },
            ].map((fact, index) => (
              <li
                key={t(fact.value)}
                className={cn(
                  "flex flex-col gap-1 py-6 sm:px-6 lg:px-8",
                  index === 0 && "sm:pl-0",
                )}
              >
                <span className="figure text-lg">{t(fact.value)}</span>
                <span className="mt-1 text-xs font-medium leading-relaxed text-text-muted">{t(fact.label)}</span>
              </li>
            ))}
          </ul>
        </Container>
      </div>

      {/* ═══ 01c · The developer story ═════════════════════════════════
          The opening of the page ends here, and it ends on the person rather
          than on the product: the claim, the constraints, and then who is
          accountable for both. Everything on it is drawn from the dated
          working documents at the root of the repository — see the note in
          `src/data/developerHistory.ts`. */}
      <DeveloperStory t={t} />

      {/* ═══ 02 · What ProGlass is ═══════════════════════════════════ */}
      <Section tone="raised" id="what" aria-labelledby="what-heading">
        <Container>
          <div className="grid gap-12 lg:grid-cols-[minmax(0,1fr)_minmax(0,1.15fr)] lg:gap-16">
            <div>
              <SectionHeading
                id="what-heading"
                eyebrow={t("Built for glass operations")}
                title={t("From the first inquiry to the final statement, without re-keying the job.")}
                lead={t("ProGlass Automation is a Windows desktop application for glass fabrication. It holds the parts of the business that normally live in separate spreadsheets — the quotation, the glass build-up, the cutting plan, the stock, the deliveries and the invoices — and makes each one carry the last one forward.")}
              />
              <Reveal delay={120} className="mt-8 flex flex-wrap gap-3">
                <ButtonLink href="/how-it-works" size="md" trailing={<ArrowRight />}>
          {t("See how it works")}
        </ButtonLink>
                <ButtonLink href="/screenshots" variant="secondary" size="md">
          {t("View the interface")}
        </ButtonLink>
              </Reveal>
            </div>

            <Reveal delay={80} className="grid gap-6 sm:grid-cols-2">
              {[
                {
                  title: t("For the estimator"),
                  body: t("Price SGU, DGU, laminated and combined build-ups against real sheet costs, then turn the quotation into a job order without retyping it."),
                },
                {
                  title: t("For the factory"),
                  body: t("Cut from optimized plans with safety edges and kerf honoured, work the eight production stages, and know what is still owed on every job."),
                },
                {
                  title: t("For the office"),
                  body: t("Ship in as many movements as the customer needs, invoice against the delivery, and read a statement that adds up the same way twice."),
                },
                {
                  title: t("For the owner"),
                  body: t("Margins that do not move after the fact: the profit snapshot is written once and reported forever, and a stale price basis is flagged."),
                },
              ].map((card, index) => (
                <div key={`${t(card.title)}-${index}`} className="surface p-5">
                  <p className="eyebrow mb-2">{String(index + 1).padStart(2, "0")}</p>
                  <h3 className="text-base font-semibold text-text-primary">{t(card.title)}</h3>
                  <p className="mt-2 text-sm leading-relaxed text-text-muted">{t(card.body)}</p>
                </div>
              ))}
            </Reveal>
          </div>
        </Container>
      </Section>

      {/* ═══ 03 · The chain ══════════════════════════════════════════ */}
      <Section id="workflow" aria-labelledby="workflow-heading">
        <Container>
          <SectionHeading
            id="workflow-heading"
            eyebrow={t("The business chain")}
            title={t("Every arrow is a conversion, not a fresh entry.")}
            lead={t("The application stores documents, not summaries. A quotation becomes a job order becomes a delivery becomes an invoice — and each step keeps the audit record of the one before it.")}
          />

          <div className="mt-12 grid gap-10 lg:grid-cols-[minmax(0,1fr)_minmax(0,1.35fr)] lg:gap-14">
            <div className="lg:sticky lg:top-28 lg:self-start">
              <ChainDiagram t={t} />
              <p className="mt-6 text-xs leading-relaxed text-text-faint font-medium">{t("Status vocabularies and handoff guarantees are taken from the application's own workflow documentation and the transition code that enforces them.")}</p>
            </div>
            <WorkflowRail
      t={t} />
          </div>
        </Container>
      </Section>

      {/* ═══ 04 · Operational Scale ═════════════════════════════════ */}
      <Section tone="sunken" aria-labelledby="scale-heading">
        <Container>
          <div className="flex flex-col gap-8 lg:flex-row lg:items-end lg:justify-between">
            <SectionHeading
              id="scale-heading"
              eyebrow={t("Operational Scale")}
              title={t("Engineered for complete factory operations.")}
              lead={t("ProGlass Automation connects every department into one integrated workflow — from estimating multi-layer architectural glass to floor-level production queues, cutting optimization, and multi-drop delivery.")}
            />
            <p className="max-w-sm text-xs leading-relaxed text-text-faint font-medium">
              {t("Unified Windows desktop software covering nine integrated modules and 15 business languages.")}
            </p>
          </div>

          <div className="mt-10 grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
            <StatCounter
              value={9}
              label={t("Workflow stages")}
              hint={t("Inquiry, estimation, proforma, job order, production, optimization, delivery, invoicing, and reporting.")}
            />
            <StatCounter
              value={4}
              label={t("Glass calculators")}
              hint={t("SGU, DGU, laminated and DGU + laminated build-ups, priced pane by pane.")}
            />
            <StatCounter
              value={8}
              label={t("Production stations")}
              hint={t("Workstation queues from cutting and edge processing through tempering and doubling.")}
            />
            <StatCounter
              value={14}
              label={t("Analytical reports")}
              hint={t("Job profitability, quotation conversion, delivery balance, and VAT submission.")}
            />
          </div>

          <div className="mt-6 flex flex-wrap items-center justify-between gap-4 rounded-xl border border-line-strong/60 bg-ink-900/50 p-4">
            <span className="text-xs text-text-muted">
              {t("Looking for technical codebase metrics, SQLite architecture, and test statistics?")}
            </span>
            <Link
              href="/developer"
              className="text-xs font-semibold text-brand-300 hover:text-brand-200 inline-flex items-center gap-1.5 transition-colors"
            >
              {t("View developer & engineering architecture")}
              <ArrowRight />
            </Link>
          </div>
        </Container>
      </Section>

      {/* ═══ 04b · Worked example ════════════════════════════════════ */}
      {/*
        The scale section above answers "how much is in it". This one answers
        "what does it look like on a real job" — and every figure in it is read
        from the workflow the application seeds into an empty database, not
        written for the page. See data/workedExample.ts, which fails the build
        if these numbers ever stop matching the seeder.
      */}
      <Section aria-labelledby="worked-heading">
        <Container>
          <div className="grid gap-10 lg:grid-cols-[minmax(0,0.95fr)_minmax(0,1.05fr)] lg:gap-16">
            <div>
              <SectionHeading
                id="worked-heading"
                eyebrow={t("Worked example")}
                title={t("Here is one job, with its real figures.")}
                lead={t("The application can seed a single complete demonstration workflow into an empty database — one enquiry, one quotation, one job order, one partial delivery, one tax invoice. These are its numbers, read straight out of the seeder rather than chosen for a page.")}
              />

              <ol className="mt-8 flex flex-wrap items-center gap-2">
                {seededDocuments.map((document, index) => (
                  <li key={document.key} className="flex items-center gap-2">
                    {index > 0 ? (
                      <span aria-hidden="true" className="text-text-faint">
                        →
                      </span>
                    ) : null}
                    <span className="rounded-full border border-line-strong/70 px-3 py-1.5 font-mono text-[0.75rem] text-text-secondary">
                      {document.example}
                      <span className="ml-1.5 text-text-faint">{t(document.status)}</span>
                    </span>
                  </li>
                ))}
              </ol>

              <p className="mt-6 text-sm leading-relaxed text-text-muted">{t("{v1}· {v2}· contract {v3}. The quotation is priced in three specification groups and the delivery is deliberately partial, so the balance workflow is visible rather than described.", { v1: t(seedParties.customer.name), v2: t(seedParties.project.name), v3: seedParties.project.contract })}</p>

              <div className="mt-8 flex flex-wrap gap-3">
                <ButtonLink href="/worked-example" size="lg" trailing={<ArrowRight />}>
          {t("Walk through the job")}
        </ButtonLink>
                <ButtonLink href="/how-it-works" variant="secondary" size="lg">
          {t("How the chain works")}
        </ButtonLink>
              </div>

              <p className="mt-5 max-w-xl text-xs leading-relaxed text-text-faint font-medium">{t("The rates in the seeded job are the sample dataset's own numbers. ProGlass Automation publishes no price list for glass, and nothing on this site is a market rate.")}</p>
            </div>

            <Reveal delay={60}>
              <div className="grid gap-4 sm:grid-cols-2">
                <StatBadge
                  value={String(quotationTotals.lines)}
                  label={t("Quotation lines")}
                  hint={t("Three specification groups, SGU and DGU.")}
                />
                <StatBadge
                  value={String(quotationTotals.pieces)}
                  label={t("Pieces of glass")}
                  hint={t.template`${quotationTotals.squareMetres.toLocaleString("en-AE", { minimumFractionDigits: 2, maximumFractionDigits: 2 })} m² of glass.`}
                />
                <StatBadge
                  value={`AED ${quotationTotals.subtotal.toLocaleString("en-AE", { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`}
                  label={t("Quoted before VAT")}
                  hint={t("Line values rounded individually, then summed.")}
                />
                <StatBadge
                  value={`AED ${quotationTotals.grandTotal.toLocaleString("en-AE", { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`}
                  label={t("VAT inclusive")}
                  hint={t("5% through the application's one VAT calculator.")}
                />
              </div>
            </Reveal>
          </div>
        </Container>
      </Section>

      {/* ═══ 05 · The calculators ════════════════════════════════════ */}
      <Section aria-labelledby="calculators-heading">
        <Container>
          <SectionHeading
            id="calculators-heading"
            eyebrow={t("Glass calculators")}
            title={t("SGU, DGU, lamination and DGU + lamination.")}
            lead={t("Four build-ups, priced against the sheet costs and rates you actually buy at. Each one carries thickness, colour, treatment and processing per pane rather than assuming a single panel for the whole quotation.")}
          />

          <Reveal delay={80} className="mt-10">
            <ProductSlider
              ariaLabel="Glass build-up calculators"
              slides={sliderShots.map((shot) => ({
                id: shot.id,
                label: shot.title.replace(/ calculator$/, ""),
                caption: shot.tags.join(" · "),
                content: <ScreenshotFrame
      t={t} shot={shot} />,
              }))}
            />
          </Reveal>

          <Reveal delay={120} className="mt-10 grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
            {[
              { key: "SGU", body: t("Single glazing: thickness, colour, treatment, edge polish and panel extras.") },
              { key: "DGU", body: t("Double glazing with a 6–32 mm air space, six spacer systems, argon gas, U-Insert, fanhole, cutouts and holes.") },
              { key: "LAM", body: t("Lamination with PVB thickness and per-pane colour and treatment.") },
              { key: "DGU + LAM", body: t("A laminated pane inside a double-glazed unit, priced as one build-up.") },
            ].map((item) => (
              <div key={item.key} className="surface p-5">
                <p className="font-mono text-sm font-semibold text-brand-300">{item.key}</p>
                <p className="mt-2 text-sm leading-relaxed text-text-muted">{t(item.body)}</p>
              </div>
            ))}
          </Reveal>
        </Container>
      </Section>

      {/* ═══ 06 · Proforma ═══════════════════════════════════════════ */}
      <Section tone="raised" aria-labelledby="proforma-heading">
        <Container>
          <div className="grid gap-12 lg:grid-cols-[minmax(0,1.05fr)_minmax(0,1fr)] lg:gap-14">
            <div>
              <SectionHeading
                id="proforma-heading"
                eyebrow={t("Proforma invoice")}
                title={t("Nine steps from a company card to a stored profit.")}
                lead={t("Step 9 is the only place money is computed in the whole application, and it answers one question — is the given price above target? Everything downstream — job profitability, statements, reports — reads the snapshot it writes.")}
              />

              <ol className="mt-8 space-y-2">
                {[
                  ["Company", "All eight legal-entity fields copied from Company Center in one action."],
                  ["Client", "Customer, TRN and billing profile with duplicate detection."],
                  ["Project", "Project identifier, contract reference and site mapping."],
                  ["Pricing", "Rates per build type, sheet selection, air space and processing."],
                  [
                    "Specification grid (step 5)",
                    "# · glass ref · W1 H1 W2 H2 · qty · SQM · total SQM · rate · value, one block per specification, with BOQ import, find and replace, Excel paste, bulk row generation and QUICK / OPTIMIZE / VIEW RESULT on the bar — then the grand total summary rolled up under it. Every panel bills at least 0.5 m², and one that reaches 4 m² takes the specification's surcharge rate.",
                  ],
                  ["Charges", "Predefined charges, wastage and custom rows."],
                  ["Summary", "VAT 5% round-safe totals and discount."],
                  ["Attachments", "Evidence chain with SHA-256 manifest deduplication."],
                  [
                    "Profit & loss",
                    "Is the given price above target? Required target price against the given price, glass cost at buy prices, then services, sheets and a spec-by-spec check.",
                  ],
                ].map(([step, detail], index) => (
                  <Reveal key={step} as="li" delay={index * 40}>
                    <div className="flex gap-4 rounded-card border border-line/60 bg-ink-900/50 p-3.5 transition-colors duration-300 hover:border-line-strong">
                      <IndexChip className="h-7 w-7 text-[0.75rem]">{String(index + 1).padStart(2, "0")}</IndexChip>
                      <div className="min-w-0">
                        <p className="text-sm font-medium text-text-primary">{t(step ?? "")}</p>
                        <p className="mt-0.5 text-xs leading-relaxed text-text-muted">{t(detail ?? "")}</p>
                      </div>
                    </div>
                  </Reveal>
                ))}
              </ol>

              <div className="mt-8 flex flex-wrap gap-3">
                {/*
                  Straight to the full-size frame on /features: the small card
                  further down this page shows the same screen, and a visitor
                  who wants the grid wants it with room to work in.
                */}
                <ButtonLink href="/features#pricing-engine" variant="secondary" size="md" trailing={<ArrowRight />}>
          {t("Work the grid")}
        </ButtonLink>
                <ButtonLink href="/how-it-works" variant="ghost" size="md">
          {t("Walk the workflow")}
        </ButtonLink>
              </div>
            </div>

            <Reveal delay={100} className="lg:sticky lg:top-28 lg:self-start">
              <div className="plate p-5">
                <div className="flex flex-wrap items-center justify-between gap-3">
                  <p className="font-mono text-2xs uppercase tracking-[0.16em] text-text-faint font-medium">
                    Step 9 · Profit &amp; loss
                  </p>
                  <Badge tone="accent">{t("Single pricing engine")}</Badge>
                </div>

                <dl className="mt-5 space-y-3">
                  {[
                    ["Required target price", "Base price × (1 + expected margin) — 15% on a fresh install"],
                    ["Price balance", "Given against required, with the difference as a value"],
                    ["Sheet cost basis", "Buy price per SQM from Sheet Store, pinned at quote time"],
                    ["Production rows", "Built from the specification's own provenance, per group"],
                    ["Wastage", "Charges rows, colours auto-imported from the spec grid"],
                    ["VAT", "5%, applied with one rounding rule for the whole application"],
                    ["Downstream", "Job profitability, statements and reports read this snapshot"],
                  ].map(([term, detail]) => (
                    <div key={term} className="rounded-card border border-line/60 bg-ink-950/50 p-3.5">
                      <dt className="text-xs font-medium uppercase tracking-[0.1em] text-text-faint">{t(term ?? "")}</dt>
                      <dd className="mt-1 text-sm leading-relaxed text-text-secondary">{t(detail ?? "")}</dd>
                    </div>
                  ))}
                </dl>

                <p className="mt-5 border-t border-line/60 pt-4 text-xs leading-relaxed text-text-faint font-medium">{t("An ERP that quietly recalculates yesterday's quote with today's glass prices is not an ERP. When sheet-store prices move, the affected row is flagged rather than silently rescored.")}</p>
              </div>
            </Reveal>
          </div>
        </Container>
      </Section>

      {/* ═══ 07 · Optimization ═══════════════════════════════════════
          The primary card of the page.

          Everything that makes the engine worth a visitor's attention is in
          one surface: what it is, what it guarantees, the four numbers that
          describe its search, and a real cutting plan it produced — drawn
          from the same packer the module previews use, proved cuttable at
          build time. The four numbers used to be four separate cards below
          the section, which is the shape this redesign exists to remove: a
          headline, then four boxes repeating its subject, then a diagram.

          The compact strip rather than the full demonstration: one box per
          plate, side by side, so a multi-plate job reads at a glance instead
          of pushing the rest of the page below the fold. The animated
          demonstration still runs on /optimization.
      */}
      <Section aria-labelledby="optimization-heading">
        <Container>
          <Reveal>
            <GlassCard
              tier="primary"
              head={{
                id: "optimization-heading",
                titleAs: "h2",
                eyebrow: t("Optimization"),
                title: t("Turn stock plates and a cutting list into a plan the table can actually cut."),
                lead: t("A guillotine packer that honours your safety edges and blade kerf, records the cut sequence as it places pieces, then proves the plan before it can be exported."),
                actions: (
                  <ButtonLink href="/optimization" size="lg" trailing={<ArrowRight />}>
                    {t("Open the engine")}
                  </ButtonLink>
                ),
              }}
            >
              <GlassMetricRow>
                {[
                  { label: t("Search families"), value: t("5 + Auto"), hint: "From a quick area-shelf pass to the widest MaxRects search." },
                  { label: t("Plate patterns"), value: "6", hint: "Tie-breaks that never buy a preference by adding a sheet." },
                  { label: t("Cutting complexity"), value: "6 levels", hint: "Free guillotine through to Z/W trims, refused on laminated stock." },
                  { label: t("Verification oracles"), value: "2", hint: "Geometry and piece conservation, both required before export." },
                ].map((item) => (
                  <GlassMetric key={t(item.label)} value={t(item.value)} label={t(item.label)} hint={t(item.hint)} />
                ))}
              </GlassMetricRow>

              <GlassDivider />

              {/* ── User-Facing Optimization Journey ────────────────────────── */}
              <div className="py-2">
                <div className="flex items-center justify-between gap-3 mb-3">
                  <p className="eyebrow text-text-muted">{t("Optimization Pipeline")}</p>
                  <span className="text-2xs font-mono text-brand-300">
                    {t("Stock Plate to Cutting Table")}
                  </span>
                </div>
                <div className="grid grid-cols-2 gap-2.5 sm:grid-cols-3 lg:grid-cols-6">
                  {[
                    { step: "01", name: t("Glass Parts"), desc: t("Dimensions from confirmed orders") },
                    { step: "02", name: t("Nesting / Layout"), desc: t("Guillotine cuts fitted to plates") },
                    { step: "03", name: t("Rotation"), desc: t("Smart 90° orientation handling") },
                    { step: "04", name: t("Kerf / Trims"), desc: t("Safety margins and blade offsets") },
                    { step: "05", name: t("Sheet Yield"), desc: t("Maximum usable area per plate") },
                    { step: "06", name: t("Wastage & Offcuts"), desc: t("Reusable stock saved to racks") },
                  ].map((item) => (
                    <div
                      key={item.step}
                      className="rounded-xl border border-line-strong/60 bg-ink-900/60 p-3 flex flex-col justify-between"
                    >
                      <div>
                        <span className="font-mono text-2xs text-brand-300 font-bold">{item.step}</span>
                        <h4 className="mt-1 text-xs font-semibold text-text-primary">{item.name}</h4>
                      </div>
                      <p className="mt-1.5 text-2xs text-text-muted leading-tight">{item.desc}</p>
                    </div>
                  ))}
                </div>
              </div>

              <GlassDivider />

              <SheetPlanStrip />
            </GlassCard>
          </Reveal>
        </Container>
      </Section>

      {/* ═══ 08 · Module showcase ════════════════════════════════════ */}
      <Section tone="raised" aria-labelledby="modules-heading">
        <Container>
          <SectionHeading
            id="modules-heading"
            eyebrow={t("Core modules")}
            title={t("The parts of the job this application holds.")}
            lead={t.template`Four of the ${modules.length} navigation targets are shown here in depth. Each exists in the rail with the label shown — nothing on this page is a planned feature presented as a shipped one.`}
          />
          <div className="mt-14">
            <ModuleShowcase
      t={t} modules={heroModules} />
          </div>
          <Reveal className="mt-14">
            <ButtonLink href="/features" size="lg" trailing={<ArrowRight />}>
              See all {modules.length} modules
            </ButtonLink>
          </Reveal>
        </Container>
      </Section>

      {/* ── The nine rail groups, as a tour ────────────────────────
         *
         * Home is a guided tour of the application as it actually is: the
         * sidebar's nine groups hold every module the catalogue lists. Each
         * card links into the catalogue at that group's own anchor, so the
         * walk home → catalogue never restarts at the top of a long page.
         */}
      <Section id="rail-tour" aria-labelledby="rail-tour-heading">
        <Container>
          <SectionHeading
            id="rail-tour-heading"
            eyebrow={t("Take the tour")}
            title={t.template`Nine rail groups, ${modules.length} rooms of the same building.`}
            lead={t("The application is organized exactly as its sidebar is: nine groups, from Main to System, hold every module the catalogue lists. Walk them here, then open one in the catalogue itself — every card there keeps the app's own navigation label and the file that backs the claim.")}
          />
          <div className="mt-12 grid gap-5 sm:grid-cols-2 lg:grid-cols-3">
            {moduleGroups.map((group, index) => {
              const list = modulesByGroup(group);
              return (
                <Reveal key={group} delay={Math.min(index, 6) * 45} className="h-full">
                  <Link
                    href={`/features#group-${group.toLowerCase().replace(/[^a-z0-9]+/g, "-")}`}
                    className="surface card-interactive group flex h-full flex-col p-5"
                  >
                    <div className="flex items-center justify-between gap-3">
                      <span className="grid h-9 w-9 place-items-center rounded-card border border-line/60 bg-ink-900/50 text-brand-300">
                        <Icon path={groupIcons[group]} className="h-4 w-4" />
                      </span>
                      <span className="font-mono text-2xs tabular-nums text-text-faint font-medium">
                        {t.template`${list.length} modules`}
                      </span>
                    </div>
                    <h3 className="mt-3 flex items-center gap-2 font-mono text-sm font-semibold uppercase tracking-[0.18em] text-brand-300">
                      {t(group)}
                    </h3>
                    <p className="mt-3 text-sm leading-relaxed text-text-muted">{t(groupNotes[group])}</p>
                    <ul className="mt-4 space-y-1.5 border-t border-line/60 pt-4">
                      {list.slice(0, 3).map((item) => (
                        <li key={item.id} className="flex gap-2.5 text-xs leading-relaxed text-text-muted">
                          <span aria-hidden="true" className="mt-1.5 h-1 w-1 shrink-0 rounded-full bg-brand-400/80" />
                          {t(item.name)}
                        </li>
                      ))}
                    </ul>
                    <span className="mt-auto inline-flex items-center gap-1.5 pt-5 text-sm font-semibold text-brand-300 transition-colors duration-200 group-hover:text-brand-200">
                      {t("Open this group in the catalogue")}
                      <ArrowRight />
                    </span>
                  </Link>
                </Reveal>
              );
            })}
          </div>
        </Container>
      </Section>

      {/* ═══ 08c · Appearance ═════════════════════════════════════════ */}
      <Section tone="sunken" aria-labelledby="appearance-heading">
        <Container>
          <div className="grid gap-10 lg:grid-cols-[minmax(0,1fr)_minmax(0,1.25fr)] lg:items-center lg:gap-16">
            <div>
              <SectionHeading
                id="appearance-heading"
                eyebrow={t("Appearance")}
                title={t("Four families, sixteen accents — and the card edge is yours to set.")}
                lead={t("The application ships four visual families and sixteen accent presets, all chosen from Settings. The card edge, the corner it turns on and the tint of the fill are set beside them. The same controls drive this website: press a plate below, or move the edge, and the page you are reading re-themes itself.")}
                gradient
              />
              <Reveal delay={120} className="mt-8 flex flex-wrap gap-3">
                <ButtonLink href="/features" size="lg" trailing={<ArrowRight />}>
          {t("See the modules")}
        </ButtonLink>
                <ButtonLink href="/developer" variant="secondary" size="lg">
          {t("Who built it")}
        </ButtonLink>
              </Reveal>
              <p className="mt-6 text-xs font-medium leading-relaxed text-text-muted">{t("Accent values are transcribed from the application's own preset table, so the swatch you press is the colour the product uses.")}</p>
            </div>

            <Reveal delay={80}>
              <AppearancePreview />
            </Reveal>
          </div>
        </Container>
      </Section>

      {/* ═══ 08b · Sponsorship ═══════════════════════════════════════ */}
      <Section aria-labelledby="sponsors-heading">
        <Container>
          <Reveal>
            <div className="card-accent p-8 sm:p-10">
              <div className="grid gap-8 lg:grid-cols-[minmax(0,1.35fr)_minmax(0,1fr)] lg:items-center lg:gap-12">
                <div>
                  <p className="eyebrow">{t("Open to help")}</p>
                  <h2 id="sponsors-heading" className="display-2 mt-3">{t("One developer, and room for a few more hands.")}</h2>
                  <p className="mt-4 max-w-2xl text-sm leading-relaxed text-text-secondary">{t("ProGlass Automation is written and maintained by one person. Sponsorship funds work — a cutting table to test export formats against, reviewed translations, test engineering — and never buys a roadmap position. The page below states the gaps as they actually are.")}</p>
                  <div className="mt-6 flex flex-wrap gap-3">
                    <ButtonLink href="/sponsors" size="lg" trailing={<ArrowRight />}>
          {t("Sponsor the build")}
        </ButtonLink>
                    <ButtonLink href="/developer" variant="secondary" size="lg">
          {t("Meet the developer")}
        </ButtonLink>
                  </div>
                </div>
                <ul className="grid gap-3 sm:grid-cols-2 lg:grid-cols-1">
                  {[
                    { label: t("Maintainers"), value: t("One") },
                    { label: t("Sponsors so far"), value: t("None — stated plainly") },
                    { label: t("Licence model"), value: t("Owned, not rented") },
                  ].map((item) => (
                    <li
                      key={t(item.label)}
                      className="flex items-baseline justify-between gap-4 rounded-card border border-line/60 bg-ink-950/40 px-4 py-3"
                    >
                      <span className="text-xs uppercase tracking-[0.14em] text-text-faint font-medium">{t(item.label)}</span>
                      <span className="text-sm font-medium text-text-secondary">{t(item.value)}</span>
                    </li>
                  ))}
                </ul>
              </div>
            </div>
          </Reveal>
        </Container>
      </Section>

      {/* ═══ 09 · Platform ═══════════════════════════════════════════ */}
      <Section aria-labelledby="platform-heading">
        <Container>
          <SectionHeading
            id="platform-heading"
            eyebrow={t("Desktop, by design")}
            title={t("Your data is a file on your machine, and that is the point.")}
            lead={t("There is no cloud account, no tenant and no subscription dashboard. The application runs on one Windows PC, or on a handful that share a database file across the office.")}
          />

          <div className="mt-12 grid gap-6 lg:grid-cols-3">
            {featureGroups
              .filter((group) => ["platform", "money", "documents"].includes(group.id))
              .map((group, index) => (
                <Reveal key={group.id} delay={index * 70} className="surface-raised flex flex-col p-6">
                  <h3 className="text-lg font-semibold text-text-primary">{t(group.title)}</h3>
                  <p className="mt-3 text-sm leading-relaxed text-text-muted">{t(group.lead)}</p>
                  <ul className="mt-5 space-y-3 border-t border-line/60 pt-5">
                    {group.items.slice(0, 4).map((item) => (
                      <li key={t(item.title)}>
                        <p className="text-sm font-medium text-text-secondary">{t(item.title)}</p>
                        <p className="mt-0.5 text-xs leading-relaxed text-text-faint font-medium">{t(item.detail)}</p>
                      </li>
                    ))}
                  </ul>
                  <p className="mono-label mt-5 pt-4">
                    <span className="sr-only">{t("Evidence in the source tree:")}{" "}</span>
                    {t(group.evidence)}
                  </p>
                </Reveal>
              ))}
          </div>
        </Container>
      </Section>

      {/* ═══ 10 · Screenshots ═══════════════════════════════════════ */}
      <Section tone="sunken" aria-labelledby="screens-heading">
        <Container>
          <div className="flex flex-col gap-8 lg:flex-row lg:items-end lg:justify-between">
            <SectionHeading
              id="screens-heading"
              eyebrow={t("The interface")}
              title={t("What you would be looking at all day.")}
              lead={t("A working application is judged by its list screens and its data grids, so those are what this gallery shows first. Every frame here is interactive: sort the grids, widen the search, run the optimizer, and read the figures it calculates.")}
            />
            <ButtonLink href="/screenshots" variant="secondary" size="lg" trailing={<ArrowRight />} className="shrink-0">
          {t("Open the gallery")}
        </ButtonLink>
          </div>

          <ul className="mt-10 grid gap-6 sm:grid-cols-2 lg:grid-cols-3">
            {screenshots
              .filter((s) => ["optimization-2d", "sheet-store-overview", "overview-dashboard"].includes(s.id))
              .map((shot, index) => (
                <Reveal key={shot.id} as="li" delay={index * 70}>
                  <ScreenshotFrame
      t={t} shot={shot} />
                </Reveal>
              ))}
          </ul>
        </Container>
      </Section>

      {/* ═══ 11 · About the build ════════════════════════════════════ */}
      <Section aria-labelledby="about-heading">
        <Container>
          <div className="grid gap-12 lg:grid-cols-[minmax(0,1fr)_minmax(0,1.1fr)] lg:gap-16">
            <SectionHeading
              id="about-heading"
              eyebrow={t("About ProGlass")}
              title={t("Written as one product, not assembled from parts.")}
              lead={t("The application is a single WPF solution: views and view-models split one folder per business area, one database context per bounded context, and a design system the whole interface is built from. The same discipline shows in the numbers on this page — no debt markers in shipped code, policed by a test rather than by good intentions.")}
            />

            <Reveal delay={80}>
              <figure className="plate overflow-hidden">
                <div className="relative aspect-[16/7] w-full">
                  <Image
                    src={glass3d.dguLam.src}
                    alt={t(glass3d.dguLam.alt)}
                    fill
                    sizes="(max-width: 1024px) 100vw, 640px"
                    className="object-cover"
                  />
                  <div aria-hidden="true" className="absolute inset-0 bg-gradient-to-t from-ink-950 via-transparent to-transparent" />
                </div>
                <figcaption className="border-t border-line/60 bg-ink-950/60 px-4 py-3 text-xs leading-relaxed text-text-faint font-medium">{t("Application asset · Resources/Glass3D/dgulam-3d.png")}</figcaption>
              </figure>

              <div className="mt-4 grid gap-4 sm:grid-cols-2">
                <Link href="/about" className="surface group p-5 transition-colors duration-300 hover:border-brand-400/40">
                  <p className="eyebrow">{t("About the product")}</p>
                  <p className="mt-2 text-sm leading-relaxed text-text-muted">{t("Why it exists, who it is for, and what problem it solves in a glass fabrication business.")}</p>
                  <span className="link-underline mt-3 text-xs">{t("Read about ProGlass")}<ArrowRight />
                  </span>
                </Link>
                <Link href="/developer" className="surface group p-5 transition-colors duration-300 hover:border-brand-400/40">
                  <p className="eyebrow">{t("The developer")}</p>
                  <p className="mt-2 text-sm leading-relaxed text-text-muted">{t("Who builds it, the technology it is built with, and how the work is organised.")}</p>
                  <span className="link-underline mt-3 text-xs">{t("Meet the developer")}<ArrowRight />
                  </span>
                </Link>
              </div>
            </Reveal>
          </div>
        </Container>
      </Section>

      {/* ═══ 12 · FAQ ═══════════════════════════════════════════════ */}

      <Section tone="raised" aria-labelledby="faq-heading">
        <Container>
          <div className="grid gap-10 lg:grid-cols-[minmax(0,0.85fr)_minmax(0,1.15fr)] lg:gap-14">
            <SectionHeading
              id="faq-heading"
              eyebrow={t("Questions")}
              title={t("Straight answers, including the ones that are a no.")}
              lead={t("If the application does not do something, this page says so rather than leaving it ambiguous.")}
            />
            <Reveal delay={80}>
              <Accordion items={faq.filter((item) => item.group === "Product").map(({ question, answer }) => ({ question, answer }))} defaultOpen={0} />
              <div className="mt-4">
                <ButtonLink href="/pricing" variant="ghost" size="sm" trailing={<ArrowRight />}>{t("Licensing, editions and deployment questions")}</ButtonLink>
              </div>
            </Reveal>
          </div>
        </Container>
      </Section>

      {/* ═══ 13 · Closing CTA ═══════════════════════════════════════ */}
      <Section aria-labelledby="cta-heading">
        <Container>
          <Reveal>
            <div className="panel-feature edge-conic relative overflow-hidden p-8 sm:p-12">
              <div aria-hidden="true" className="pointer-events-none absolute inset-0">
                <div className="aurora-field aurora-drift" />
              </div>
              {/* The emblem, at the size a closing panel can carry it, behind
                  the buttons and out of the way of the text. */}
              <div
                aria-hidden="true"
                className="pointer-events-none absolute -bottom-14 -end-8 hidden opacity-[0.09] sm:block"
              >
                <GlassCubeEmblem size={280} label={null} />
              </div>

              <div className="relative grid gap-8 lg:grid-cols-[minmax(0,1.2fr)_minmax(0,1fr)] lg:items-center">
                <div>
                  <p className="eyebrow-chip mb-5">{t("Next step")}</p>
                  <h2 id="cta-heading" className="display-2">
          {t("See it against your own cutting list.")}
        </h2>
                  <p className="lede-lg mt-5 max-w-xl">
          {t("Tell us the glass types you run and how the work reaches your floor. A demonstration works best when it uses a job you recognise rather than a sample project.")}
        </p>
                  <div className="mt-7 flex flex-wrap gap-3">
                    <ButtonLink href="/contact" size="lg" trailing={<ArrowRight />}>
          {t("Request a Demo")}
        </ButtonLink>
                    <ButtonLink href="/pricing" variant="secondary" size="lg">
                      Licensing &amp; editions
                    </ButtonLink>
                  </div>
                </div>

                {/* The four facts a visitor checks before asking for a demo.
                    Each one is a small lit card rather than a bordered box, so
                    the panel's own glow reads through between them. */}
                <dl className="grid grid-cols-2 gap-4">
                  {[
                    ["Platform", "Windows (WPF)"],
                    ["Data", "Local SQLite"],
                    ["Trial", "30 days, all features"],
                    ["Activation", "Offline licence file"],
                  ].map(([term, detail]) => (
                    <Spotlight key={term} className="surface p-4">
                      <dt className="font-mono text-2xs uppercase tracking-[0.14em] text-text-faint font-medium">{term}</dt>
                      <dd className="mt-1.5 text-sm font-semibold text-text-primary">{detail}</dd>
                    </Spotlight>
                  ))}
                </dl>
              </div>
            </div>
          </Reveal>
        </Container>
      </Section>
    </>
    </PageOverlay>
  );
}

/** The chain as a compact vertical diagram for the sticky column. */
function ChainDiagram({ t }: { t: Translator }) {
  const stages = [
    "Inquiry",
    "Estimation",
    "Proforma / Quotation",
    "Job Order",
    "Production",
    "Optimization",
    "Delivery",
    "Invoicing",
    "Reporting",
  ];
  return (
    <ol className="space-y-1.5">
      {stages.map((stage, index) => (
        <li key={`${stage}-${index}`} className="flex items-center gap-3">
          <span className="font-mono text-2xs tabular-nums text-text-faint font-medium">{String(index + 1).padStart(2, "0")}</span>
          <span className="flex-1 rounded-lg border border-line/60 bg-ink-900/60 px-3 py-2 text-sm text-text-secondary">
            {t(stage)}
          </span>
        </li>
      ))}
    </ol>
  );
}

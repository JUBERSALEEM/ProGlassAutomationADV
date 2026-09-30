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
import { ProductHero } from "@/components/hero/ProductHero";
import { WorkflowRail } from "@/components/product/WorkflowRail";
import { AppearancePreview } from "@/components/layout/AppearancePreview";
import { ModuleShowcase } from "@/components/product/ModuleShowcase";
import { ProductSlider } from "@/components/product/ProductSlider";
import { SheetPlanStrip } from "@/components/optimization/SheetPlanStrip";
import { ScreenshotFrame } from "@/components/gallery/ScreenshotFrame";
import { cn } from "@/lib/cn";
import { modules } from "@/data/modules";
import { glass3d } from "@/data/assets";
import { featureGroups } from "@/data/features";
import { faq } from "@/data/faq";
import { quotationTotals, seedParties, seededDocuments } from "@/data/workedExample";
import { screenshots, screenshotById } from "@/data/screenshots";
import { RouteSchema } from "@/components/seo/RouteSchema";
import { pageMetadata } from "@/lib/metadata";
import { site } from "@/lib/site";
import { translatorFor } from "@/i18n/server";

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

      {/* ═══ 01b · The facts strip ═══════════════════════════════════ */}
      <div className="border-y border-line-strong/50 bg-ink-900">
        <Container>
          <ul className="grid divide-line/50 sm:grid-cols-2 sm:divide-x lg:grid-cols-4">
            {[
              { value: t("Windows desktop"), label: t("Installed software, not a browser tab") },
              { value: t("Local SQLite"), label: t("Your data on your machine, 16 databases") },
              { value: t("No account"), label: t("Nothing to sign up for, nothing to log into") },
              { value: `${site.locales} languages`, label: t("Interface translated, not just labelled") },
            ].map((fact, index) => (
              <li
                key={fact.value}
                className={cn(
                  "flex flex-col gap-1 py-5 sm:px-6 lg:px-8",
                  index === 0 && "sm:pl-0",
                )}
              >
                <span className="text-base font-bold text-text-primary">{fact.value}</span>
                <span className="text-xs font-medium leading-relaxed text-text-muted">{t(fact.label)}</span>
              </li>
            ))}
          </ul>
        </Container>
      </div>

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
                <div key={`${card.title}-${index}`} className="surface p-5">
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
              <ChainDiagram />
              <p className="mt-6 text-xs leading-relaxed text-text-faint font-medium">
                Status vocabularies and handoff guarantees are taken from the application&apos;s own workflow
                documentation and the transition code that enforces them.
              </p>
            </div>
            <WorkflowRail
      t={t} />
          </div>
        </Container>
      </Section>

      {/* ═══ 04 · Numbers ════════════════════════════════════════════ */}
      <Section tone="sunken" aria-labelledby="scale-heading">
        <Container>
          <div className="flex flex-col gap-8 lg:flex-row lg:items-end lg:justify-between">
            <SectionHeading
              id="scale-heading"
              eyebrow={t("Scale")}
              title={t("What is actually in the application.")}
              lead={t("Each card names where in the source tree the behaviour is defined, so a claim can be checked rather than believed. These are counts of what the repository contains — modules, locales, databases and tests. They are not performance claims, and the application makes none.")}
            />
            <p className="max-w-sm text-xs leading-relaxed text-text-faint font-medium">
              Counted against the application tree on {site.statsMeasuredOn}: {site.framework}, {site.pattern}, {site.database}. obj/, bin/ and .git/ are excluded.
            </p>
          </div>

          <div className="mt-10 grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
            <StatCounter
              value={modules.length}
              label="Navigation targets"
              hint="Every address the sidebar rail can open, across nine groups."
            />
            <StatCounter value={site.databaseCount} label="SQLite databases" hint="One DbContext per bounded context, with additive-only migrations." />
            <StatCounter value={site.locales} label="Languages" hint="Locales are resource dictionaries — adding one needs no code change." />
            <StatCounter value={site.testCount} label="Test methods" hint={`[Fact] and [Theory] methods across ${site.testFiles} test files, plus ${site.testInlineCases.toLocaleString("en-GB")} [InlineData] cases.`} />
          </div>

          <div className="mt-4 grid gap-4 sm:grid-cols-3">
            {[
              { value: "9", label: t("Navigation groups"), hint: "Main, Inventory, Sales, Production, Dispatch, Finance, Reports, Master Data, System." },
              { value: "8", label: t("Production stages"), hint: "Cutting through Doubling / Lamination, each with a workstation view." },
              { value: "14", label: t("Report surfaces"), hint: "Executive, relationships, documents and the financial reports, behind one hub." },
            ].map((fact) => (
              <div key={fact.label} className="stat-tile p-5">
                <p className="font-mono text-3xl font-bold text-text-primary">{fact.value}</p>
                <p className="mt-2 text-sm font-bold text-text-primary">{t(fact.label)}</p>
                <p className="mt-1.5 text-xs font-medium leading-relaxed text-text-muted">{t(fact.hint)}</p>
              </div>
            ))}
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

              <p className="mt-6 text-sm leading-relaxed text-text-muted">
                {t(seedParties.customer.name)} · {t(seedParties.project.name)} · contract {seedParties.project.contract}. The
                quotation is priced in three specification groups and the delivery is deliberately partial, so the
                balance workflow is visible rather than described.
              </p>

              <div className="mt-8 flex flex-wrap gap-3">
                <ButtonLink href="/worked-example" size="lg" trailing={<ArrowRight />}>
          {t("Walk through the job")}
        </ButtonLink>
                <ButtonLink href="/how-it-works" variant="secondary" size="lg">
          {t("How the chain works")}
        </ButtonLink>
              </div>

              <p className="mt-5 max-w-xl text-xs leading-relaxed text-text-faint font-medium">
                The rates in the seeded job are the sample dataset&apos;s own numbers. ProGlass Automation publishes no
                price list for glass, and nothing on this site is a market rate.
              </p>
            </div>

            <Reveal delay={60}>
              <div className="grid gap-4 sm:grid-cols-2">
                <StatBadge
                  value={String(quotationTotals.lines)}
                  label="Quotation lines"
                  hint="Three specification groups, SGU and DGU."
                />
                <StatBadge
                  value={String(quotationTotals.pieces)}
                  label="Pieces of glass"
                  hint={`${quotationTotals.squareMetres.toLocaleString("en-AE", { minimumFractionDigits: 2, maximumFractionDigits: 2 })} m² of glass.`}
                />
                <StatBadge
                  value={`AED ${quotationTotals.subtotal.toLocaleString("en-AE", { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`}
                  label="Quoted before VAT"
                  hint="Line values rounded individually, then summed."
                />
                <StatBadge
                  value={`AED ${quotationTotals.grandTotal.toLocaleString("en-AE", { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`}
                  label="VAT inclusive"
                  hint="5% through the application's one VAT calculator."
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
                        <p className="text-sm font-medium text-text-primary">{step}</p>
                        <p className="mt-0.5 text-xs leading-relaxed text-text-muted">{detail}</p>
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
                      <dt className="text-xs font-medium uppercase tracking-[0.1em] text-text-faint">{term}</dt>
                      <dd className="mt-1 text-sm leading-relaxed text-text-secondary">{detail}</dd>
                    </div>
                  ))}
                </dl>

                <p className="mt-5 border-t border-line/60 pt-4 text-xs leading-relaxed text-text-faint font-medium">
                  An ERP that quietly recalculates yesterday&apos;s quote with today&apos;s glass prices is not an ERP.
                  When sheet-store prices move, the affected row is flagged rather than silently rescored.
                </p>
              </div>
            </Reveal>
          </div>
        </Container>
      </Section>

      {/* ═══ 07 · Optimization ═══════════════════════════════════════ */}
      <Section aria-labelledby="optimization-heading">
        <Container>
          <div className="flex flex-col gap-8 lg:flex-row lg:items-end lg:justify-between">
            <SectionHeading
              id="optimization-heading"
              eyebrow={t("Optimization")}
              title={t("Turn stock plates and a cutting list into a plan the table can actually cut.")}
              lead={t("A guillotine packer that honours your safety edges and blade kerf, records the cut sequence as it places pieces, then proves the plan before it can be exported.")}
            />
            <ButtonLink href="/optimization" variant="secondary" size="lg" trailing={<ArrowRight />} className="shrink-0">
          {t("Open the engine")}
        </ButtonLink>
          </div>

          {/*
            The compact strip rather than the full demonstration: one box per
            plate, side by side, so a multi-plate job reads at a glance instead
            of pushing the rest of the page below the fold. The animated
            demonstration still runs on /optimization.
          */}
          <div className="mt-10">
            <SheetPlanStrip />
          </div>

          <Reveal delay={140} className="mt-10 grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
            {[
              { label: t("Search families"), value: t("5 + Auto"), hint: "From a quick area-shelf pass to the widest MaxRects search." },
              { label: t("Plate patterns"), value: "6", hint: "Tie-breaks that never buy a preference by adding a sheet." },
              { label: t("Cutting complexity"), value: "6 levels", hint: "Free guillotine through to Z/W trims, refused on laminated stock." },
              { label: t("Verification oracles"), value: "2", hint: "Geometry and piece conservation, both required before export." },
            ].map((item) => (
              <div key={item.label} className="surface-raised p-5">
                <p className="font-mono text-xl font-semibold text-brand-300">{item.value}</p>
                <p className="mt-2 text-sm font-medium text-text-secondary">{t(item.label)}</p>
                <p className="mt-1 text-xs leading-relaxed text-text-faint font-medium">{t(item.hint)}</p>
              </div>
            ))}
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
            lead={`Four of the ${modules.length} navigation targets are shown here in depth. Each exists in the rail with the label shown — nothing on this page is a planned feature presented as a shipped one.`}
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
              <p className="mt-6 text-xs font-medium leading-relaxed text-text-muted">
                Accent values are transcribed from the application&apos;s own preset table, so the swatch you press is the
                colour the product uses.
              </p>
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
                  <h2 id="sponsors-heading" className="mt-3 text-3xl leading-tight sm:text-4xl">
                    One developer, and room for a few more hands.
                  </h2>
                  <p className="mt-4 max-w-2xl text-sm leading-relaxed text-text-secondary">
                    ProGlass Automation is written and maintained by one person. Sponsorship funds work — a cutting
                    table to test export formats against, reviewed translations, test engineering — and never buys a
                    roadmap position. The page below states the gaps as they actually are.
                  </p>
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
                      key={item.label}
                      className="flex items-baseline justify-between gap-4 rounded-card border border-line/60 bg-ink-950/40 px-4 py-3"
                    >
                      <span className="text-xs uppercase tracking-[0.14em] text-text-faint font-medium">{t(item.label)}</span>
                      <span className="text-sm font-medium text-text-secondary">{item.value}</span>
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
                  <p className="mt-3 text-sm leading-relaxed text-text-muted">{group.lead}</p>
                  <ul className="mt-5 space-y-3 border-t border-line/60 pt-5">
                    {group.items.slice(0, 4).map((item) => (
                      <li key={item.title}>
                        <p className="text-sm font-medium text-text-secondary">{t(item.title)}</p>
                        <p className="mt-0.5 text-xs leading-relaxed text-text-faint font-medium">{t(item.detail)}</p>
                      </li>
                    ))}
                  </ul>
                  <p className="mono-label mt-5 pt-4">
                    <span className="sr-only">Evidence in the source tree: </span>
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
                <figcaption className="border-t border-line/60 bg-ink-950/60 px-4 py-3 text-xs leading-relaxed text-text-faint font-medium">
                  Application asset · Resources/Glass3D/dgulam-3d.png
                </figcaption>
              </figure>

              <div className="mt-4 grid gap-4 sm:grid-cols-2">
                <Link href="/about" className="surface group p-5 transition-colors duration-300 hover:border-brand-400/40">
                  <p className="eyebrow">{t("About the product")}</p>
                  <p className="mt-2 text-sm leading-relaxed text-text-muted">
                    Why it exists, who it is for, and what problem it solves in a glass fabrication business.
                  </p>
                  <span className="link-underline mt-3 text-xs">
                    Read about ProGlass <ArrowRight />
                  </span>
                </Link>
                <Link href="/developer" className="surface group p-5 transition-colors duration-300 hover:border-brand-400/40">
                  <p className="eyebrow">{t("The developer")}</p>
                  <p className="mt-2 text-sm leading-relaxed text-text-muted">
                    Who builds it, the technology it is built with, and how the work is organised.
                  </p>
                  <span className="link-underline mt-3 text-xs">
                    Meet the developer <ArrowRight />
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
                <ButtonLink href="/pricing" variant="ghost" size="sm" trailing={<ArrowRight />}>
                  Licensing, editions and deployment questions
                </ButtonLink>
              </div>
            </Reveal>
          </div>
        </Container>
      </Section>

      {/* ═══ 13 · Closing CTA ═══════════════════════════════════════ */}
      <Section aria-labelledby="cta-heading">
        <Container>
          <Reveal>
            <div className="plate relative overflow-hidden p-8 sm:p-12">
              <div aria-hidden="true" className="glow-layer pointer-events-none absolute inset-0">
                <div className="absolute -right-24 -top-24 h-72 w-72 rounded-full bg-brand-500/14 blur-[90px]" />
                <div className="absolute -bottom-32 left-10 h-72 w-72 rounded-full bg-glass-teal/8 blur-[100px]" />
              </div>

              <div className="relative grid gap-8 lg:grid-cols-[minmax(0,1.2fr)_minmax(0,1fr)] lg:items-center">
                <div>
                  <p className="eyebrow mb-4">{t("Next step")}</p>
                  <h2 id="cta-heading" className="text-2xl leading-tight sm:text-3xl lg:text-4xl">
          {t("See it against your own cutting list.")}
        </h2>
                  <p className="lede mt-4 max-w-xl">
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

                <dl className="grid grid-cols-2 gap-4">
                  {[
                    ["Platform", "Windows (WPF)"],
                    ["Data", "Local SQLite"],
                    ["Trial", "30 days, all features"],
                    ["Activation", "Offline licence file"],
                  ].map(([term, detail]) => (
                    <div key={term} className="rounded-card border border-line/60 bg-ink-950/50 p-4">
                      <dt className="font-mono text-2xs uppercase tracking-[0.14em] text-text-faint font-medium">{term}</dt>
                      <dd className="mt-1.5 text-sm font-medium text-text-secondary">{detail}</dd>
                    </div>
                  ))}
                </dl>
              </div>
            </div>
          </Reveal>
        </Container>
      </Section>
    </>
  );
}

/** The chain as a compact vertical diagram for the sticky column. */
function ChainDiagram() {
  const stages = ["Inquiry", "Proforma", "Job order", "Production", "Delivery", "Tax invoice", "Statement"];
  return (
    <ol className="space-y-1.5">
      {stages.map((stage, index) => (
        <li key={`${stage}-${index}`} className="flex items-center gap-3">
          <span className="font-mono text-2xs tabular-nums text-text-faint font-medium">{String(index + 1).padStart(2, "0")}</span>
          <span className="flex-1 rounded-lg border border-line/60 bg-ink-900/60 px-3 py-2 text-sm text-text-secondary">
            {stage}
          </span>
        </li>
      ))}
    </ol>
  );
}

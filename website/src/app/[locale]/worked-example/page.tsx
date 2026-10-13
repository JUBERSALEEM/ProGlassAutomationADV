import type { Metadata } from "next";
import { LocaleLink as Link } from "@/components/ui/LocaleLink";

import { Container, Section } from "@/components/ui/Container";
import { PageHero } from "@/components/layout/PageHero";
import { footerIconFor } from "@/data/footerIcons";
import { ArrowRight, ButtonLink } from "@/components/ui/Button";
import { Badge } from "@/components/ui/Badge";
import { cn as cnCell } from "@/lib/cn";
import { Reveal } from "@/components/ui/Reveal";
import { SectionHeading } from "@/components/ui/SectionHeading";
import { OptimizationRunGate } from "@/components/optimization/OptimizationRunGate";
import { PlateInspector } from "@/components/optimization/PlateInspector";
import {
  seedRunBreakoutMm,
  seedRunCutList,
  seedRunGeometry,
  seedRunMargins,
  seedRunPieces,
  seedRunPlates,
  seedRunSheet,
  seedRunTotals,
} from "@/components/optimization/seedRun";
import { StatBadge } from "@/components/ui/Stat";
import { RouteSchema } from "@/components/seo/RouteSchema";
import { pageMetadata } from "@/lib/metadata";
import { translatorFor } from "@/i18n/server";
import { PageOverlay } from "@/i18n/PageOverlay";
import {
  ROUNDING_SOURCE,
  SEED_SOURCE,
  VAT_SOURCE,
  balancePieces,
  batchOneWeightKg,
  deliveredTotals,
  groupTotals,
  billablePieceArea,
  lineArea,
  lineValue,
  priceLight,
  ruleLights,
  sheetCosts,
  sheetForRef,
  pieceArea,
  pieceWeight,
  quotationTotals,
  reorderAlerts,
  seedConsumables,
  seedParties,
  seedSheets,
  seedSpecGroups,
  seededDocuments,
} from "@/data/workedExample";
import {
  MIN_PANEL_SQM,
  STANDARD_SURCHARGE_PERCENT,
  SURCHARGE_THRESHOLD_SQM,
  pricingRules,
} from "@/data/pricingRules";

type PageProps = { params: Promise<{ locale: string }> };

export async function generateMetadata({ params }: PageProps): Promise<Metadata> {
  const t = await translatorFor(params);
  return pageMetadata({
  locale: t.locale,
  title: t("Worked example — one glass job"),
  description: t("One complete job: 22 quotation lines, 83 pieces, AED 34,762.19 — from inquiry to tax invoice, numbers from the seed."),
  path: "/worked-example",
  keywords: [t("glass fabrication ERP example"), "glass quotation calculation", "glass delivery balance", t("VAT invoice glass"), "cutting optimization example"],
  ogEyebrow: t("Worked example"),
  });
}

/** Dhaka-style grouping, so a figure in a table is countable at a glance. */
const aed = (value: number) =>
  value.toLocaleString("en-AE", { minimumFractionDigits: 2, maximumFractionDigits: 2 });

const plain = (value: number, decimals = 2) =>
  value.toLocaleString("en-AE", { minimumFractionDigits: decimals, maximumFractionDigits: decimals });

const mm = (value: number) => value.toLocaleString("en-AE");

/**
 * The two header-cell treatments, shared by every column in the Step 5 grid.
 *
 * Eleven columns cannot carry an inline class each without the table becoming
 * unreadable to edit, and the two styles are genuinely two: a left-aligned label
 * column and a right-aligned numeric one.
 */
const TH = "px-4 py-3 text-right font-mono text-2xs uppercase tracking-[0.14em] text-text-faint font-medium";
const THL = "px-4 py-3 font-mono text-2xs uppercase tracking-[0.14em] text-text-faint font-medium";

export default async function WorkedExamplePage({ params }: PageProps) {
  const t = await translatorFor(params);
  const locale = t.locale;
  return (
    <PageOverlay locale={t.locale} route="worked-example">
    <>
      <RouteSchema
      locale={locale}
        path="/worked-example"
        title={t("Worked example — one glass job")}
        description={t("One complete job: 22 quotation lines, 83 pieces, AED 34,762.19 — from inquiry to tax invoice, numbers from the seed.")}
      />

      <PageHero
        variant="showcase"
      t={t}
        glyph={footerIconFor("/worked-example")!}
        eyebrow={t("Worked example")}
        title={t("One job, all the way through.")}
        lead={t("Not a mock-up and not a market rate: this is the demonstration job the application seeds into an empty database, read back line by line. The customer, the project, the 22 quotation lines, the sheet stock behind them, the partial delivery and the invoice that followed are the records the software itself creates.")}
        banner="calculator"
        breadcrumb={[
          { label: t("Home"), href: "/" },
          { label: t("Worked example"), href: "/worked-example" },
        ]}
      >
        <div className="mt-8 flex flex-wrap gap-3">
          <ButtonLink href="#quotation" size="lg" trailing={<ArrowRight />}>
          {t("See the quotation")}
        </ButtonLink>
          <ButtonLink href="/contact" variant="secondary" size="lg">
          {t("Request a demo")}
        </ButtonLink>
        </div>
      </PageHero>

      {/* ── Where this data comes from ───────────────────────────── */}
      <Section tone="raised" aria-labelledby="provenance-heading">
        <Container>
          <div className="grid gap-10 lg:grid-cols-[minmax(0,1.1fr)_minmax(0,0.9fr)] lg:gap-16">
            <div>
              <SectionHeading
                id="provenance-heading"
                eyebrow={t("Where this comes from")}
                title={t("The application seeds this job itself.")}
                lead={t("ProGlass Automation can load one complete demonstration workflow into an empty database. It is a single connected job — one enquiry, one quotation, one job order, one partial delivery, one tax invoice — with the same data flowing through every stage, and it refuses to run at all if the database already holds real records.")}
              />

              <div className="mt-8 space-y-4">
                <div className="surface p-5">
                  <p className="mono-label">{t("Source of every figure on this page")}</p>
                  <p className="mt-2 font-mono text-sm text-brand-200">{SEED_SOURCE}</p>
                  <p className="mt-3 text-sm leading-relaxed text-text-muted">{t("Read the seeded records out of that file and you will find the customer, the project reference, each glass reference, each size, each rate, the sheet stock and the consumables reproduced below.")}</p>
                </div>

                <div className="surface p-5">
                  <p className="mono-label">{t("How the money is rounded")}</p>
                  <p className="mt-2 font-mono text-sm text-brand-200">
                    {ROUNDING_SOURCE} · {VAT_SOURCE}
                  </p>
                  <p className="mt-3 text-sm leading-relaxed text-text-muted">{t("Two decimal places, half away from zero, and 5% VAT. The arithmetic on this page is performed the same way rather than copied from the file, and a build-time check fails if the two ever disagree.")}</p>
                </div>
              </div>

              <div className="surface mt-4 border-state-warning/30 p-5">
                <div className="flex flex-wrap items-center gap-3">
                  <Badge tone="warning">{t("Read this before quoting anything")}</Badge>
                </div>
                <p className="mt-3 text-sm leading-relaxed text-text-secondary">
                  The rates below are the sample dataset&apos;s own numbers, chosen so the demonstration adds up. They
                  are <strong className="font-semibold text-text-primary">not a price list</strong>{t(", not a market rate and not a quotation from anyone. ProGlass Automation publishes no price list for glass, and this page does not change that: it shows how the software carries a job, not what glass costs.")}</p>
              </div>
            </div>

            <Reveal delay={80}>
              <div className="space-y-4">
                <div className="grid gap-4 sm:grid-cols-2">
                  <StatBadge value={String(quotationTotals.lines)} label={t("Quotation lines")} hint={t("Across three specification groups")} />
                  <StatBadge value={String(quotationTotals.pieces)} label={t("Pieces of glass")} hint={t("SGU and DGU panels")} />
                  <StatBadge
                    value={`${plain(quotationTotals.squareMetres)} m²`}
                    label={t("Total glazed area")}
                    hint={t("Sum of every line, not an estimate")}
                  />
                  <StatBadge value={`AED ${aed(quotationTotals.grandTotal)}`} label={t("Quotation, VAT inclusive")} hint={t("Sample rates — not a market price")} />
                </div>

                <div className="surface p-5">
                  <p className="mono-label">{t("The job in one paragraph")}</p>
                  <p className="mt-3 text-sm leading-relaxed text-text-muted">{t("{v1} asked for clear tempered, tinted and Low-E glass for the podium facade of {v2} in {v3}. The enquiry arrived with an indicative value of AED {v4}; by the time it was priced it had become {v5} lines and {v6} pieces. Batch 1 of 2 has left the factory, and the invoice has been raised on what actually went out.", { v1: t(seedParties.customer.name), v2: t(seedParties.project.name), v3: seedParties.project.site, v4: aed(seedParties.inquiryValue), v5: quotationTotals.lines, v6: quotationTotals.pieces })}</p>
                  <p className="mt-3 text-sm leading-relaxed text-text-muted">{t("Contract {v1}· LPO {v2}· site reference {v3}.", { v1: seedParties.project.contract, v2: seedParties.project.lpo, v3: seedParties.project.reference })}</p>
                </div>
              </div>
            </Reveal>
          </div>
        </Container>
      </Section>

      {/* ── The five documents ───────────────────────────────────── */}
      <Section aria-labelledby="documents-heading">
        <Container>
          <SectionHeading
            id="documents-heading"
            eyebrow={t("The chain")}
            title={t("Five documents, one job.")}
            lead={t("Each document is raised from the one before it rather than retyped. The number format is the application's own — the year is taken from the machine the data was seeded on, so 2026 here is an example of that year, not a fixed part of the format.")}
          />

          <ol className="mt-12 grid gap-4 xl:grid-cols-5">
            {seededDocuments.map((document, index) => (
              <Reveal key={document.key} as="li" delay={Math.min(index, 4) * 50}>
                <article className="surface flex h-full flex-col p-5">
                  <div className="flex items-baseline justify-between gap-3">
                    <span className="font-mono text-2xs uppercase tracking-[0.18em] text-brand-300">
                      {String(index + 1).padStart(2, "0")}
                    </span>
                    <Badge tone={document.key === "tax-invoice" ? "accent" : "neutral"}>{t(document.status)}</Badge>
                  </div>

                  <h3 className="mt-3 text-base font-semibold text-text-primary">{t(document.title)}</h3>
                  <p className="mt-1 font-mono text-xs text-text-faint">{document.example}</p>

                  <p className="mt-3 text-sm leading-relaxed text-text-muted">{t(document.summary)}</p>

                  <p className="mt-auto border-t border-line/60 pt-4 text-xs leading-relaxed text-text-faint font-medium">
                    {t(document.handoff)}
                  </p>
                  <p className="mt-3 font-mono text-[0.75rem] leading-relaxed text-text-faint">{t(document.evidence)}</p>
                </article>
              </Reveal>
            ))}
          </ol>

          <p className="mt-6 text-xs leading-relaxed text-text-faint font-medium">
            {t.template`Number patterns as the seeder builds them: ${seededDocuments.map((document) => document.pattern).join(" · ")}.`}
          </p>
        </Container>
      </Section>

      {/* ── The quotation, line by line ──────────────────────────── */}
      <Section tone="raised" aria-labelledby="quotation-heading" id="quotation">
        <Container>
          <SectionHeading
            id="quotation-heading"
            eyebrow={t("The quotation")}
            title={t("Twenty-two lines, priced three ways.")}
            lead={t("The proforma is priced by specification group: clear tempered for the main facade, tinted where the sun hits, and a double-glazed Low-E unit for the energy-efficient zones. Each line carries its specification exactly as Step 5 holds it — serial, glass reference, outer pane W1 × H1, inner pane W2 × H2 where there is one, quantity, area, rate and value.")}
          />

          {/*
            The column legend.

            A dash in W2/H2 is a rendered convention, not the stored value: the
            seeder writes 0 for a single-glazed line (SampleDataService.cs, the
            CT- and TT- groups), and the application's grid binds that integer
            straight into a TextBox. Printing five zeros would read as broken
            data rather than as "there is no second pane", so the dash is used
            and explained here — the reader is told what the application stores
            rather than being left to guess.
          */}
          <p className="mt-5 text-xs leading-relaxed text-text-muted">
            <span className="font-mono text-text-faint">#</span>{" "}
            {t("is the line’s Sr No — the serial the application stores on every cutting line, and the number the grid sorts by.")}{" "}
            <span className="font-mono text-text-faint">{t("TOTAL SQM")}</span>{" "}
            {t("is SQM × QTY. A")}{" "}
            <span className="font-mono text-text-faint">—</span>{" "}
            {t("in W2 or H2 means the line has no second pane: the application stores")}{" "}
            <span className="font-mono text-text-faint">0</span>
            {t("in those cells for single glazing, while a double-glazed line carries both pairs — and in this seeded job the two panes are cut to the same size.")}</p>

          <div className="mt-6 flex flex-wrap gap-2">
            {groupTotals.map((group) => (
              <span
                key={group.number}
                className="rounded-full border border-line-strong/70 px-3.5 py-1.5 text-xs font-medium text-text-muted"
              >
                Group {group.number}: {t(group.name)}
                <span className="ml-1.5 font-mono text-[0.75rem] text-text-faint">
                  {group.lines} lines · {group.pieces} pcs
                </span>
              </span>
            ))}
          </div>

          {seedSpecGroups.map((group, groupIndex) => {
            const totals = groupTotals[groupIndex]!;
            return (
              <Reveal key={group.number} delay={groupIndex * 60} className="mt-12">
                <div className="surface overflow-hidden">
                  <div className="flex flex-wrap items-baseline justify-between gap-3 border-b border-line/60 bg-ink-900/60 px-5 py-4">
                    <div>
                      {/*
                        The application's own heading for this block. Step 5's
                        grid is a SPECIFICATION, and the rows under it are its
                        cutting lines — the word is the product's, not a label
                        invented here. See Themes/Languages/en-US.xaml, key
                        `Dg.Specification`.
                      */}
                      <p className="mono-label">Specification {group.number}</p>
                      <h3 className="mt-1.5 text-base font-semibold text-text-primary">{t(group.name)}</h3>
                      {/*
                        The block's own fields, under the application's names:
                        Dg.GlassType, Dg.Specification and Dg.Base sit above the
                        grid in Step5DataGrid/DataGridView.xaml, and the table
                        below them is the specification's cutting lines.
                      */}
                      <dl className="mt-2.5 flex flex-wrap gap-x-6 gap-y-1.5">
                        {(
                          [
                            ["GLASS TYPE", group.glassType === "DGU" ? "DGU" : "SGU"],
                            ["SPECIFICATION", group.name],
                            ["BASE", `AED ${aed(group.baseRate)} / m²`],
                            ["LINES", `${totals.lines}`],
                          ] as const
                        ).map(([key, value]) => (
                          <div key={key} className="flex items-baseline gap-2">
                            <dt className="font-mono text-[0.6875rem] font-semibold uppercase tracking-[0.16em] text-text-faint">
                              {key}
                            </dt>
                            <dd className="text-xs font-medium text-text-secondary">{value}</dd>
                          </div>
                        ))}
                      </dl>
                      {/*
                        `Dg.ColumnsHint`, verbatim. The application prints this
                        line above the grid so a reader knows what the eleven
                        columns are without opening a manual.
                      */}
                      <p className="mt-2.5 font-mono text-[0.6875rem] tracking-[0.08em] text-text-muted">
          {t("GLASS REF · W1 H1 W2 H2 · QTY · SQM · T.SQM · RATE · VALUE.")}
        </p>
                    </div>
                    <p className="font-mono text-sm text-brand-200">AED {aed(totals.subtotal)}</p>
                  </div>

                  <div className="overflow-x-auto">
                    <table className="w-full min-w-[62rem] border-collapse text-sm">
                      <caption className="sr-only">{t("Group {v1}, {v2}: every quotation line with its size, area, rate and value.", { v1: group.number, v2: t(group.name) })}</caption>
                      <thead>
                        <tr className="border-b border-line/60 text-left">
                          {/*
                            The column set is Step 5's, in Step 5's order, under
                            Step 5's names — `Dg.Hash`, `Dg.GlassRef`, `Dg.W1`,
                            `Dg.H1`, `Dg.W2`, `Dg.H2`, `Dg.Qty`, `Dg.Sqm`,
                            `Dg.TotalSqm`, `Dg.Rate`, `Dg.Value` in
                            Themes/Languages/en-US.xaml. W1/H1 are the outer pane
                            and W2/H2 the inner; a single-glazed line has no
                            second pair, and the cells say so rather than being
                            blank.
                          */}
                          {/*
                            The application's own header strings — `Dg.Hash`,
                            `Dg.GlassRef`, `Dg.W1`, `Dg.H1`, `Dg.W2`, `Dg.H2`,
                            `Dg.Qty`, `God.Sqm`, `Dg.TotalSqm`, `Dg.Rate`,
                            `Dg.Value` — in the order DataGridView.xaml lays
                            them out, with its own tooltips for the two area
                            columns. Nothing here is a renamed column.
                          */}
                          <th scope="col" className={THL} title={t("Sr No — the line's serial number (Dg.Hash)")}>#</th>
                          <th scope="col" className={THL}>{t("GLASS REF")}</th>
                          <th scope="col" className={TH} title={t("Outer pane width, millimetres")}>W1</th>
                          <th scope="col" className={TH} title={t("Outer pane height, millimetres")}>H1</th>
                          <th scope="col" className={TH} title={t("Inner pane width, millimetres — double-glazed lines only")}>W2</th>
                          <th scope="col" className={TH} title={t("Inner pane height, millimetres — double-glazed lines only")}>H2</th>
                          <th scope="col" className={TH} title={t("Pieces on this line")}>QTY</th>
                          <th scope="col" className={TH} title={t("Unit panel area")}>SQM</th>
                          <th scope="col" className={TH} title={t("Total SQM (SQM × Qty)")}>{t("TOTAL SQM")}</th>
                          <th scope="col" className={TH} title={t("Rate per square metre")}>RATE</th>
                          <th
                            scope="col"
                            className={TH}
                            title={t("Dg.SurchargePct — the specification's uplift, and the rate a panel at 4 m² or above takes instead of the base rate")}
                          >
          {t("SURCHARGE %")}
        </th>
                          <th scope="col" className={TH} title={t("Line value from the rate")}>VALUE</th>
                        </tr>
                      </thead>
                      <tbody>
                        {group.lines.map((line) => (
                          <tr key={`${group.number}-${line.ref}`} className="border-b border-line/40 last:border-0">
                            <td className="px-4 py-2.5 font-mono text-xs text-text-faint">{line.serial}</td>
                            <td className="px-4 py-2.5 font-mono text-xs text-text-secondary">{line.ref}</td>
                            <td className="px-4 py-2.5 text-right font-mono text-xs text-text-secondary tabular-nums">
                              {mm(line.widthMm)}
                            </td>
                            <td className="px-4 py-2.5 text-right font-mono text-xs text-text-secondary tabular-nums">
                              {mm(line.heightMm)}
                            </td>
                            <td
                              className="px-4 py-2.5 text-right font-mono text-xs text-text-muted tabular-nums"
                              title={
                                line.secondPane
                                  ? "Inner pane width — the seeder cuts both panes to the same size on this line"
                                  : "No second pane on a single-glazed line; the application stores 0 here"
                              }
                            >
                              {line.secondPane ? mm(line.secondPane.widthMm) : "—"}
                            </td>
                            <td
                              className="px-4 py-2.5 text-right font-mono text-xs text-text-muted tabular-nums"
                              title={
                                line.secondPane
                                  ? "Inner pane height — the seeder cuts both panes to the same size on this line"
                                  : "No second pane on a single-glazed line; the application stores 0 here"
                              }
                            >
                              {line.secondPane ? mm(line.secondPane.heightMm) : "—"}
                            </td>
                            <td className="px-4 py-2.5 text-right text-text-secondary tabular-nums">{line.qty}</td>
                            <td className="px-4 py-2.5 text-right text-text-muted tabular-nums">
                              {plain(billablePieceArea(line))}
                              {billablePieceArea(line) > pieceArea(line) ? (
                                <span
                                  className="ml-1.5 rounded-full border border-brand-400/45 bg-brand-400/12 px-1.5 py-px font-mono text-2xs text-brand-100"
                                  title={t.template`${plain(pieceArea(line))} m² of glass, billed at the ${MIN_PANEL_SQM} m² floor`}
                                >
                                  floor
                                </span>
                              ) : null}
                            </td>
                            <td className="px-4 py-2.5 text-right text-text-muted tabular-nums">{plain(lineArea(line))}</td>
                            <td className="px-4 py-2.5 text-right text-text-muted tabular-nums">
                              {aed(line.rate)}
                              {/*
                                The switch, shown on the row it would fire on.
                                `row.Sqm1 >= 4m ? SurchargeRate : BaseRate` — on
                                this job no panel reaches 4 m², so every row is
                                on its own rate and the column says so rather
                                than leaving the visitor to assume.
                              */}
                              {billablePieceArea(line) >= SURCHARGE_THRESHOLD_SQM ? (
                                <span className="ml-1.5 rounded-full border border-money-positive/45 bg-money-positive/12 px-1.5 py-px font-mono text-2xs text-money-positive">
                                  +{STANDARD_SURCHARGE_PERCENT}%
                                </span>
                              ) : null}
                            </td>
                            <td className="px-4 py-2.5 text-right font-mono text-xs text-text-faint tabular-nums">
                              {group.surchargePercent === 0 ? "0" : group.surchargePercent}%
                            </td>
                            <td className="px-4 py-2.5 text-right font-medium text-text-primary tabular-nums">
                              {aed(lineValue(line))}
                            </td>
                          </tr>
                        ))}
                      </tbody>
                      <tfoot>
                        <tr className="border-t border-line/60 bg-ink-900/50">
                          <td colSpan={6} className="px-4 py-3 text-xs font-medium text-text-secondary">
                            Specification {group.number} · {totals.lines} lines
                          </td>
                          <td className="px-4 py-3 text-right font-semibold text-text-secondary tabular-nums">
                            {totals.pieces}
                          </td>
                          <td className="px-4 py-3" />
                          <td className="px-4 py-3 text-right font-semibold text-text-secondary tabular-nums">
                            {plain(totals.squareMetres)}
                          </td>
                          <td className="px-4 py-3" />
                          <td className="px-4 py-3" />
                          <td className="px-4 py-3 text-right font-semibold text-text-primary tabular-nums">
                            {aed(totals.subtotal)}
                          </td>
                        </tr>
                      </tfoot>
                    </table>
                  </div>
                </div>
              </Reveal>
            );
          })}

          {/* ── Totals ─────────────────────────────────────────────── */}
          <Reveal className="mt-12">
            <div className="grid gap-6 lg:grid-cols-[minmax(0,0.9fr)_minmax(0,1.1fr)]">
              <div className="surface-raised p-6 sm:p-8">
                <p className="eyebrow">{t("Quotation total")}</p>
                <dl className="mt-5 space-y-3 text-sm">
                  <div className="flex items-baseline justify-between gap-4 border-b border-line/50 pb-3">
                    <dt className="text-text-muted">
                      Glass materials, {quotationTotals.lines} lines
                      <span className="block text-xs text-text-faint">
                        {plain(quotationTotals.squareMetres)} m² of {quotationTotals.pieces} pieces
                      </span>
                    </dt>
                    <dd className="font-mono text-text-primary tabular-nums">AED {aed(quotationTotals.subtotal)}</dd>
                  </div>
                  <div className="flex items-baseline justify-between gap-4 border-b border-line/50 pb-3">
                    <dt className="text-text-muted">{t("Discount")}</dt>
                    <dd className="font-mono text-text-primary tabular-nums">{t("AED 0.00")}</dd>
                  </div>
                  <div className="flex items-baseline justify-between gap-4 border-b border-line/50 pb-3">
                    <dt className="text-text-muted">
          {t("VAT at 5%")}
        <span className="block text-xs text-text-faint">{t("One centralised VAT calculator")}</span>
                    </dt>
                    <dd className="font-mono text-text-primary tabular-nums">AED {aed(quotationTotals.vat)}</dd>
                  </div>
                  <div className="flex items-baseline justify-between gap-4 pt-1">
                    <dt className="text-base font-semibold text-text-primary">{t("Grand total")}</dt>
                    <dd className="font-mono text-lg font-semibold text-brand-100 tabular-nums">
                      AED {aed(quotationTotals.grandTotal)}
                    </dd>
                  </div>
                </dl>
                <p className="mt-5 text-xs leading-relaxed text-text-faint font-medium">{t("Line values are rounded individually, then summed — which is how the application prices a proforma, and why this total is not simply area × rate.")}</p>
              </div>

              <div className="space-y-4">
                <div className="surface p-6">
                  <p className="eyebrow">{t("What the figures say")}</p>
                  <ul className="mt-4 space-y-3 text-sm leading-relaxed text-text-muted">
                    <li className="flex gap-3">
                      <span aria-hidden="true" className="mt-2 h-1 w-1 shrink-0 rounded-full bg-brand-400/80" />
                      <span>
                        {t(
                          "The most expensive line is not the largest quantity. {group} runs {lines} lines for {pieces} pieces, but at AED {rate}/m² it is worth AED {value} — more than the tinted group, which has more lines and more pieces.",
                          {
                            group: groupTotals[2]!.name.split(" — ")[0] ?? groupTotals[2]!.name,
                            lines: groupTotals[2]!.lines,
                            pieces: groupTotals[2]!.pieces,
                            rate: aed(seedSpecGroups[2]!.baseRate),
                            value: aed(groupTotals[2]!.subtotal),
                          },
                        )}
                      </span>
                    </li>
                    <li className="flex gap-3">
                      <span aria-hidden="true" className="mt-2 h-1 w-1 shrink-0 rounded-full bg-brand-400/80" />
                      <span>
                        {(() => {
                          const heaviest = seedSpecGroups
                            .flatMap((group) => group.lines)
                            .reduce((best, line) => (pieceWeight(line) > pieceWeight(best) ? line : best));
                          return t.template`A double-glazed line weighs twice its glass plus the cavity. The heaviest piece in the job is ${heaviest.ref} at ${plain(pieceWeight(heaviest), 1)} kg — ${plain(pieceArea(heaviest))} m² of 24 mm nominal build-up. Batch 1 alone is ${plain(batchOneWeightKg, 1)} kg on the truck.`;
                        })()}
                      </span>
                    </li>
                    <li className="flex gap-3">
                      <span aria-hidden="true" className="mt-2 h-1 w-1 shrink-0 rounded-full bg-brand-400/80" />
                      <span>{t("The enquiry was captured at AED {v1}. The quotation came out at AED {v2} VAT inclusive, or AED {v3} before it — the difference between an indicative number taken by the sales desk and a priced one.", { v1: aed(seedParties.inquiryValue), v2: aed(quotationTotals.grandTotal), v3: aed(quotationTotals.subtotal) })}</span>
                    </li>
                  </ul>
                </div>

                <div className="surface p-6">
                  <p className="eyebrow">{t("Built up, not guessed at")}</p>
                  <p className="mt-3 text-sm leading-relaxed text-text-muted">{t("Every rate above matches the sell price of the sheet that glass comes from — 6 mm clear tempered at 150, 8 mm at 185, 10 mm at 210, 12 mm at 245, tinted at 175 and 195, and the Double-glazed unit at 260. That consistency is the point of the sample dataset: the stock in the store, the rate on the quotation and the value on the invoice are the same underlying job data.")}</p>
                </div>
              </div>
            </div>
          </Reveal>
        </Container>
      </Section>

      {/* ── The partial delivery ─────────────────────────────────── */}
      <Section aria-labelledby="delivery-heading">
        <Container>
          <SectionHeading
            id="delivery-heading"
            eyebrow={t("The partial delivery")}
            title={t("Forty-five pieces out, thirty-eight still owed.")}
            lead={t("The delivery note is created as Dispatching and carries only what actually leaves. Batch 1 is the ten Clear Tempered lines; the tinted and Low-E pieces stay on the job with their full quantity as balance, waiting for Batch 2.")}
          />

          <div className="mt-10 grid gap-6 lg:grid-cols-[minmax(0,1.15fr)_minmax(0,0.85fr)]">
            <Reveal>
              <div className="surface overflow-hidden">
                <div className="flex flex-wrap items-baseline justify-between gap-3 border-b border-line/60 bg-ink-900/60 px-5 py-4">
                  <div>
                    <h3 className="text-base font-semibold text-text-primary">
                      {seededDocuments[3]!.example}
                    </h3>
                    <p className="mt-1 text-xs text-text-faint font-medium">
                      Job order {seededDocuments[2]!.example} · Batch 1 of 2 · {seedParties.people.driver} ·{" "}
                      {seedParties.people.vehicle}
                    </p>
                  </div>
                  <Badge tone="warning">{seededDocuments[3]!.status}</Badge>
                </div>

                <div className="overflow-x-auto">
                  <table className="w-full min-w-[40rem] border-collapse text-sm">
                    <caption className="sr-only">{t("Delivered and outstanding quantity per specification group.")}</caption>
                    <thead>
                      <tr className="border-b border-line/60 text-left">
                        <th scope="col" className="px-4 py-3 font-mono text-2xs uppercase tracking-[0.14em] text-text-faint font-medium">{t("Spec group")}</th>
                        <th scope="col" className="px-4 py-3 text-right font-mono text-2xs uppercase tracking-[0.14em] text-text-faint font-medium">{t("Ordered")}</th>
                        <th scope="col" className="px-4 py-3 text-right font-mono text-2xs uppercase tracking-[0.14em] text-text-faint font-medium">{t("Delivered")}</th>
                        <th scope="col" className="px-4 py-3 text-right font-mono text-2xs uppercase tracking-[0.14em] text-text-faint font-medium">{t("Balance")}</th>
                        <th scope="col" className="px-4 py-3 font-mono text-2xs uppercase tracking-[0.14em] text-text-faint font-medium">{t("Batch")}</th>
                      </tr>
                    </thead>
                    <tbody>
                      {groupTotals.map((group, index) => {
                        const delivered = index === 0 ? group.pieces : 0;
                        return (
                          <tr key={group.number} className="border-b border-line/40 last:border-0">
                            <td className="px-4 py-3 text-text-secondary">
                              <span className="font-mono text-xs text-text-faint">{group.number}</span>{" "}
                              {t(group.name)}
                            </td>
                            <td className="px-4 py-3 text-right text-text-secondary tabular-nums">{group.pieces}</td>
                            <td className="px-4 py-3 text-right text-text-primary tabular-nums">{delivered}</td>
                            <td className="px-4 py-3 text-right text-text-muted tabular-nums">
                              {group.pieces - delivered}
                            </td>
                            <td className="px-4 py-3 text-xs text-text-faint">
                              {delivered > 0 ? "Batch 1 — dispatched" : "Batch 2 — outstanding"}
                            </td>
                          </tr>
                        );
                      })}
                    </tbody>
                    <tfoot>
                      <tr className="border-t border-line/60 bg-ink-900/50">
                        <td className="px-4 py-3 text-xs font-medium text-text-secondary">{t("Job total")}</td>
                        <td className="px-4 py-3 text-right font-medium text-text-primary tabular-nums">
                          {quotationTotals.pieces}
                        </td>
                        <td className="px-4 py-3 text-right font-medium text-text-primary tabular-nums">
                          {deliveredTotals.pieces}
                        </td>
                        <td className="px-4 py-3 text-right font-medium text-text-primary tabular-nums">
                          {balancePieces}
                        </td>
                        <td className="px-4 py-3" />
                      </tr>
                    </tfoot>
                  </table>
                </div>
              </div>
            </Reveal>

            <Reveal delay={80}>
              <div className="space-y-4">
                <div className="surface p-6">
                  <p className="eyebrow">{t("What was invoiced")}</p>
                  <dl className="mt-4 space-y-3 text-sm">
                    <div className="flex items-baseline justify-between gap-4 border-b border-line/50 pb-3">
                      <dt className="text-text-muted">
          {t("Dispatched glass")}
        <span className="block text-xs text-text-faint">
                          {deliveredTotals.pieces} pieces · {plain(deliveredTotals.squareMetres)} m²
                        </span>
                      </dt>
                      <dd className="font-mono text-text-primary tabular-nums">AED {aed(deliveredTotals.subtotal)}</dd>
                    </div>
                    <div className="flex items-baseline justify-between gap-4 border-b border-line/50 pb-3">
                      <dt className="text-text-muted">{t("VAT at 5%")}</dt>
                      <dd className="font-mono text-text-primary tabular-nums">AED {aed(deliveredTotals.vat)}</dd>
                    </div>
                    <div className="flex items-baseline justify-between gap-4">
                      <dt className="font-semibold text-text-primary">{t("Invoice total")}</dt>
                      <dd className="font-mono font-semibold text-brand-100 tabular-nums">
                        AED {aed(deliveredTotals.total)}
                      </dd>
                    </div>
                  </dl>
                  <p className="mt-5 text-xs leading-relaxed text-text-faint font-medium">
                    {t(
                      "{document} is raised from the dispatched quantity only. The balance of {balance} pieces stays on the job order and is not billed on this invoice, and the {lines} lines it does bill are carried at their own {rates} rates straight off the proforma — none of them recalculated for the delivery.",
                      {
                        document: seededDocuments[4]!.example,
                        balance: balancePieces,
                        lines: deliveredTotals.lines,
                        rates: new Set(seedSpecGroups[0]!.lines.map((line) => line.rate)).size,
                      },
                    )}
                  </p>
                </div>

                <div className="surface p-6">
                  <p className="eyebrow">{t("Why the balance matters")}</p>
                  <p className="mt-3 text-sm leading-relaxed text-text-muted">{t("A part delivery is where paper systems lose track. Because the delivery note records {v1} delivered against {v2} ordered, the outstanding {v3} pieces stay visible on the job rather than being re-typed from memory — and the invoice for Batch 2 will be raised from Batch 2's own dispatch, not from the original order.", { v1: deliveredTotals.pieces, v2: quotationTotals.pieces, v3: balancePieces })}</p>
                  <p className="mt-4">
                    <Link href="/features#delivery-note" className="link-underline text-sm text-brand-200">
          {t("How the delivery note and balance work")}
        </Link>
                  </p>
                </div>
              </div>
            </Reveal>
          </div>
        </Container>
      </Section>

      {/* ── The 2D view of the seeded job ────────────────────────── */}
      <Section aria-labelledby="nested-heading">
        <Container>
          <SectionHeading
            id="nested-heading"
            eyebrow={t("The 2D view")}
            title={t("Where those sheets go.")}
            lead={t(
              "A sheet store is only half the picture; the other half is what comes off the sheets. Below is the seeded job's own glass, nested on the seeded stock — {v1} panes of {v2} mm {v3} from {v4}, cut with the application's house profile for that thickness: a {v5} mm trim on all four edges and a {v6} mm blade gap.",
              {
                v1: seedRunPieces,
                v2: seedRunSheet.thicknessMm,
                v3: t(seedRunSheet.category),
                v4: seedRunSheet.supplier,
                v5: seedRunMargins.left,
                v6: seedRunMargins.kerf,
              },
            )}
          />

          {/*
            Behind the same gate as the module. The drawing is the seeded job's
            own glass and it is still one press away — Run optimization — but it
            is not on screen before a run is asked for, because the application
            does not show a plan it has not been asked for either, and the
            worked example is where a reader learns what the module does.
          */}
          <OptimizationRunGate
            rows={seedRunCutList}
            className="mt-10"
            note={t("{v1} · trim {v2} mm · kerf {v3} mm", {
              v1: seedRunSheet.label,
              v2: seedRunMargins.left,
              v3: seedRunMargins.kerf,
            })}
          >
            <PlateInspector
              plates={seedRunPlates}
              margins={seedRunMargins}
              minBreakoutMm={seedRunBreakoutMm}
              thicknessMm={seedRunSheet.thicknessMm}
              composition={t("{v1} mm {v2} · {v3}", {
                v1: seedRunSheet.thicknessMm,
                v2: t(seedRunSheet.category),
                v3: seedRunSheet.colour,
              })}
              showSchedule
              galleryColumns="grid-cols-2 sm:grid-cols-3 lg:grid-cols-4"
            />

            {/* The two notes below explain the drawing above them, so they
                appear with it rather than before it. */}
            <div className="mt-6 grid max-w-4xl gap-4 sm:grid-cols-2">
            <p className="text-xs font-medium leading-relaxed text-text-faint">
              {t(
                "The panes are grouped by the sheet they are bought on — one glass to a run, the way a shop actually cuts it — and this is the largest of those runs, so the drawing above is the seeded quotation's own glass and not a cut list written for the occasion. The store holds {available} sheets of this glass, {used} spoken for; the run takes {plates}.",
                {
                  available: seedRunTotals.sheetsAvailable,
                  used: seedRunSheet.used,
                  plates: seedRunTotals.plates,
                },
              )}
            </p>
            <p className="text-xs font-medium leading-relaxed text-text-faint">
              {t(
                "Why some plates carry only two panes: the run's tallest pane is {tallest} mm, and under it the trims and the blade gap leave {below} mm — short of the run's next size down at {smallest} mm. Beside it there is {beside} mm against a smallest width of {smallest} mm. That is the sheet size, not the packing: it is why a rack holds more than one, and why the engine searches other sizes as well as other patterns.",
                {
                  tallest: seedRunGeometry.tallest,
                  below: seedRunGeometry.belowTallest,
                  beside: seedRunGeometry.besideTallest,
                  smallest: seedRunGeometry.smallest,
                },
              )}
            </p>
          </div>
          </OptimizationRunGate>
        </Container>
      </Section>

      {/* ── The stock behind the job ─────────────────────────────── */}
      <Section tone="raised" aria-labelledby="stock-heading">
        <Container>
          <SectionHeading
            id="stock-heading"
            eyebrow={t("The stock behind it")}
            title={t("Eight sheets and ten consumables.")}
            lead={t("The sheet store and the consumable inventory are seeded alongside the job, because a glass business is not only glass. Available quantity is stock minus used — the application's own live fields, not a derived figure.")}
          />

          <Reveal className="mt-10">
            <div className="surface overflow-hidden">
              <div className="border-b border-line/60 bg-ink-900/60 px-5 py-4">
                <h3 className="text-base font-semibold text-text-primary">{t("Sheet store")}</h3>
              </div>
              <div className="overflow-x-auto">
                <table className="w-full min-w-[52rem] border-collapse text-sm">
                  <caption className="sr-only">{t("The eight glass sheets seeded into the sheet store.")}</caption>
                  <thead>
                    <tr className="border-b border-line/60 text-left">
                      {["Category", "Thickness", "Colour", "Sheet size (mm)", "Supplier", "Buy / m²", "Sell / m²", "Stock", "Used", "Available"].map((heading, index) => (
                        <th
                          key={heading}
                          scope="col"
                          className={`px-4 py-3 font-mono text-2xs uppercase tracking-[0.14em] text-text-faint font-medium ${index >= 5 ? "text-right" : ""}`}
                        >
                          {heading}
                        </th>
                      ))}
                    </tr>
                  </thead>
                  <tbody>
                    {seedSheets.map((sheet) => (
                      <tr key={`${t(sheet.category)}-${sheet.thicknessMm}-${sheet.colour}`} className="border-b border-line/40 last:border-0">
                        <td className="px-4 py-2.5 text-text-secondary">{t(sheet.category)}</td>
                        <td className="px-4 py-2.5 font-mono text-xs text-text-muted">{sheet.thicknessMm} mm</td>
                        <td className="px-4 py-2.5 text-text-muted">{sheet.colour}</td>
                        <td className="px-4 py-2.5 font-mono text-xs text-text-faint">
                          {mm(sheet.widthMm)} × {mm(sheet.heightMm)}
                        </td>
                        <td className="px-4 py-2.5 text-text-muted">{sheet.supplier}</td>
                        <td className="px-4 py-2.5 text-right text-text-muted tabular-nums">{aed(sheet.buyPerSqm)}</td>
                        <td className="px-4 py-2.5 text-right text-text-primary tabular-nums">{aed(sheet.sellPerSqm)}</td>
                        <td className="px-4 py-2.5 text-right text-text-secondary tabular-nums">{sheet.stock}</td>
                        <td className="px-4 py-2.5 text-right text-text-muted tabular-nums">{sheet.used}</td>
                        <td className="px-4 py-2.5 text-right font-medium text-text-primary tabular-nums">
                          {sheet.stock - sheet.used}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </div>
          </Reveal>

          <div className="mt-6 grid gap-6 lg:grid-cols-[minmax(0,1.35fr)_minmax(0,0.65fr)]">
            <Reveal>
              <div className="surface overflow-hidden">
                <div className="border-b border-line/60 bg-ink-900/60 px-5 py-4">
                  <h3 className="text-base font-semibold text-text-primary">{t("Consumables")}</h3>
                  <p className="mt-1 text-xs text-text-faint font-medium">{t("Spacer bar, desiccant, primary and secondary seal, gas and hardware — what an insulated unit needs beyond the glass.")}</p>
                </div>
                <div className="overflow-x-auto">
                  <table className="w-full min-w-[54rem] border-collapse text-sm">
                    <caption className="sr-only">{t("The ten consumable items seeded into inventory.")}</caption>
                    <thead>
                      <tr className="border-b border-line/60 text-left">
                        {["Item", "Part no.", "Specification", "Supplier", "Unit", "Buy", "Sell", "Stock", "Used", "Reorder at"].map((heading, index) => (
                          <th
                            key={heading}
                            scope="col"
                            className={`px-4 py-3 font-mono text-2xs uppercase tracking-[0.14em] text-text-faint font-medium ${index >= 5 ? "text-right" : ""}`}
                          >
                            {heading}
                          </th>
                        ))}
                      </tr>
                    </thead>
                    <tbody>
                      {seedConsumables.map((item) => {
                        const available = item.stock - item.used;
                        const low = available < item.reorderLevel;
                        return (
                          <tr key={item.partNumber} className="border-b border-line/40 last:border-0">
                            <td className="px-4 py-2.5 text-text-secondary">{t(item.name)}</td>
                            <td className="px-4 py-2.5 font-mono text-xs text-text-faint">{item.partNumber}</td>
                            <td className="px-4 py-2.5 text-text-muted">{t(item.specification)}</td>
                            <td className="px-4 py-2.5 text-text-muted">{item.supplier}</td>
                            <td className="px-4 py-2.5 text-xs text-text-faint">{t(item.uom)}</td>
                            <td className="px-4 py-2.5 text-right text-text-muted tabular-nums">{aed(item.buy)}</td>
                            <td className="px-4 py-2.5 text-right text-text-primary tabular-nums">{aed(item.sell)}</td>
                            <td className="px-4 py-2.5 text-right text-text-secondary tabular-nums">{item.stock}</td>
                            <td className="px-4 py-2.5 text-right text-text-muted tabular-nums">{item.used}</td>
                            <td
                              className={`px-4 py-2.5 text-right tabular-nums ${low ? "font-semibold text-state-warning" : "text-text-muted"}`}
                            >
                              {item.reorderLevel}
                              {low ? <span className="ml-2 font-mono text-[0.75rem]">LOW</span> : null}
                            </td>
                          </tr>
                        );
                      })}
                    </tbody>
                  </table>
                </div>
              </div>
            </Reveal>

            <Reveal delay={80}>
              <div className="space-y-4">
                <div className="surface p-6">
                  <p className="eyebrow">{t("One item is already low")}</p>
                  {reorderAlerts.map((item) => (
                    <p key={item.partNumber} className="mt-3 text-sm leading-relaxed text-text-muted">
                      {t(
                        "{part} — {name}, {specification}: {available} {uom} available against a reorder level of {reorder}. The store reports it without anyone having to go and count.",
                        {
                          part: item.partNumber,
                          name: t(item.name),
                          specification: t(item.specification),
                          available: item.stock - item.used,
                          uom: item.uom.toLowerCase(),
                          reorder: item.reorderLevel,
                        },
                      )}
                    </p>
                  ))}
                  <p className="mt-4 text-sm leading-relaxed text-text-muted">{t("The other nine are above their reorder level, so only this one raises a flag — which is what a store report is for.")}</p>
                </div>

                <div className="surface p-6">
                  <p className="eyebrow">{t("Buy and sell, side by side")}</p>
                  <p className="mt-3 text-sm leading-relaxed text-text-muted">{t("Every sheet and consumable carries a purchase price and a sell price. The quotation's rates come from the sell side and the margin a job made is read from the job's own recorded costs — which is why job profitability is a report on real charges rather than a spreadsheet reconstruction.")}</p>
                  <p className="mt-4">
                    <Link href="/features#job-profitability" className="link-underline text-sm text-brand-200">
          {t("How job profitability is worked out")}
        </Link>
                  </p>
                </div>
              </div>
            </Reveal>
          </div>
        </Container>
      </Section>

      {/* ── What it demonstrates ─────────────────────────────────── */}
      {/* ── The two rules, on the panels where they bite ────────────── */}
      <Section aria-labelledby="rules-heading" id="pricing-rules">
        <Container>
          <SectionHeading
            id="rules-heading"
            eyebrow={t("How a panel is priced")}
            title={t("Two rules, and neither is a rounding.")}
            lead={t("A shop does not quote glass by area alone. A pane too small to fill a square metre still costs a square metre of work, and one big enough to need a crane costs more per square metre than the rest of the specification. Both are in the pricing engine, both price every line on this page, and this job sits between them: its smallest lite is 0.80 m² and its largest is 3.84 m², so neither rule fires on it. Here they are on the panels that do.")}
          />

          <div className="mt-10 grid gap-5 lg:grid-cols-2">
            {pricingRules.map((rule) => (
              <Reveal key={rule.id}>
                <div className="surface-raised h-full p-6 sm:p-7">
                  <div className="flex flex-wrap items-baseline justify-between gap-2">
                    <p className="eyebrow">{t(rule.label)}</p>
                    <p className="font-mono text-sm font-semibold text-brand-200">{t(rule.value)}</p>
                  </div>
                  <h3 className="mt-3 text-xl font-semibold text-text-primary">{t(rule.headline)}</h3>
                  <p className="mt-3 text-sm font-medium leading-relaxed text-text-muted">{t(rule.body)}</p>
                  <p className="mt-4 font-mono text-2xs text-text-faint">{t(rule.evidence)}</p>
                </div>
              </Reveal>
            ))}
          </div>

          {/*
            The two panels the rules exist for, priced through the same
            functions as every line above. Neither is on the seeded job and
            neither number is typed in: `priceLight` runs `billablePanelSqm`,
            `isFloored` and the switch over the size and prints what comes out.
          */}
          <Reveal className="mt-6">
            <div className="overflow-x-auto">
              <table className="w-full min-w-[54rem] border-collapse text-sm">
                <caption className="sr-only">{t("Two panels at the edges of the two rules: a 400 × 300 mm vent light billed at the 0.5 m² floor, and a 2400 × 1800 mm atrium lite priced at base plus 20% because it reaches 4 m².")}</caption>
                <thead>
                  <tr className="border-b border-line/60 text-left">
                    <th scope="col" className={THL}>{t("Panel")}</th>
                    <th scope="col" className={THL}>{t("Glass ref")}</th>
                    <th scope="col" className={TH} title={t("Outer pane width, millimetres")}>W1 × H1</th>
                    <th scope="col" className={TH} title={t("Pieces on this line")}>QTY</th>
                    <th scope="col" className={TH} title={t("Width × height, before the floor")}>{t("PHYSICAL M²")}</th>
                    <th scope="col" className={TH} title={t("SqmCalculator.CalculatePanel — floored at 0.5 m²")}>{t("BILLED M²")}</th>
                    <th scope="col" className={TH} title={t("Which rule decided this row")}>RULE</th>
                    <th scope="col" className={TH} title={t("Base rate for the specification, then the rate after the switch")}>{t("BASE RATE")}</th>
                    <th scope="col" className={TH} title={t("The rate this row is charged at")}>{t("BILLED RATE")}</th>
                    <th scope="col" className={TH} title={t("Billed area × qty × billed rate")}>VALUE</th>
                  </tr>
                </thead>
                <tbody>
                  {ruleLights.map((light) => {
                    const priced = priceLight(light, STANDARD_SURCHARGE_PERCENT);
                    return (
                      <tr key={light.id} className="border-b border-line/40 last:border-0">
                        <td className="px-4 py-3">
                          <span className="font-medium text-text-secondary">{t(priced.name)}</span>
                          <span className="mt-0.5 block text-xs text-text-muted">{t(priced.note)}</span>
                        </td>
                        <td className="px-4 py-3 font-mono text-xs text-text-secondary">{priced.ref}</td>
                        <td className="px-4 py-3 text-right font-mono text-xs text-text-secondary tabular-nums">
                          {mm(priced.widthMm)} × {mm(priced.heightMm)}
                        </td>
                        <td className="px-4 py-3 text-right text-text-secondary tabular-nums">{priced.qty}</td>
                        <td className="px-4 py-3 text-right font-mono text-xs text-text-muted tabular-nums">
                          {priced.physical.toFixed(2)}
                        </td>
                        <td className="px-4 py-3 text-right font-mono text-xs tabular-nums">
                          <span className={priced.floored ? "text-brand-100" : "text-text-secondary"}>
                            {priced.billable.toFixed(2)}
                          </span>
                          {priced.floored ? (
                            <span className="ml-1.5 rounded-full border border-brand-400/45 bg-brand-400/12 px-1.5 py-px font-mono text-2xs text-brand-100">
                              floor
                            </span>
                          ) : null}
                        </td>
                        <td className="px-4 py-3 text-xs text-text-muted">
                          {priced.floored
                            ? t.template`Minimum area · ${MIN_PANEL_SQM} m² · rate unchanged`
                            : t.template`Large-panel rate · +${STANDARD_SURCHARGE_PERCENT}% at ${SURCHARGE_THRESHOLD_SQM} m²`}
                        </td>
                        <td className="px-4 py-3 text-right font-mono text-xs text-text-muted tabular-nums">{aed(priced.baseRate)}</td>
                        <td
                          className={cnCell(
                            "px-4 py-3 text-right font-mono text-xs tabular-nums",
                            priced.surcharged ? "text-money-positive" : "text-text-secondary",
                          )}
                        >
                          {aed(priced.rate)}
                        </td>
                        <td className="px-4 py-3 text-right font-medium text-text-primary tabular-nums">{aed(priced.value)}</td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          </Reveal>

          {/*
            The cost side, from the seeder's own stock rows. The selling column
            of each of these is the rate the quotation bills at — Clear 12 mm
            165 / 245 against the CT-12 lines at 245 — so the two columns
            reconcile, which is the reconciliation Step 9 performs.
          */}
          <Reveal className="mt-10">
            <div className="grid gap-5 lg:grid-cols-[minmax(0,1fr)_minmax(0,1fr)]">
              <div className="surface-raised p-6 sm:p-7">
                <p className="eyebrow">{t("The cost behind the rate")}</p>
                <h3 className="mt-2 text-xl font-semibold text-text-primary">{t("Step 9 costs these same panels at the purchase price.")}</h3>
                <p className="mt-3 text-sm font-medium leading-relaxed text-text-muted">{t("Step 9 is the application's only profit-and-loss step, and it asks one question: is the given price above target? Revenue is what the specification bills; the glass cost is the same billable square metres at the Sheet Store purchase price; the target is the base price plus the expected margin. Every row below is a seeded stock line, and the sell column is the rate the quotation above charges — which is why the two figures reconcile rather than merely agreeing.")}</p>
              </div>

              <div className="overflow-x-auto">
                <table className="w-full min-w-[30rem] border-collapse text-sm">
                  <caption className="sr-only">{t("Seeded sheet stock: purchase price, selling price, and the glass references priced from each.")}</caption>
                  <thead>
                    <tr className="border-b border-line/60 text-left">
                      <th scope="col" className={THL}>{t("Glass")}</th>
                      <th scope="col" className={TH} title={t("GlassSheet.PurchasePricePerSqm")}>{t("BUY / M²")}</th>
                      <th scope="col" className={TH} title={t("GlassSheet.SellPricePerSqm — the rate Step 5 bills at")}>{t("SELL / M²")}</th>
                      <th scope="col" className={THL}>{t("Prices")}</th>
                    </tr>
                  </thead>
                  <tbody>
                    {sheetCosts.map((sheet) => {
                      const priced = seedSpecGroups.flatMap((group) =>
                        group.lines.filter((line) => sheetForRef(line.ref)?.key === sheet.key),
                      );
                      return (
                        <tr key={sheet.key} className="border-b border-line/40 last:border-0">
                          <td className="px-4 py-2.5">
                            <span className="font-medium text-text-secondary">{t(sheet.label)}</span>
                            <span className="mt-0.5 block font-mono text-xs text-text-faint">
                              {sheet.sheetMm} · {sheet.supplier}
                            </span>
                          </td>
                          <td className="px-4 py-2.5 text-right font-mono text-xs text-text-secondary tabular-nums">
                            {aed(sheet.purchase)}
                          </td>
                          <td className="px-4 py-2.5 text-right font-mono text-xs text-text-secondary tabular-nums">
                            {aed(sheet.sell)}
                          </td>
                          <td className="px-4 py-2.5 text-xs text-text-muted">
                            {priced.length > 0 ? `${priced.length} line${priced.length === 1 ? "" : "s"}` : "not on this job"}
                          </td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>
            </div>
          </Reveal>
        </Container>
      </Section>

      <Section aria-labelledby="proves-heading">
        <Container>
          <SectionHeading
            id="proves-heading"
            eyebrow={t("What this demonstrates")}
            title={t("Traceability, shown rather than claimed.")}
            lead={t("The defining property of this application is that a job can be followed from the enquiry that started it to the money it made. Everything on this page exists because the software keeps those links — not because anyone wrote a summary of them.")}
          />

          <div className="mt-10 grid gap-5 sm:grid-cols-2 xl:grid-cols-3">
            {[
              {
                title: t("The same data, six times"),
                body: t.template`One job appears as ${seededDocuments[0]!.example}, ${seededDocuments[1]!.example}, ${seededDocuments[2]!.example}, ${seededDocuments[3]!.example} and ${seededDocuments[4]!.example} — and the customer, project and specification are not retyped at any of them.`,
              },
              {
                title: t("Pricing carries its basis"),
                body: t.template`Each of the ${quotationTotals.lines} lines keeps its reference, size, rate and the group it was priced in. Change a rate and the line value, the group subtotal and the total all follow from it.`,
              },
              {
                title: t("Partial is a first-class state"),
                body: t.template`Forty-five pieces dispatched against ${quotationTotals.pieces} ordered is recorded as a balance of ${balancePieces} on the job, not as a discrepancy someone has to notice later.`,
              },
              {
                title: t("Money rounds in one place"),
                body: t("Every value here went through the same two-decimal, half-away-from-zero rule, and VAT through the one calculator — with the totals verified against the seeder at build time."),
              },
              {
                title: t("Stock and job are connected"),
                body: t("The rates on the quotation are the sell prices of the sheets and consumables in the store. Stock levels, usage and reorder levels are live fields, not a report generated afterwards."),
              },
              {
                title: t("It refuses to run on real data"),
                body: t("Seeding is blocked when the database already holds records, so this workflow can never be mixed into a business's own books. It exists to be looked at, then cleared."),
              },
            ].map((item, index) => (
              <Reveal key={t(item.title)} delay={Math.min(index, 5) * 45}>
                <article className="surface h-full p-5">
                  <h3 className="text-sm font-semibold text-text-primary">{t(item.title)}</h3>
                  <p className="mt-2.5 text-sm leading-relaxed text-text-muted">{t(item.body)}</p>
                </article>
              </Reveal>
            ))}
          </div>

          <p className="mt-8 max-w-4xl text-xs leading-relaxed text-text-faint font-medium">{t("Nothing on this page is a performance claim. No time saved, no cost reduction, no percentage improvement is quoted — the application does not measure those, and neither does this site. The figures are the sample dataset's own numbers, reproduced exactly as the software stores them.")}</p>
        </Container>
      </Section>

      {/* ── CTA ──────────────────────────────────────────────────── */}
      <Section tone="raised" aria-label={t("Next steps")}>
        <Container>
          <Reveal>
            <div className="surface-raised flex flex-col items-center gap-6 p-8 text-center sm:p-10">
              <div>
                <p className="eyebrow">{t("See it on your own numbers")}</p>
                <h2 className="mt-3 text-2xl leading-tight sm:text-3xl">
          {t("The seeded job is a demonstration. Yours would be your own.")}
        </h2>
                <p className="lede mt-3 max-w-2xl">{t("Ask for a demo and the same walkthrough is run against your specification groups, your thicknesses and your supplier pricing. There is no price list to publish and nothing to sign up for — just an email back.")}</p>
              </div>
              <div className="flex flex-wrap justify-center gap-3">
                <ButtonLink href="/contact" size="lg" trailing={<ArrowRight />}>
          {t("Request a demo")}
        </ButtonLink>
                <ButtonLink href="/screenshots" variant="secondary" size="lg">
          {t("See the screens")}
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

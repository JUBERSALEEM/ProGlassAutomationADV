import type { Metadata } from "next";
import Link from "next/link";

import { Container, Section } from "@/components/ui/Container";
import { PageHero } from "@/components/layout/PageHero";
import { ArrowRight, ButtonLink } from "@/components/ui/Button";
import { Badge } from "@/components/ui/Badge";
import { Reveal } from "@/components/ui/Reveal";
import { SectionHeading } from "@/components/ui/SectionHeading";
import { StatBadge } from "@/components/ui/Stat";
import { RouteSchema } from "@/components/seo/RouteSchema";
import { pageMetadata } from "@/lib/metadata";
import {
  ROUNDING_SOURCE,
  SEED_SOURCE,
  VAT_SOURCE,
  balancePieces,
  batchOneWeightKg,
  deliveredTotals,
  groupTotals,
  lineArea,
  lineValue,
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

export const metadata: Metadata = pageMetadata({
  title: "Worked example — one glass job, end to end",
  description:
    "One job through ProGlass Automation: 22 quotation lines, 83 pieces and AED 34,762.19 across three specification groups, a partial delivery and its tax invoice.",
  path: "/worked-example",
  keywords: [
    "glass fabrication ERP example",
    "glass quotation calculation",
    "glass delivery balance",
    "VAT invoice glass",
    "cutting optimization example",
  ],
  ogEyebrow: "Worked example",
});

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

export default function WorkedExamplePage() {
  return (
    <>
      <RouteSchema
        path="/worked-example"
        title="Worked example — one glass job, end to end"
        description="One job through ProGlass Automation: 22 quotation lines, 83 pieces and AED 34,762.19 across three specification groups, a partial delivery and its tax invoice."
      />

      <PageHero
        eyebrow="Worked example"
        title="One job, all the way through."
        lead="Not a mock-up and not a market rate: this is the demonstration job the application seeds into an empty database, read back line by line. The customer, the project, the 22 quotation lines, the sheet stock behind them, the partial delivery and the invoice that followed are the records the software itself creates."
        banner="calculator"
        breadcrumb={[
          { label: "Home", href: "/" },
          { label: "Worked example", href: "/worked-example" },
        ]}
      >
        <div className="mt-8 flex flex-wrap gap-3">
          <ButtonLink href="#quotation" size="lg" trailing={<ArrowRight />}>
            See the quotation
          </ButtonLink>
          <ButtonLink href="/contact" variant="secondary" size="lg">
            Request a demo
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
                eyebrow="Where this comes from"
                title="The application seeds this job itself."
                lead="ProGlass Automation can load one complete demonstration workflow into an empty database. It is a single connected job — one enquiry, one quotation, one job order, one partial delivery, one tax invoice — with the same data flowing through every stage, and it refuses to run at all if the database already holds real records."
              />

              <div className="mt-8 space-y-4">
                <div className="surface p-5">
                  <p className="mono-label">Source of every figure on this page</p>
                  <p className="mt-2 font-mono text-sm text-brand-200">{SEED_SOURCE}</p>
                  <p className="mt-3 text-sm leading-relaxed text-text-muted">
                    Read the seeded records out of that file and you will find the customer, the project reference,
                    each glass reference, each size, each rate, the sheet stock and the consumables reproduced below.
                  </p>
                </div>

                <div className="surface p-5">
                  <p className="mono-label">How the money is rounded</p>
                  <p className="mt-2 font-mono text-sm text-brand-200">
                    {ROUNDING_SOURCE} · {VAT_SOURCE}
                  </p>
                  <p className="mt-3 text-sm leading-relaxed text-text-muted">
                    Two decimal places, half away from zero, and 5% VAT. The arithmetic on this page is performed the
                    same way rather than copied from the file, and a build-time check fails if the two ever disagree.
                  </p>
                </div>
              </div>

              <div className="surface mt-4 border-state-warning/30 p-5">
                <div className="flex flex-wrap items-center gap-3">
                  <Badge tone="warning">Read this before quoting anything</Badge>
                </div>
                <p className="mt-3 text-sm leading-relaxed text-text-secondary">
                  The rates below are the sample dataset&apos;s own numbers, chosen so the demonstration adds up. They
                  are <strong className="font-semibold text-text-primary">not a price list</strong>, not a market rate
                  and not a quotation from anyone. ProGlass Automation publishes no price list for glass, and this page
                  does not change that: it shows how the software carries a job, not what glass costs.
                </p>
              </div>
            </div>

            <Reveal delay={80}>
              <div className="space-y-4">
                <div className="grid gap-4 sm:grid-cols-2">
                  <StatBadge value={String(quotationTotals.lines)} label="Quotation lines" hint="Across three specification groups" />
                  <StatBadge value={String(quotationTotals.pieces)} label="Pieces of glass" hint="SGU and DGU panels" />
                  <StatBadge
                    value={`${plain(quotationTotals.squareMetres)} m²`}
                    label="Total glazed area"
                    hint="Sum of every line, not an estimate"
                  />
                  <StatBadge value={`AED ${aed(quotationTotals.grandTotal)}`} label="Quotation, VAT inclusive" hint="Sample rates — not a market price" />
                </div>

                <div className="surface p-5">
                  <p className="mono-label">The job in one paragraph</p>
                  <p className="mt-3 text-sm leading-relaxed text-text-muted">
                    {seedParties.customer.name} asked for clear tempered, tinted and Low-E glass for the podium facade
                    of {seedParties.project.name} in {seedParties.project.site}. The enquiry arrived with an indicative
                    value of AED {aed(seedParties.inquiryValue)}; by the time it was priced it had become{" "}
                    {quotationTotals.lines} lines and {quotationTotals.pieces} pieces. Batch 1 of 2 has left the
                    factory, and the invoice has been raised on what actually went out.
                  </p>
                  <p className="mt-3 text-sm leading-relaxed text-text-muted">
                    Contract {seedParties.project.contract} · LPO {seedParties.project.lpo} · site reference{" "}
                    {seedParties.project.reference}.
                  </p>
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
            eyebrow="The chain"
            title="Five documents, one job."
            lead="Each document is raised from the one before it rather than retyped. The number format is the application's own — the year is taken from the machine the data was seeded on, so 2026 here is an example of that year, not a fixed part of the format."
          />

          <ol className="mt-12 grid gap-4 xl:grid-cols-5">
            {seededDocuments.map((document, index) => (
              <Reveal key={document.key} as="li" delay={Math.min(index, 4) * 50}>
                <article className="surface flex h-full flex-col p-5">
                  <div className="flex items-baseline justify-between gap-3">
                    <span className="font-mono text-2xs uppercase tracking-[0.18em] text-brand-300">
                      {String(index + 1).padStart(2, "0")}
                    </span>
                    <Badge tone={document.key === "tax-invoice" ? "accent" : "neutral"}>{document.status}</Badge>
                  </div>

                  <h3 className="mt-3 text-base font-semibold text-text-primary">{document.title}</h3>
                  <p className="mt-1 font-mono text-xs text-text-faint">{document.example}</p>

                  <p className="mt-3 text-sm leading-relaxed text-text-muted">{document.summary}</p>

                  <p className="mt-auto border-t border-line/60 pt-4 text-xs leading-relaxed text-text-faint font-medium">
                    {document.handoff}
                  </p>
                  <p className="mt-3 font-mono text-[0.75rem] leading-relaxed text-text-faint">{document.evidence}</p>
                </article>
              </Reveal>
            ))}
          </ol>

          <p className="mt-6 text-xs leading-relaxed text-text-faint font-medium">
            Number patterns as the seeder builds them: {seededDocuments.map((document) => document.pattern).join(" · ")}.
          </p>
        </Container>
      </Section>

      {/* ── The quotation, line by line ──────────────────────────── */}
      <Section tone="raised" aria-labelledby="quotation-heading" id="quotation">
        <Container>
          <SectionHeading
            id="quotation-heading"
            eyebrow="The quotation"
            title="Twenty-two lines, priced three ways."
            lead="The proforma is priced by specification group: clear tempered for the main facade, tinted where the sun hits, and a double-glazed Low-E unit for the energy-efficient zones. Each line carries its specification exactly as Step 5 holds it — serial, glass reference, outer pane W1 × H1, inner pane W2 × H2 where there is one, quantity, area, rate and value."
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
            <span className="font-mono text-text-faint">#</span> is the line&rsquo;s Sr No — the serial the application
            stores on every cutting line, and the number the grid sorts by.{" "}
            <span className="font-mono text-text-faint">TOTAL SQM</span> is SQM × QTY. A{" "}
            <span className="font-mono text-text-faint">—</span> in W2 or H2 means the line has no second pane: the
            application stores <span className="font-mono text-text-faint">0</span> in those cells for single glazing,
            while a double-glazed line carries both pairs — and in this seeded job the two panes are cut to the same
            size.
          </p>

          <div className="mt-6 flex flex-wrap gap-2">
            {groupTotals.map((group) => (
              <span
                key={group.number}
                className="rounded-full border border-line-strong/70 px-3.5 py-1.5 text-xs font-medium text-text-muted"
              >
                Group {group.number}: {group.name}
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
                      <h3 className="mt-1.5 text-base font-semibold text-text-primary">{group.name}</h3>
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
                        GLASS REF · W1 H1 W2 H2 · QTY · SQM · T.SQM · RATE · VALUE.
                      </p>
                    </div>
                    <p className="font-mono text-sm text-brand-200">AED {aed(totals.subtotal)}</p>
                  </div>

                  <div className="overflow-x-auto">
                    <table className="w-full min-w-[62rem] border-collapse text-sm">
                      <caption className="sr-only">
                        Group {group.number}, {group.name}: every quotation line with its size, area, rate and value.
                      </caption>
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
                          <th scope="col" className={THL} title="Sr No — the line's serial number (Dg.Hash)">#</th>
                          <th scope="col" className={THL}>GLASS REF</th>
                          <th scope="col" className={TH} title="Outer pane width, millimetres">W1</th>
                          <th scope="col" className={TH} title="Outer pane height, millimetres">H1</th>
                          <th scope="col" className={TH} title="Inner pane width, millimetres — double-glazed lines only">W2</th>
                          <th scope="col" className={TH} title="Inner pane height, millimetres — double-glazed lines only">H2</th>
                          <th scope="col" className={TH} title="Pieces on this line">QTY</th>
                          <th scope="col" className={TH} title="Unit panel area">SQM</th>
                          <th scope="col" className={TH} title="Total SQM (SQM × Qty)">TOTAL SQM</th>
                          <th scope="col" className={TH} title="Rate per square metre">RATE</th>
                          <th scope="col" className={TH} title="Line value from the rate">VALUE</th>
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
                            <td className="px-4 py-2.5 text-right text-text-muted tabular-nums">{plain(pieceArea(line))}</td>
                            <td className="px-4 py-2.5 text-right text-text-muted tabular-nums">{plain(lineArea(line))}</td>
                            <td className="px-4 py-2.5 text-right text-text-muted tabular-nums">{aed(line.rate)}</td>
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
              <div className="plate p-6 sm:p-8">
                <p className="eyebrow">Quotation total</p>
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
                    <dt className="text-text-muted">Discount</dt>
                    <dd className="font-mono text-text-primary tabular-nums">AED 0.00</dd>
                  </div>
                  <div className="flex items-baseline justify-between gap-4 border-b border-line/50 pb-3">
                    <dt className="text-text-muted">
                      VAT at 5%
                      <span className="block text-xs text-text-faint">One centralised VAT calculator</span>
                    </dt>
                    <dd className="font-mono text-text-primary tabular-nums">AED {aed(quotationTotals.vat)}</dd>
                  </div>
                  <div className="flex items-baseline justify-between gap-4 pt-1">
                    <dt className="text-base font-semibold text-text-primary">Grand total</dt>
                    <dd className="font-mono text-lg font-semibold text-brand-100 tabular-nums">
                      AED {aed(quotationTotals.grandTotal)}
                    </dd>
                  </div>
                </dl>
                <p className="mt-5 text-xs leading-relaxed text-text-faint font-medium">
                  Line values are rounded individually, then summed — which is how the application prices a proforma,
                  and why this total is not simply area × rate.
                </p>
              </div>

              <div className="space-y-4">
                <div className="surface p-6">
                  <p className="eyebrow">What the figures say</p>
                  <ul className="mt-4 space-y-3 text-sm leading-relaxed text-text-muted">
                    <li className="flex gap-3">
                      <span aria-hidden="true" className="mt-2 h-1 w-1 shrink-0 rounded-full bg-brand-400/80" />
                      <span>
                        The most expensive line is not the largest quantity. {groupTotals[2]!.name.split(" — ")[0]} runs{" "}
                        {groupTotals[2]!.lines} lines for {groupTotals[2]!.pieces} pieces, but at AED{" "}
                        {aed(seedSpecGroups[2]!.baseRate)}/m² it is worth AED {aed(groupTotals[2]!.subtotal)} — more than
                        the tinted group, which has more lines and more pieces.
                      </span>
                    </li>
                    <li className="flex gap-3">
                      <span aria-hidden="true" className="mt-2 h-1 w-1 shrink-0 rounded-full bg-brand-400/80" />
                      <span>
                        A double-glazed line weighs twice its glass plus the cavity. The heaviest piece in the job is{" "}
                        {(() => {
                          const heaviest = seedSpecGroups
                            .flatMap((group) => group.lines)
                            .reduce((best, line) => (pieceWeight(line) > pieceWeight(best) ? line : best));
                          return (
                            <>
                              {heaviest.ref} at {plain(pieceWeight(heaviest), 1)} kg —{" "}
                              {plain(pieceArea(heaviest))} m² of 24 mm nominal build-up
                            </>
                          );
                        })()}
                        . Batch 1 alone is {plain(batchOneWeightKg, 1)} kg on the truck.
                      </span>
                    </li>
                    <li className="flex gap-3">
                      <span aria-hidden="true" className="mt-2 h-1 w-1 shrink-0 rounded-full bg-brand-400/80" />
                      <span>
                        The enquiry was captured at AED {aed(seedParties.inquiryValue)}. The quotation came out at AED{" "}
                        {aed(quotationTotals.grandTotal)} VAT inclusive, or AED {aed(quotationTotals.subtotal)} before
                        it — the difference between an indicative number taken by the sales desk and a priced one.
                      </span>
                    </li>
                  </ul>
                </div>

                <div className="surface p-6">
                  <p className="eyebrow">Built up, not guessed at</p>
                  <p className="mt-3 text-sm leading-relaxed text-text-muted">
                    Every rate above matches the sell price of the sheet that glass comes from — 6 mm clear tempered at
                    150, 8 mm at 185, 10 mm at 210, 12 mm at 245, tinted at 175 and 195, and the Double-glazed unit at 260.
                    That consistency is the point of the sample dataset: the stock in the store, the rate on the
                    quotation and the value on the invoice are the same underlying job data.
                  </p>
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
            eyebrow="The partial delivery"
            title="Forty-five pieces out, thirty-eight still owed."
            lead="The delivery note is created as Dispatching and carries only what actually leaves. Batch 1 is the ten Clear Tempered lines; the tinted and Low-E pieces stay on the job with their full quantity as balance, waiting for Batch 2."
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
                    <caption className="sr-only">Delivered and outstanding quantity per specification group.</caption>
                    <thead>
                      <tr className="border-b border-line/60 text-left">
                        <th scope="col" className="px-4 py-3 font-mono text-2xs uppercase tracking-[0.14em] text-text-faint font-medium">Spec group</th>
                        <th scope="col" className="px-4 py-3 text-right font-mono text-2xs uppercase tracking-[0.14em] text-text-faint font-medium">Ordered</th>
                        <th scope="col" className="px-4 py-3 text-right font-mono text-2xs uppercase tracking-[0.14em] text-text-faint font-medium">Delivered</th>
                        <th scope="col" className="px-4 py-3 text-right font-mono text-2xs uppercase tracking-[0.14em] text-text-faint font-medium">Balance</th>
                        <th scope="col" className="px-4 py-3 font-mono text-2xs uppercase tracking-[0.14em] text-text-faint font-medium">Batch</th>
                      </tr>
                    </thead>
                    <tbody>
                      {groupTotals.map((group, index) => {
                        const delivered = index === 0 ? group.pieces : 0;
                        return (
                          <tr key={group.number} className="border-b border-line/40 last:border-0">
                            <td className="px-4 py-3 text-text-secondary">
                              <span className="font-mono text-xs text-text-faint">{group.number}</span>{" "}
                              {group.name}
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
                        <td className="px-4 py-3 text-xs font-medium text-text-secondary">Job total</td>
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
                  <p className="eyebrow">What was invoiced</p>
                  <dl className="mt-4 space-y-3 text-sm">
                    <div className="flex items-baseline justify-between gap-4 border-b border-line/50 pb-3">
                      <dt className="text-text-muted">
                        Dispatched glass
                        <span className="block text-xs text-text-faint">
                          {deliveredTotals.pieces} pieces · {plain(deliveredTotals.squareMetres)} m²
                        </span>
                      </dt>
                      <dd className="font-mono text-text-primary tabular-nums">AED {aed(deliveredTotals.subtotal)}</dd>
                    </div>
                    <div className="flex items-baseline justify-between gap-4 border-b border-line/50 pb-3">
                      <dt className="text-text-muted">VAT at 5%</dt>
                      <dd className="font-mono text-text-primary tabular-nums">AED {aed(deliveredTotals.vat)}</dd>
                    </div>
                    <div className="flex items-baseline justify-between gap-4">
                      <dt className="font-semibold text-text-primary">Invoice total</dt>
                      <dd className="font-mono font-semibold text-brand-100 tabular-nums">
                        AED {aed(deliveredTotals.total)}
                      </dd>
                    </div>
                  </dl>
                  <p className="mt-5 text-xs leading-relaxed text-text-faint font-medium">
                    {seededDocuments[4]!.example} is raised from the dispatched quantity only. The balance of{" "}
                    {balancePieces} pieces stays on the job order and is not billed on this invoice, and the{" "}
                    {deliveredTotals.lines} lines it does bill are carried at their own{" "}
                    {new Set(seedSpecGroups[0]!.lines.map((line) => line.rate)).size} rates straight off the proforma —
                    none of them recalculated for the delivery.
                  </p>
                </div>

                <div className="surface p-6">
                  <p className="eyebrow">Why the balance matters</p>
                  <p className="mt-3 text-sm leading-relaxed text-text-muted">
                    A part delivery is where paper systems lose track. Because the delivery note records{" "}
                    {deliveredTotals.pieces} delivered against {quotationTotals.pieces} ordered, the outstanding{" "}
                    {balancePieces} pieces stay visible on the job rather than being re-typed from memory — and the
                    invoice for Batch 2 will be raised from Batch 2&apos;s own dispatch, not from the original order.
                  </p>
                  <p className="mt-4">
                    <Link href="/features#delivery-note" className="link-underline text-sm text-brand-200">
                      How the delivery note and balance work
                    </Link>
                  </p>
                </div>
              </div>
            </Reveal>
          </div>
        </Container>
      </Section>

      {/* ── The stock behind the job ─────────────────────────────── */}
      <Section tone="raised" aria-labelledby="stock-heading">
        <Container>
          <SectionHeading
            id="stock-heading"
            eyebrow="The stock behind it"
            title="Eight sheets and ten consumables."
            lead="The sheet store and the consumable inventory are seeded alongside the job, because a glass business is not only glass. Available quantity is stock minus used — the application's own live fields, not a derived figure."
          />

          <Reveal className="mt-10">
            <div className="surface overflow-hidden">
              <div className="border-b border-line/60 bg-ink-900/60 px-5 py-4">
                <h3 className="text-base font-semibold text-text-primary">Sheet store</h3>
              </div>
              <div className="overflow-x-auto">
                <table className="w-full min-w-[52rem] border-collapse text-sm">
                  <caption className="sr-only">The eight glass sheets seeded into the sheet store.</caption>
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
                      <tr key={`${sheet.category}-${sheet.thicknessMm}-${sheet.colour}`} className="border-b border-line/40 last:border-0">
                        <td className="px-4 py-2.5 text-text-secondary">{sheet.category}</td>
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
                  <h3 className="text-base font-semibold text-text-primary">Consumables</h3>
                  <p className="mt-1 text-xs text-text-faint font-medium">
                    Spacer bar, desiccant, primary and secondary seal, gas and hardware — what an insulated unit needs
                    beyond the glass.
                  </p>
                </div>
                <div className="overflow-x-auto">
                  <table className="w-full min-w-[54rem] border-collapse text-sm">
                    <caption className="sr-only">The ten consumable items seeded into inventory.</caption>
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
                            <td className="px-4 py-2.5 text-text-secondary">{item.name}</td>
                            <td className="px-4 py-2.5 font-mono text-xs text-text-faint">{item.partNumber}</td>
                            <td className="px-4 py-2.5 text-text-muted">{item.specification}</td>
                            <td className="px-4 py-2.5 text-text-muted">{item.supplier}</td>
                            <td className="px-4 py-2.5 text-xs text-text-faint">{item.uom}</td>
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
                  <p className="eyebrow">One item is already low</p>
                  {reorderAlerts.map((item) => (
                    <p key={item.partNumber} className="mt-3 text-sm leading-relaxed text-text-muted">
                      <span className="font-mono text-text-secondary">{item.partNumber}</span> — {item.name},{" "}
                      {item.specification}: {item.stock - item.used} {item.uom.toLowerCase()}
                      {item.stock - item.used === 1 ? "" : "s"} available against a reorder level of{" "}
                      {item.reorderLevel}. The store reports it without anyone having to go and count.
                    </p>
                  ))}
                  <p className="mt-4 text-sm leading-relaxed text-text-muted">
                    The other nine are above their reorder level, so only this one raises a flag — which is what a store
                    report is for.
                  </p>
                </div>

                <div className="surface p-6">
                  <p className="eyebrow">Buy and sell, side by side</p>
                  <p className="mt-3 text-sm leading-relaxed text-text-muted">
                    Every sheet and consumable carries a purchase price and a sell price. The quotation&apos;s rates come
                    from the sell side and the margin a job made is read from the job&apos;s own recorded costs — which
                    is why job profitability is a report on real charges rather than a spreadsheet reconstruction.
                  </p>
                  <p className="mt-4">
                    <Link href="/features#job-profitability" className="link-underline text-sm text-brand-200">
                      How job profitability is worked out
                    </Link>
                  </p>
                </div>
              </div>
            </Reveal>
          </div>
        </Container>
      </Section>

      {/* ── What it demonstrates ─────────────────────────────────── */}
      <Section aria-labelledby="proves-heading">
        <Container>
          <SectionHeading
            id="proves-heading"
            eyebrow="What this demonstrates"
            title="Traceability, shown rather than claimed."
            lead="The defining property of this application is that a job can be followed from the enquiry that started it to the money it made. Everything on this page exists because the software keeps those links — not because anyone wrote a summary of them."
          />

          <div className="mt-10 grid gap-5 sm:grid-cols-2 xl:grid-cols-3">
            {[
              {
                title: "The same data, six times",
                body: `One job appears as ${seededDocuments[0]!.example}, ${seededDocuments[1]!.example}, ${seededDocuments[2]!.example}, ${seededDocuments[3]!.example} and ${seededDocuments[4]!.example} — and the customer, project and specification are not retyped at any of them.`,
              },
              {
                title: "Pricing carries its basis",
                body: `Each of the ${quotationTotals.lines} lines keeps its reference, size, rate and the group it was priced in. Change a rate and the line value, the group subtotal and the total all follow from it.`,
              },
              {
                title: "Partial is a first-class state",
                body: `Forty-five pieces dispatched against ${quotationTotals.pieces} ordered is recorded as a balance of ${balancePieces} on the job, not as a discrepancy someone has to notice later.`,
              },
              {
                title: "Money rounds in one place",
                body: `Every value here went through the same two-decimal, half-away-from-zero rule, and VAT through the one calculator — with the totals verified against the seeder at build time.`,
              },
              {
                title: "Stock and job are connected",
                body: "The rates on the quotation are the sell prices of the sheets and consumables in the store. Stock levels, usage and reorder levels are live fields, not a report generated afterwards.",
              },
              {
                title: "It refuses to run on real data",
                body: "Seeding is blocked when the database already holds records, so this workflow can never be mixed into a business's own books. It exists to be looked at, then cleared.",
              },
            ].map((item, index) => (
              <Reveal key={item.title} delay={Math.min(index, 5) * 45}>
                <article className="surface h-full p-5">
                  <h3 className="text-sm font-semibold text-text-primary">{item.title}</h3>
                  <p className="mt-2.5 text-sm leading-relaxed text-text-muted">{item.body}</p>
                </article>
              </Reveal>
            ))}
          </div>

          <p className="mt-8 max-w-4xl text-xs leading-relaxed text-text-faint font-medium">
            Nothing on this page is a performance claim. No time saved, no cost reduction, no percentage improvement is
            quoted — the application does not measure those, and neither does this site. The figures are the sample
            dataset&apos;s own numbers, reproduced exactly as the software stores them.
          </p>
        </Container>
      </Section>

      {/* ── CTA ──────────────────────────────────────────────────── */}
      <Section tone="raised" aria-label="Next steps">
        <Container>
          <Reveal>
            <div className="plate flex flex-col gap-6 p-8 sm:flex-row sm:items-center sm:justify-between sm:p-10">
              <div>
                <p className="eyebrow">See it on your own numbers</p>
                <h2 className="mt-3 text-2xl leading-tight sm:text-3xl">
                  The seeded job is a demonstration. Yours would be your own.
                </h2>
                <p className="lede mt-3 max-w-2xl">
                  Ask for a demo and the same walkthrough is run against your specification groups, your thicknesses and
                  your supplier pricing. There is no price list to publish and nothing to sign up for — just an email
                  back.
                </p>
              </div>
              <div className="flex shrink-0 flex-wrap gap-3">
                <ButtonLink href="/contact" size="lg" trailing={<ArrowRight />}>
                  Request a demo
                </ButtonLink>
                <ButtonLink href="/screenshots" variant="secondary" size="lg">
                  See the screens
                </ButtonLink>
              </div>
            </div>
          </Reveal>
        </Container>
      </Section>
    </>
  );
}

import type { Metadata } from "next";
import { Container, Section } from "@/components/ui/Container";
import { PageHero } from "@/components/layout/PageHero";
import { ArrowRight, ButtonLink } from "@/components/ui/Button";
import { Badge, IndexChip } from "@/components/ui/Badge";
import { Reveal } from "@/components/ui/Reveal";
import { SectionHeading } from "@/components/ui/SectionHeading";
import { Accordion } from "@/components/ui/Accordion";
import { AppFrame } from "@/components/product/AppFrame";
import { LiveSurface } from "@/components/product/LiveSurface";
import { NestingDemo } from "@/components/optimization/NestingDemo";
import { StrategyPicker } from "@/components/optimization/StrategyPicker";
import { ProductSlider } from "@/components/product/ProductSlider";
import { ScreenshotFrame } from "@/components/gallery/ScreenshotFrame";
import { screenshotById } from "@/data/screenshots";
import {
  cutComplexities,
  engineHonestyNotes,
  enginePipeline,
  optimizationResultFields,
  optimizerTabs,
  thicknessProfiles,
  verificationOracles,
} from "@/data/optimization";
import { RouteSchema } from "@/components/seo/RouteSchema";
import { pageMetadata } from "@/lib/metadata";
import { translatorFor } from "@/i18n/server";
import { useT } from "@/i18n/provider";

type PageProps = { params: Promise<{ locale: string }> };

export async function generateMetadata({ params }: PageProps): Promise<Metadata> {
  const t = await translatorFor(params);
  return pageMetadata({
  locale: t.locale,
  title: t("Glass cutting optimization and guillotine nesting"),
  description: t("How ProGlass nests glass parts on stock plates: safety edges, kerf, the guillotine cut sequence, verification and export to Bottero, Intermac, Lisec and DXF."),
  path: "/optimization",
  keywords: ["glass cutting optimization software", "guillotine nesting", "glass optimization software", t("CNC glass cutting export"), t("Bottero OPT export"), t("Lisec DAT export"), "glass cutting layout software"],
  ogEyebrow: t("Cutting plans"),
  });
}

const tabShots = ["optimization-settings", "optimization-2d", "optimization-report"]
  .map((id) => screenshotById(id))
  .filter((s): s is NonNullable<typeof s> => Boolean(s));

export default async function OptimizationPage({ params }: PageProps) {
  const t = await translatorFor(params);
  const locale = t.locale;
  return (
    <>
    {/* Structured data for this route — see lib/schema.ts. */}
    <RouteSchema
      locale={locale}
      path="/optimization"
      title={t("Glass cutting optimization and guillotine nesting")}
      description={t("How ProGlass nests glass parts on stock plates: safety edges, kerf, the guillotine cut sequence, verification and export to Bottero, Intermac, Lisec and DXF.")}
    />
      <PageHero
      t={t}
        eyebrow={t("Optimization")}
        title={
          <>
          {t("Turn stock plates into")}
        <span className="block text-brand-300">{t("a cutting plan the table can prove.")}</span>
          </>
        }
        lead={t("A guillotine nesting engine built for glass: your safety edges, your blade gap, your stock racks and your cutting list. Every plan is checked by two independent oracles before it can be exported or become production work.")}
        banner="nesting"
        breadcrumb={[
          { label: t("Home"), href: "/" },
          { label: t("Optimization"), href: "/optimization" },
        ]}
        meta={[
          { label: t("Algorithm"), value: t("Guillotine nesting") },
          { label: t("Workspace"), value: "8 tabs" },
          { label: t("Search families"), value: t("5 + Auto") },
          { label: t("Exports"), value: t("DXF · .OPT · .ISO · .DAT") },
        ]}
        actions={
          <>
            <ButtonLink href="/contact" size="lg" trailing={<ArrowRight />}>
          {t("Request a Demo")}
        </ButtonLink>
            <ButtonLink href="#workspace" variant="secondary" size="lg">
          {t("See the workspace")}
        </ButtonLink>
          </>
        }
      />

      {/* ── Why a guillotine model ───────────────────────────────── */}
      <Section aria-labelledby="model-heading">
        <Container>
          <div className="grid gap-12 lg:grid-cols-[minmax(0,1fr)_minmax(0,1.1fr)] lg:gap-16">
            <SectionHeading
              id="model-heading"
              eyebrow={t("The model")}
              title={t("A plan is only useful if the table can cut it.")}
              lead={t("Some optimizers place rectangles anywhere on the sheet. A glass table cannot work that way — it scores a line, breaks the sheet, and the piece comes free. So the engine models the cuts, not just the positions: it splits the remaining rectangle at each placement and records the split as a cut in the order the operator will make it.")}
            >
              <div className="mt-8 space-y-4">
                {[
                  {
                    title: t("The cut list and the free-rectangle ledger come from the same code"),
                    body: t("Because the cut sequence is written by the same routine that decides the remaining space, the two can never disagree. A plan that says it needs six cuts has six cuts that produce it."),
                  },
                  {
                    title: t("Margins are a property of thickness, not a global setting"),
                    body: t("Edge trims are resolved per glass thickness, so a 4 mm sheet and a 12 mm sheet in the same run are trimmed correctly rather than averaged."),
                  },
                  {
                    title: t("Rotation is adaptive"),
                    body: t("A piece keeps the orientation it was entered in whenever that fits; the engine only turns it 90° when it has to."),
                  },
                ].map((item) => (
                  <div key={item.title} className="surface p-5">
                    <p className="text-sm font-semibold text-text-primary">{t(item.title)}</p>
                    <p className="mt-1.5 text-sm leading-relaxed text-text-muted">{t(item.body)}</p>
                  </div>
                ))}
              </div>
            </SectionHeading>

            <Reveal delay={80}>
              <EnginePipeline />
            </Reveal>
          </div>
        </Container>
      </Section>

      {/* ── Demonstration ────────────────────────────────────────── */}
      <Section tone="raised" aria-labelledby="demo-heading">
        <Container>
          <SectionHeading
            id="demo-heading"
            eyebrow={t("Nesting, step by step")}
            title={t("Watch the plate get split, not just filled.")}
            lead={t("The animation below follows one worked example: a 3210 × 2250 plate of 6 mm glass, a 15 mm trim on all four edges, a 3 mm blade gap, and five pieces to come out of it.")}
          />
          <div className="mt-10">
            <NestingDemo />
          </div>
        </Container>
      </Section>

      {/* ── Thickness profiles ───────────────────────────────────── */}
      <Section aria-labelledby="profiles-heading">
        <Container>
          <SectionHeading
            id="profiles-heading"
            eyebrow={t("Cutting allowances")}
            title={t("The house margin profile, by thickness bucket.")}
            lead={t("These are the defaults the engineering assumes before an operator changes anything. Every value is editable per run, and 'Load house defaults' puts them back.")}
          />

          <div className="mt-10 overflow-x-auto">
            <table className="w-full min-w-[46rem] border-separate border-spacing-0 text-sm">
              <caption className="sr-only">{t("Default cutting allowances per glass thickness bucket")}</caption>
              <thead>
                <tr className="text-left">
                  {["Bucket", "Range", "Safety edges", "Kerf (blade gap)", "Minimum breakout"].map((heading) => (
                    <th
                      key={heading}
                      scope="col"
                      className="border-b border-line-strong/70 px-4 py-3 font-mono text-2xs uppercase tracking-[0.14em] text-text-faint font-medium"
                    >
                      {heading}
                    </th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {thicknessProfiles.map((row) => (
                  <tr key={row.bucket} className="transition-colors hover:bg-ink-900/50">
                    <th scope="row" className="border-b border-line/60 px-4 py-4 text-left font-semibold text-text-primary">
                      {row.bucket}
                    </th>
                    <td className="border-b border-line/60 px-4 py-4 font-mono text-text-secondary">{row.range}</td>
                    <td className="border-b border-line/60 px-4 py-4 text-text-muted">{row.edges}</td>
                    <td className="border-b border-line/60 px-4 py-4 font-mono text-text-secondary">{row.kerf}</td>
                    <td className="border-b border-line/60 px-4 py-4 text-text-muted">{row.breakout}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          <p className="mt-5 max-w-3xl text-xs leading-relaxed text-text-faint font-medium">{t("The thick bucket is deliberately asymmetric: thick glass takes no trim on the top edge. The minimum breakout is the narrowest offcut worth keeping — anything thinner is reported as trim waste rather than counted as reusable stock.")}</p>
        </Container>
      </Section>

      {/* ── Strategy + patterns ──────────────────────────────────── */}
      <Section tone="sunken" aria-labelledby="strategy-heading">
        <Container>
          <SectionHeading
            id="strategy-heading"
            eyebrow={t("Search")}
            title={t("Two independent dials: how wide the search is, and how hard it pushes.")}
            lead={t("The strategy family decides which placement portfolios get tried. Effort decides how hard the engine searches inside that family — time budget, shuffles and consolidation sweeps. Changing one does not silently change the other.")}
          />

          <div className="mt-10">
            <StrategyPicker />
          </div>
        </Container>
      </Section>

      {/* ── Cutting complexity ───────────────────────────────────── */}
      <Section aria-labelledby="complexity-heading">
        <Container>
          <SectionHeading
            id="complexity-heading"
            eyebrow={t("Cutting complexity")}
            title={t("Match the plan to the table, not the other way round.")}
            lead={t("A two-stage guillotine is faster to run and easier to jig. A free guillotine packs tighter. The engine lets you choose how deep the cut tree may go, and refuses trim cuts on laminated stock outright.")}
          />

          <div className="mt-10 grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
            {cutComplexities.map((item, index) => (
              <Reveal key={`${item.name}-${index}`} delay={index * 50} className="surface flex flex-col p-5">
                <IndexChip className="h-7 w-7 text-[0.75rem]">{String(index + 1).padStart(2, "0")}</IndexChip>
                <p className="mt-4 text-sm font-semibold text-text-primary">{t(item.name)}</p>
                <p className="mt-2 text-sm leading-relaxed text-text-muted">{t(item.detail)}</p>
              </Reveal>
            ))}
          </div>

          <Reveal delay={120} className="mt-8">
            <div className="surface flex flex-col gap-3 border-state-warning/30 bg-state-warning/[0.04] p-5 sm:flex-row sm:items-start sm:gap-4">
              <Badge tone="warning" className="shrink-0">
          {t("Laminate rule")}
        </Badge>
              <p className="text-sm leading-relaxed text-text-muted">{t("Z and W trims are never cut into laminated stock. The plies are not hand-broken at the table, so a trim cut would leave an unseparated edge. Refusing is the safe direction: a refused trim costs a little yield, a trim cut into laminate costs the sheet.")}</p>
            </div>
          </Reveal>
        </Container>
      </Section>

      {/* ── Verification + results ───────────────────────────────── */}
      <Section tone="raised" aria-labelledby="verify-heading">
        <Container>
          <SectionHeading
            id="verify-heading"
            eyebrow={t("Verification")}
            title={t("A plan that has lost pieces must never reach the table.")}
            lead={t("Two oracles run over every result and check different things. A plan that fails either one is still displayed so it can be inspected — but it is never exported to a cut file and never fed to production work.")}
          />

          <div className="mt-10 grid gap-6 lg:grid-cols-2">
            {verificationOracles.map((oracle, index) => (
              <Reveal key={`${oracle.name}-${index}`} delay={index * 70} className="surface-raised p-6">
                <p className="eyebrow">{t(oracle.evidence)}</p>
                <h3 className="mt-3 text-base font-semibold text-text-primary">{t(oracle.name)} check</h3>
                <p className="mt-2 text-sm leading-relaxed text-text-muted">{oracle.checks}</p>
              </Reveal>
            ))}
          </div>

          <div className="mt-10 grid gap-6 lg:grid-cols-[minmax(0,1.05fr)_minmax(0,1fr)]">
            <Reveal>
              <div className="surface-raised p-6">
                <h3 className="text-base font-semibold text-text-primary">{t("What a run reports")}</h3>
                <dl className="mt-5 divide-y divide-line/60">
                  {optimizationResultFields.map((field) => (
                    <div key={field.key} className="flex flex-col gap-1 py-3 sm:flex-row sm:gap-4">
                      <dt className="w-44 shrink-0 text-sm font-medium text-text-secondary">{field.key}</dt>
                      <dd className="text-sm leading-relaxed text-text-muted">{t(field.detail)}</dd>
                    </div>
                  ))}
                </dl>
              </div>
            </Reveal>

            <Reveal delay={80}>
              <div className="surface-raised p-6">
                <h3 className="text-base font-semibold text-text-primary">{t("Four claims this engine does not make")}</h3>
                <ul className="mt-5 space-y-3.5">
                  {engineHonestyNotes.map((note) => (
                    <li key={note} className="flex gap-3 text-sm leading-relaxed text-text-muted">
                      <span aria-hidden="true" className="mt-2 h-1 w-1 shrink-0 rounded-full bg-brand-400/80" />
                      {note}
                    </li>
                  ))}
                </ul>
              </div>
            </Reveal>
          </div>
        </Container>
      </Section>

      {/* ── Workspace ────────────────────────────────────────────── */}
      <Section id="workspace" aria-labelledby="workspace-heading">
        <Container>
          <SectionHeading
            id="workspace-heading"
            eyebrow={t("Workspace")}
            title={t("Eight tabs, in the order a job flows through them.")}
            lead={t("Confirmed orders are pulled into a batch, stock racks are chosen, the plan is produced, inspected in 2D, summarised, and exported. The tab names below are the labels on the screen.")}
          />

          <Reveal delay={80} className="mt-10">
            <ProductSlider
              ariaLabel="Optimization workspace tabs"
              slides={tabShots.map((shot) => ({
                id: shot.id,
                label: shot.title.replace(/^Optimization — /, ""),
                caption: shot.evidence,
                content: <ScreenshotFrame
      t={t} shot={shot} />,
              }))}
            />
          </Reveal>

          <div className="mt-12 grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
            {optimizerTabs.map((tab, index) => (
              <Reveal key={tab.id} id={tab.id} delay={index * 45} className="surface p-5">
                <div className="flex items-center justify-between gap-3">
                  <p className="font-mono text-sm font-semibold text-brand-300">{t(tab.label)}</p>
                  <span className="font-mono text-2xs tabular-nums text-text-faint font-medium">
                    {String(index + 1).padStart(2, "0")}
                  </span>
                </div>
                <p className="mt-3 text-sm leading-relaxed text-text-muted">{t(tab.summary)}</p>
                <ul className="mt-4 space-y-2 border-t border-line/60 pt-4">
                  {tab.bullets.map((bullet) => (
                    <li key={bullet} className="flex gap-2.5 text-xs leading-relaxed text-text-faint font-medium">
                      <span aria-hidden="true" className="mt-1.5 h-1 w-1 shrink-0 rounded-full bg-line-strong" />
                      {bullet}
                    </li>
                  ))}
                </ul>
              </Reveal>
            ))}
          </div>
        </Container>
      </Section>

      {/* ── Export / report ──────────────────────────────────────── */}
      <Section tone="sunken" aria-labelledby="export-heading">
        <Container>
          <div className="grid gap-12 lg:grid-cols-[minmax(0,1fr)_minmax(0,1.1fr)] lg:gap-16">
            <div>
              <SectionHeading
                id="export-heading"
                eyebrow={t("Output")}
                title={t("From the plan to the paperwork, and to the machine.")}
                lead={t("A run produces printable report sections for the office and machine-readable files for the equipment's own software. Nothing here writes to a machine directly.")}
              />

              <div className="mt-8 grid gap-4 sm:grid-cols-2">
                {[
                  { title: t("1 · Industrial cut schedule"), detail: t("The sheet-by-sheet schedule with its cuts in execution order.") },
                  { title: t("2 · Nested sheets summary"), detail: t("Per-plate placement and utilization.") },
                  { title: t("3 · Merged specifications summary"), detail: t("The glass specifications the batch was built from.") },
                  { title: t("4 · Stock plates allocation"), detail: t("Which rack rows were consumed, and what is left.") },
                ].map((item) => (
                  <div key={item.title} className="surface p-5">
                    <p className="text-sm font-semibold text-text-primary">{t(item.title)}</p>
                    <p className="mt-1.5 text-xs leading-relaxed text-text-muted">{t(item.detail)}</p>
                  </div>
                ))}
              </div>

              <div className="mt-8">
                <p className="eyebrow mb-3">{t("Machine-readable exports")}</p>
                <ul className="flex flex-wrap gap-2">
                  {["Generic DXF", "Bottero .OPT", "Intermac .ISO", "Lisec .DAT", "Cut-list CSV", "Excel (.xlsx)"].map((item) => (
                    <li
                      key={item}
                      className="rounded-lg border border-line-strong/70 bg-ink-900/60 px-3 py-1.5 font-mono text-xs text-text-secondary"
                    >
                      {item}
                    </li>
                  ))}
                </ul>
              </div>
            </div>

            <Reveal delay={80}>
              <AppFrame
      t={t}
                title={t("Optimization — Report")}
                railActive="Optimization"
                note={
                  <>
                    <span className="font-mono text-2xs uppercase tracking-[0.16em] text-brand-300">{t("Interface map")}</span>{" "}
                    — the tab set and labels are the application&apos;s own (Views/Optimization/Tabs). The panel is a
                    working preview with sample data, not a screen capture.
                  </>
                }
              >
                <LiveSurface
                  moduleKey="Optimization"
                  name="Optimization report"
                  capabilities={["Sheets used", "Utilization", "Wastage"]}
                />
              </AppFrame>
            </Reveal>
          </div>
        </Container>
      </Section>

      {/* ── Technical detail ─────────────────────────────────────── */}
      <Section aria-labelledby="technical-heading">
        <Container>
          <div className="grid gap-10 lg:grid-cols-[minmax(0,0.85fr)_minmax(0,1.15fr)] lg:gap-14">
            <SectionHeading
              id="technical-heading"
              eyebrow={t("Technical detail")}
              title={t("For the people who will ask.")}
              lead={t("If your production manager wants to know how the plan is produced, these are the answers. Nothing below exposes anything proprietary — the engine's source is not published, and this section describes behaviour rather than implementation.")}
            />
            <Reveal delay={80}>
              <Accordion
                defaultOpen={0}
                items={[
                  {
                    question: t("How does the engine decide between two plans?"),
                    answer: t("It compares complete plans, not placements. Fewest unplaced pieces wins first, then fewest sheets, then utilization. The plate pattern you choose is applied only after those limits are equal, which is why no pattern can trade a sheet for its own preference."),
                  },
                  {
                    question: t("What happens on a very large cutting list?"),
                    answer: t("Search breadth is trimmed in tiers by part count, so the run terminates instead of exhausting memory. Every reduction is recorded on the result as a plain-language note — the operator can see that the plan was produced under a reduced search rather than discovering it later."),
                  },
                  {
                    question: t("Can I run it against a rack that is nearly empty?"),
                    answer: t("Yes. Each stock row can carry an available-sheet cap, and the packer will not take more than the rack holds. A rack with no cap is deliberately distinguished from a rack with nothing left: the first means 'track the usage, do not limit it', the second is removed from the pool."),
                  },
                  {
                    question: t("What is the optimality gap?"),
                    answer: t("The engine computes a deliberately weak lower bound on how many sheets the job needs using total area alone, then reports how many sheets the actual plan used above that bound. A gap of zero means no plan could use fewer plates. It is shown because 'how close was this?' deserves an answer rather than a boast."),
                  },
                  {
                    question: t("Does the engine reuse offcuts?"),
                    answer: t("Only if you ask it to. Offcut reuse moves panes from the emptiest plate into leftovers other plates already produced, and drops any plate that ends up empty — but it changes the plan, so it is off by default. When it runs, every pane is re-checked after it moves and the whole attempt is rolled back if the plan stops verifying."),
                  },
                ]}
              />
            </Reveal>
          </div>
        </Container>
      </Section>

      {/* ── CTA ──────────────────────────────────────────────────── */}
      <Section tone="raised" aria-label={t("Request a demo")}>
        <Container>
          <Reveal>
            <div className="plate flex flex-col gap-6 p-8 sm:flex-row sm:items-center sm:justify-between sm:p-10">
              <div>
                <h2 className="text-2xl leading-tight sm:text-3xl">{t("Send us a cutting list.")}</h2>
                <p className="mt-3 max-w-xl text-sm leading-relaxed text-text-muted">{t("A demonstration is most useful when it runs on your glass, your sheet sizes and your margins — not on a sample project. Bring a job you recognise.")}</p>
              </div>
              <div className="flex shrink-0 flex-wrap gap-3">
                <ButtonLink href="/contact" size="lg" trailing={<ArrowRight />}>
          {t("Request a Demo")}
        </ButtonLink>
                <ButtonLink href="/features#showcase-optimization" variant="secondary" size="lg">
          {t("Module detail")}
        </ButtonLink>
              </div>
            </div>
          </Reveal>
        </Container>
      </Section>
    </>
  );
}

function EnginePipeline() {
  const t = useT();
  return (
    <div className="plate sticky top-28 p-6">
      <p className="mono-label">{t("How a run is produced")}</p>
      <ol className="mt-5">
        {enginePipeline.map((stage, index) => (
          <li key={`${stage.step}-${index}`} className="relative flex gap-4 pb-5 last:pb-0">
            {index < enginePipeline.length - 1 ? (
              <span aria-hidden="true" className="absolute left-[0.6875rem] top-6 bottom-0 w-px bg-line-strong" />
            ) : null}
            <span
              aria-hidden="true"
              className="relative z-10 mt-0.5 grid h-6 w-6 shrink-0 place-items-center rounded-md border border-line-strong/80 bg-ink-900 font-mono text-[0.6875rem] text-brand-300"
            >
              {String(index + 1).padStart(2, "0")}
            </span>
            <div className="min-w-0">
              <p className="text-sm font-medium text-text-primary">{t(stage.step)}</p>
              <p className="mt-0.5 text-xs leading-relaxed text-text-muted">{t(stage.detail)}</p>
            </div>
          </li>
        ))}
      </ol>
    </div>
  );
}

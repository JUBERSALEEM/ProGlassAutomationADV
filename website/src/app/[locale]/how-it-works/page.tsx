import type { Metadata } from "next";
import { LocaleLink as Link } from "@/components/ui/LocaleLink";
import { Container, Section } from "@/components/ui/Container";
import { PageHero } from "@/components/layout/PageHero";
import { ArrowRight, ButtonLink } from "@/components/ui/Button";
import { Badge, IndexChip } from "@/components/ui/Badge";
import { Reveal } from "@/components/ui/Reveal";
import { SectionHeading } from "@/components/ui/SectionHeading";
import { ChainStrip, WorkflowRail } from "@/components/product/WorkflowRail";
import { AppFrame } from "@/components/product/AppFrame";
import { LiveSurface } from "@/components/product/LiveSurface";
import { beforeAfter, businessChain } from "@/data/workflows";
import { RouteSchema } from "@/components/seo/RouteSchema";
import { pageMetadata } from "@/lib/metadata";
import { translatorFor } from "@/i18n/server";

type PageProps = { params: Promise<{ locale: string }> };

export async function generateMetadata({ params }: PageProps): Promise<Metadata> {
  const t = await translatorFor(params);
  return pageMetadata({
  locale: t.locale,
  title: t("How ProGlass Automation works, inquiry to statement"),
  description: t("The ProGlass workflow stage by stage: inquiry, the 9-step proforma wizard, job order, production stages, multi-movement delivery, VAT invoices and statements."),
  path: "/how-it-works",
  keywords: ["glass business workflow software", "quotation to invoice software", "glass production workflow", "delivery note software", t("glass ERP workflow")],
  ogEyebrow: t("Working day"),
  });
}

/** Mirrors the assistant's real read permissions rather than inventing steps. */
const assistantExamples = [
  "Show stock for 6 mm clear",
  "What is left to deliver on this job order?",
  "Open the tax invoices list",
  "Review the last proforma",
  "Adjust two sheets used on this rack",
];

export default async function HowItWorksPage({ params }: PageProps) {
  const t = await translatorFor(params);
  const locale = t.locale;
  return (
    <>
    {/* Structured data for this route — see lib/schema.ts. */}
    <RouteSchema
      locale={locale}
      path="/how-it-works"
      title={t("How ProGlass Automation works, inquiry to statement")}
      description={t("The ProGlass workflow stage by stage: inquiry, the 9-step proforma wizard, job order, production stages, multi-movement delivery, VAT invoices and statements.")}
    />
      <PageHero
      t={t}
        eyebrow={t("How it works")}
        title={t("One job, eight stages, and nothing typed twice.")}
        lead={t("This is the whole application in sequence. Each stage below names the statuses it stores, what it carries into the next stage, and the guarantee that keeps the handoff honest — because a chain is only as good as the weakest conversion.")}
        banner="production"
        breadcrumb={[
          { label: t("Home"), href: "/" },
          { label: t("How it works"), href: "/how-it-works" },
        ]}
        meta={[
          { label: t("Stages"), value: "8 in the chain" },
          { label: t("Documents"), value: t("Snapshot-based") },
          { label: t("Pricing engine"), value: t("One, at Step 9") },
          { label: t("Money maths"), value: t("Never recomputed") },
        ]}
        actions={
          <>
            <ButtonLink href="/contact" size="lg" trailing={<ArrowRight />}>
          {t("Request a Demo")}
        </ButtonLink>
            <ButtonLink href="/features" variant="secondary" size="lg">
          {t("Browse features")}
        </ButtonLink>
          </>
        }
      >
        <ChainStrip
      t={t} />
      </PageHero>

      {/* ── The full chain ───────────────────────────────────────── */}
      <Section aria-labelledby="chain-heading">
        <Container>
          <SectionHeading
            id="chain-heading"
            eyebrow={t("The chain")}
            title={t("Inquiry → quote → confirm → produce → deliver → invoice → settle.")}
            lead={t("Every arrow is a conversion of the previous record with an audit entry behind it. Nothing downstream invents data that the upstream record did not carry.")}
          />

          <div className="mt-12 grid gap-10 lg:grid-cols-[minmax(0,1fr)_minmax(0,1.6fr)] lg:gap-14">
            <div className="lg:sticky lg:top-28 lg:self-start">
              <div className="surface p-5">
                <p className="eyebrow mb-4">{t("At a glance")}</p>
                <ol className="space-y-1.5">
                  {businessChain.map((stage) => (
                    <li key={stage.id}>
                      <a
                        href={`#stage-${stage.id}`}
                        className="group flex items-center gap-3 rounded-lg px-2.5 py-2 transition-colors duration-200 hover:bg-ink-850/70"
                      >
                        <span className="font-mono text-2xs tabular-nums text-text-faint font-medium">{stage.index}</span>
                        <span className="text-sm text-text-secondary transition-colors group-hover:text-brand-200">
                          {t(stage.name)}
                        </span>
                      </a>
                    </li>
                  ))}
                </ol>
              </div>

              <div className="surface mt-4 p-5">
                <p className="eyebrow mb-3">{t("What the chain guarantees")}</p>
                <ul className="space-y-2.5 text-xs leading-relaxed text-text-muted">
                  <li>Documents are stored as snapshots, so editing a customer later does not rewrite an old invoice.</li>
                  <li>Money is computed once, at proforma Step 9, and read back everywhere else.</li>
                  <li>{t("A status the build does not recognise passes through untouched rather than becoming something plausible.")}</li>
                </ul>
              </div>
            </div>

            <div>
              <WorkflowRail
      t={t} />
            </div>
          </div>
        </Container>
      </Section>

      {/* ── The same job, two ways ───────────────────────────────── */}
      <Section aria-labelledby="before-after-heading">
        <Container>
          <SectionHeading
            id="before-after-heading"
            eyebrow={t("Before and after")}
            title={t("The same job, worked two ways.")}
            lead={t("The left column is how a glass job is usually run on paper and in spreadsheets. The right column is what this application does for that same step. Both columns describe the step honestly — there is no time saved or percentage gained in this table, because nothing in the software measures either.")}
          />

          <div className="mt-12 space-y-4">
            {beforeAfter.map((row, index) => (
              <Reveal key={row.id} delay={Math.min(index, 5) * 45}>
                <article className="surface overflow-hidden">
                  <div className="grid gap-px bg-line/50 lg:grid-cols-[minmax(0,0.75fr)_minmax(0,1fr)_minmax(0,1fr)]">
                    <div className="bg-ink-900/60 px-5 py-5 sm:px-6">
                      <p className="mono-label">{String(index + 1).padStart(2, "0")}</p>
                      <h3 className="mt-2 text-base font-semibold text-text-primary">{t(row.step)}</h3>
                      <Link
                        href={row.href}
                        className="link-underline mt-3 inline-flex items-center gap-1.5 text-xs font-medium text-brand-200"
                      >
                        {row.anchor}
                        <ArrowRight />
                      </Link>
                    </div>

                    <div className="bg-ink-950/40 px-5 py-5 sm:px-6">
                      <p className="mono-label text-state-warning/80">{t("On paper and in spreadsheets")}</p>
                      <p className="mt-3 text-sm leading-relaxed text-text-muted">{row.before}</p>
                    </div>

                    <div className="bg-ink-900/60 px-5 py-5 sm:px-6">
                      <p className="mono-label text-state-success/80">{t("In ProGlass Automation")}</p>
                      <p className="mt-3 text-sm leading-relaxed text-text-secondary">{row.after}</p>
                    </div>
                  </div>
                </article>
              </Reveal>
            ))}
          </div>

          <p className="mt-6 max-w-4xl text-xs leading-relaxed text-text-faint font-medium">
            The left-hand descriptions are general practice, not a claim about any particular business, and no figures are
            quoted for it. The right-hand column is limited to behaviour that exists in the application.
          </p>

          <div className="surface mt-6 flex flex-col gap-4 p-6 sm:flex-row sm:items-center sm:justify-between">
            <p className="max-w-2xl text-sm leading-relaxed text-text-secondary">
              For the numbers rather than the description, the{" "}
              <Link href="/worked-example" className="link-underline text-brand-200">
                worked example
              </Link>{" "}
              follows one seeded job through all five documents — 22 quotation lines, a partial delivery and the invoice
              raised from it.
            </p>
            <ButtonLink href="/worked-example" variant="secondary" size="sm" trailing={<ArrowRight />}>
          {t("See the figures")}
        </ButtonLink>
          </div>
        </Container>
      </Section>

      {/* ── Where the assistant fits ─────────────────────────────── */}
      <Section tone="raised" aria-labelledby="assistant-heading">
        <Container>
          <div className="grid gap-12 lg:grid-cols-[minmax(0,1.05fr)_minmax(0,1fr)] lg:gap-16">
            <div>
              <SectionHeading
                id="assistant-heading"
                eyebrow={t("Alongside the chain")}
                title={t("You can also just ask it.")}
                lead={t("The assistant inside the application reads across the same records — stock, documents, activity, profitability, statements and analytics. It is deliberately read-heavy: there is exactly one write it can perform, and it is permission-gated, confirmed and audited.")}
              />

              <ul className="mt-8 space-y-2.5">
                {assistantExamples.map((example) => (
                  <li
                    key={example}
                    className="flex items-center gap-3 rounded-card border border-line/60 bg-ink-900/50 px-4 py-3"
                  >
                    <span aria-hidden="true" className="h-1.5 w-1.5 shrink-0 rounded-full bg-brand-400" />
                    <span className="text-sm text-text-secondary">&ldquo;{example}&rdquo;</span>
                  </li>
                ))}
              </ul>

              <p className="mt-5 text-xs leading-relaxed text-text-faint font-medium">
                The one write is adjusting a sheet-stock quantity, which requires the SheetStore.Edit permission and a
                confirmation, and is recorded in the activity log. Over-issue is refused.
              </p>
            </div>

            <Reveal delay={80}>
              <div className="surface-raised p-6">
                <div className="flex items-center justify-between gap-3">
                  <h3 className="text-base font-semibold text-text-primary">{t("What it can and cannot touch")}</h3>
                  <Badge tone="success">{t("Read-heavy by design")}</Badge>
                </div>
                <div className="mt-5 grid gap-4 sm:grid-cols-2">
                  <div>
                    <p className="mono-label mb-2.5 text-state-success/80">{t("Can read")}</p>
                    <ul className="space-y-2">
                      {[
                        "Inquiries, proformas, job orders",
                        "Delivery notes and tax invoices",
                        "Customers and projects",
                        "Sheet-store stock and history",
                        "Activity log and audit trail",
                        "Job profitability and statements",
                        "Sales analytics and reports",
                      ].map((item) => (
                        <li key={item} className="flex gap-2.5 text-xs leading-relaxed text-text-muted">
                          <span aria-hidden="true" className="mt-1.5 h-1 w-1 shrink-0 rounded-full bg-state-success/70" />
                          {item}
                        </li>
                      ))}
                    </ul>
                  </div>
                  <div>
                    <p className="mono-label mb-2.5 text-state-warning/80">{t("Cannot")}</p>
                    <ul className="space-y-2">
                      {[
                        "Write documents or change prices",
                        "Delete records",
                        "Start programs or reach outside sanctioned folders",
                        "Send anything to a provider you have not configured",
                      ].map((item) => (
                        <li key={item} className="flex gap-2.5 text-xs leading-relaxed text-text-muted">
                          <span aria-hidden="true" className="mt-1.5 h-1 w-1 shrink-0 rounded-full bg-state-warning/70" />
                          {item}
                        </li>
                      ))}
                    </ul>
                  </div>
                </div>
              </div>
            </Reveal>
          </div>
        </Container>
      </Section>

      {/* ── Day in the life ──────────────────────────────────────── */}
      <Section aria-labelledby="day-heading">
        <Container>
          <SectionHeading
            id="day-heading"
            eyebrow={t("A working day")}
            title={t("What the software looks like in use.")}
            lead={t("Four moments that between them cover most of what a glass business does with it. The screens named are the ones the navigation rail opens.")}
          />

          <div className="mt-10 grid gap-6 lg:grid-cols-2">
            {[
              {
                time: "Morning",
                title: t("A quotation comes in"),
                body: t("The inquiry is recorded with its scope of supply. Converting it copies the BOQ rows onto the proforma's spec grid, where Step 4 prices each build group against the sheet costs you actually buy at. Step 5 then prices every panel — at least 0.5 m² of work each, base rate plus the specification's surcharge once a lite reaches 4 m² — and QUICK or OPTIMIZE hands Step 6 its sheet quantity."),
                frame: "Proforma Invoice — Step 4 Pricing",
                moduleKey: "ProformaInvoice",
                rail: "Proforma Invoice",
              },
              {
                time: "Midday",
                title: t("The factory needs a cutting plan"),
                body: t("Confirmed orders are pulled into an optimization batch, stock racks are ticked, and the engine produces a guillotine plan with your margins. Both oracles pass, so it can be exported to the table."),
                frame: "Optimization — 2D View",
                moduleKey: "Optimization",
                rail: "Optimization",
              },
              {
                time: "Afternoon",
                title: t("A partial delivery leaves"),
                body: t("The delivery note records the movement, the balance drops, and what is still owed stays visible on the job. A second movement later opens against the same balance rather than starting a new one."),
                frame: "Delivery Note — Detail",
                moduleKey: "DeliveryNote",
                rail: "Delivery Note",
              },
              {
                time: "Month end",
                title: t("The numbers are already there"),
                body: t("Tax invoices are raised against the deliveries, the statement aggregates them, and the VAT Submission report gathers the period. Job profitability reads the margin that was written at quote time."),
                frame: "Reports — Overview",
                moduleKey: "SalesReport",
                rail: "Overview",
              },
            ].map((moment, index) => (
              <Reveal key={`${moment.title}-${index}`} delay={index * 70}>
                <div className="flex h-full flex-col gap-5">
                  <div className="flex items-start gap-4">
                    <IndexChip>{String(index + 1).padStart(2, "0")}</IndexChip>
                    <div>
                      <p className="mono-label">{moment.time}</p>
                      <h3 className="mt-1 text-lg font-semibold text-text-primary">{t(moment.title)}</h3>
                      <p className="mt-2 text-sm leading-relaxed text-text-muted">{t(moment.body)}</p>
                    </div>
                  </div>

                  <AppFrame
      t={t}
                    title={moment.frame}
                    railActive={moment.rail}
                    compact
                    note={t("Interactive preview — real chrome and real fields, sample data, not a screen capture.")}
                  >
                    <LiveSurface
                      moduleKey={moment.moduleKey}
                      name={moment.frame}
                      capabilities={[moment.title, moment.body]}
                    />
                  </AppFrame>
                </div>
              </Reveal>
            ))}
          </div>
        </Container>
      </Section>

      {/* ── CTA ──────────────────────────────────────────────────── */}
      <Section tone="raised" aria-label={t("Request a demo")}>
        <Container>
          <Reveal>
            <div className="plate flex flex-col gap-6 p-8 sm:flex-row sm:items-center sm:justify-between sm:p-10">
              <div>
                <h2 className="text-2xl leading-tight sm:text-3xl">{t("Walk the chain on your own job.")}</h2>
                <p className="mt-3 max-w-xl text-sm leading-relaxed text-text-muted">
                  We will take one real quotation from inquiry through to statement, including the cutting plan and the
                  margin it produced.
                </p>
              </div>
              <div className="flex shrink-0 flex-wrap gap-3">
                <ButtonLink href="/contact" size="lg" trailing={<ArrowRight />}>
          {t("Request a Demo")}
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
  );
}

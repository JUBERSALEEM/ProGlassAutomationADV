import type { Metadata } from "next";
import { LocaleLink as Link } from "@/components/ui/LocaleLink";
import { Container, Section } from "@/components/ui/Container";
import { PageHero } from "@/components/layout/PageHero";
import { ArrowRight, ButtonLink } from "@/components/ui/Button";
import { Reveal } from "@/components/ui/Reveal";
import { SectionHeading } from "@/components/ui/SectionHeading";
import { StatCounter } from "@/components/ui/Stat";
import { IndexChip } from "@/components/ui/Badge";
import { site } from "@/lib/site";
import { RouteSchema } from "@/components/seo/RouteSchema";
import { pageMetadata } from "@/lib/metadata";
import { translatorFor } from "@/i18n/server";

type PageProps = { params: Promise<{ locale: string }> };

export async function generateMetadata({ params }: PageProps): Promise<Metadata> {
  const t = await translatorFor(params);
  return pageMetadata({
  locale: t.locale,
  title: t("About ProGlass Automation and why it exists"),
  description: t("ProGlass Automation is desktop software for glass fabrication businesses: what it is, the problem it solves, how it is built and what it does not do."),
  path: "/about",
  keywords: [t("about ProGlass Automation"), "glass industry software", t("glass fabrication ERP"), "glass business software"],
  ogEyebrow: t("About"),
  });
}

export default async function AboutPage({ params }: PageProps) {
  const t = await translatorFor(params);
  const locale = t.locale;
  return (
    <>
    {/* Structured data for this route — see lib/schema.ts. */}
    <RouteSchema
      locale={locale}
      path="/about"
      title={t("About ProGlass Automation and why it exists")}
      description={t("ProGlass Automation is desktop software for glass fabrication businesses: what it is, the problem it solves, how it is built and what it does not do.")}
    />
      <PageHero
      t={t}
        eyebrow={t("About")}
        title={t("A glass business is not a general business.")}
        lead={t("Generic ERP software assumes widgets: identical units, a bill of materials, a production order. Glass is not that. It is sold by area, priced on a build-up, cut from sheets, delivered in part-loads and invoiced against whatever actually left the yard. ProGlass Automation exists because that mismatch is where the money quietly goes missing.")}
        banner="calculator"
        breadcrumb={[
          { label: t("Home"), href: "/" },
          { label: t("About"), href: "/about" },
        ]}
        meta={[
          { label: t("What it is"), value: t("Windows desktop software") },
          { label: t("Who it is for"), value: t("Glass fabrication businesses") },
          { label: t("Stage"), value: `Version ${site.version}` },
          { label: t("Built in"), value: `${site.developerLocation}` },
        ]}
        actions={
          <>
            <ButtonLink href="/developer" size="lg" trailing={<ArrowRight />}>
          {t("The developer")}
        </ButtonLink>
            <ButtonLink href="/contact" variant="secondary" size="lg">
          {t("Request a Demo")}
        </ButtonLink>
          </>
        }
      />

      {/* ── The problem ──────────────────────────────────────────── */}
      <Section aria-labelledby="problem-heading">
        <Container>
          <div className="grid gap-12 lg:grid-cols-[minmax(0,1fr)_minmax(0,1.15fr)] lg:gap-16">
            <SectionHeading
              id="problem-heading"
              eyebrow={t("The problem")}
              title={t("The same glass gets typed five times, and the fifth number never matches the first.")}
              lead={t("In a glass business that has outgrown its spreadsheets, the quotation lives in one file, the cutting plan in another, the stock in a third, and the delivery in an email. Every handover is a chance to lose a piece, a colour, a rate or a margin — and nobody can prove afterwards which version was right.")}
            />

            <Reveal delay={80}>
              <div className="flow">
                {[
                  {
                    title: t("The quote is not a document, it is a calculation"),
                    body: t("Price depends on the build-up, the pane sizes, the treatment, the air space, the spacer, the wastage and the sheet cost at that moment. Change the sheet price later and a spreadsheet silently re-costs last month's work."),
                  },
                  {
                    title: t("The cutting plan has to be cuttable"),
                    body: t("A layout that looks tidy on a screen is worthless if the table cannot reach the score line. Edge trims, the blade gap and the minimum offcut worth keeping are not decoration — they decide whether the plan is real."),
                  },
                  {
                    title: t("Deliveries do not arrive in one piece"),
                    body: t("A job goes out over three movements and the fourth is a replacement. Without one place that owns 'what is still owed', every conversation starts with somebody counting."),
                  },
                  {
                    title: t("A margin you cannot trust is not a margin"),
                    body: t("If the profit figure changes depending on when you opened the screen, it cannot be used to price the next job. It has to be written once and read forever."),
                  },
                ].map((item, index) => (
                  <div key={`${item.title}-${index}`} className="flex gap-4">
                    <IndexChip>{String(index + 1).padStart(2, "0")}</IndexChip>
                    <div className="surface flex-1 p-5">
                      <p className="text-sm font-semibold text-text-primary">{t(item.title)}</p>
                      <p className="mt-2 text-sm leading-relaxed text-text-muted">{t(item.body)}</p>
                    </div>
                  </div>
                ))}
              </div>
            </Reveal>
          </div>
        </Container>
      </Section>

      {/* ── The idea ─────────────────────────────────────────────── */}
      <Section tone="raised" aria-labelledby="idea-heading">
        <Container>
          <SectionHeading
            id="idea-heading"
            eyebrow={t("The idea")}
            title={t("Traceability, not more screens.")}
            lead={t("The product has exactly one organising principle: a document is a conversion of the document before it, and every number on it keeps the context it was calculated with. Everything else — the wizard, the engine, the delivery chain — follows from that.")}
            align="center"
          />

          <div className="mt-12 grid gap-6 lg:grid-cols-3">
            {[
              {
                title: t("Documents convert, they do not re-enter"),
                body: t("An inquiry becomes a quotation becomes a job order becomes a delivery becomes an invoice. Each step copies the previous record and keeps the audit trail, so the third version of a number never appears out of nowhere."),
              },
              {
                title: t("Money is computed once"),
                body: t("Proforma Step 9 is the single pricing engine. Profitability, statements and reports read the snapshot it writes. A stale price basis is flagged rather than quietly rescored."),
              },
              {
                title: t("The floor gets what the office promised"),
                body: t("Real grid geometry crosses from quotation to job order — sheet selections and air-spacer gaps included — so the panel that was priced is the panel that gets built."),
              },
            ].map((pillar, index) => (
              <Reveal key={`${pillar.title}-${index}`} delay={index * 70} className="surface-raised p-6">
                <p className="eyebrow mb-3">{String(index + 1).padStart(2, "0")}</p>
                <h3 className="text-lg font-semibold text-text-primary">{t(pillar.title)}</h3>
                <p className="mt-3 text-sm leading-relaxed text-text-muted">{t(pillar.body)}</p>
              </Reveal>
            ))}
          </div>
        </Container>
      </Section>

      {/* ── How it is built ──────────────────────────────────────── */}
      <Section aria-labelledby="built-heading">
        <Container>
          <div className="grid gap-12 lg:grid-cols-[minmax(0,1fr)_minmax(0,1fr)] lg:gap-16">
            <div>
              <SectionHeading
                id="built-heading"
                eyebrow={t("How it is built")}
                title={t("One solution, held to one standard.")}
                lead={t("The application is a single WPF solution: views and view-models split one folder per business area, one database context per bounded context, and a design system the entire interface is drawn from. Localisation is resource dictionaries, so a new language does not need a code change.")}
              />

              <dl className="mt-8 space-y-3">
                {[
                  ["Platform", site.framework],
                  ["Pattern", `${site.pattern} — views and view-models split one folder per business area`],
                  ["Language", site.language],
                  ["Data", `${site.database} · ${site.databaseCount} databases, one per bounded context, additive-only migrations`],
                  ["Authorisation", `${site.roleCount} assignable roles · ${site.permissionCount} live permission constants, enforced at the service boundary`],
                  ["Localisation", `${site.locales} languages, shipped as resource dictionaries`],
                  ["Measured", `Counts below taken from the application tree on ${site.statsMeasuredOn}`],
                ].map(([term, detail]) => (
                  <div key={term} className="flex flex-col gap-1 rounded-card border border-line/60 bg-ink-900/50 p-4 sm:flex-row sm:gap-4">
                    <dt className="w-32 shrink-0 font-mono text-2xs uppercase tracking-[0.14em] text-text-faint font-medium">
                      {term}
                    </dt>
                    <dd className="text-sm leading-relaxed text-text-secondary">{detail}</dd>
                  </div>
                ))}
              </dl>
            </div>

            <Reveal delay={80}>
              <div className="grid gap-4 sm:grid-cols-2">
                <StatCounter value={site.moduleCount} label="View areas" hint="Each with its own views and view-models." />
                <StatCounter value={site.databaseCount} label="Databases" hint="Bounded contexts kept separate on purpose." />
                <StatCounter value={site.testCount} label="Test methods" hint={`[Fact] and [Theory] methods; most target workflow invariants rather than getters.`} />
                <StatCounter value={site.locales} label="Languages" hint="Adding one needs no C# change." />
              </div>

              <div className="surface mt-4 p-6">
                <p className="eyebrow mb-3">{t("The discipline that matters most")}</p>
                <p className="text-sm leading-relaxed text-text-muted">
                  Shipped code contains no <span className="font-mono text-text-secondary">TODO</span>, no{" "}
                  <span className="font-mono text-text-secondary">FIXME</span>, no{" "}
                  <span className="font-mono text-text-secondary">{t("NotImplementedException")}</span> and no empty{" "}
                  <span className="font-mono text-text-secondary">catch</span> block. That is not maintained by
                  willpower — an automated test scans the tree and fails the build if one appears. Numbers you cannot
                  check are marketing; this one you can.
                </p>
              </div>
            </Reveal>
          </div>
        </Container>
      </Section>

      {/* ── Who it is for ────────────────────────────────────────── */}
      <Section tone="sunken" aria-labelledby="audience-heading">
        <Container>
          <SectionHeading
            id="audience-heading"
            eyebrow={t("Who it is for")}
            title={t("The business, not a department.")}
            lead={t("ProGlass is a single-company system: one business, its own customers and its own staff. It is not built for multi-tenant hosting, and it does not want to be.")}
          />

          <div className="mt-10 grid gap-5 sm:grid-cols-2 lg:grid-cols-3">
            {[
              {
                title: t("Glass fabrication and processing"),
                body: t("Cutting, edging, tempering, laminating and assembling — the business model the application was written around."),
              },
              {
                title: t("Glazing contractors and installers"),
                body: t("Where the quoting happens before the site visit and the delivery has to match what the client actually specified."),
              },
              {
                title: t("Aluminium and glass contracting"),
                body: t("Multi-section jobs where each section has its own build-up and cannot all be priced from the same panel."),
              },
              {
                title: t("Glass trading and distribution"),
                body: t("Where the sheet is the product and stock accuracy and reorder levels are the daily problem."),
              },
              {
                title: t("The estimation desk"),
                body: t("Someone who needs a defensible quotation out the same day, built from costs the business actually buys at."),
              },
              {
                title: t("The owner who prices the awkward jobs"),
                body: t("The one who wants the margin visible at the moment of quoting rather than at the end of the quarter."),
              },
            ].map((item, index) => (
              <Reveal key={`${item.title}-${index}`} delay={index * 50} className="surface p-5">
                <h3 className="text-sm font-semibold text-text-primary">{t(item.title)}</h3>
                <p className="mt-2 text-sm leading-relaxed text-text-muted">{t(item.body)}</p>
              </Reveal>
            ))}
          </div>
        </Container>
      </Section>

      {/* ── Vision ───────────────────────────────────────────────── */}
      <Section aria-labelledby="vision-heading">
        <Container>
          <div className="grid gap-12 lg:grid-cols-[minmax(0,1.1fr)_minmax(0,1fr)] lg:gap-16">
            <SectionHeading
              id="vision-heading"
              eyebrow={t("Direction")}
              title={t("Finish the factory floor, then stop adding surface area.")}
              lead={t("The commercial chain — inquiry through statement — is complete in the application. The production stages are the current wave of work: eight shop-floor workstations and a control centre sitting under the same job order, so the office and the floor finally read from one record. After that the direction is depth rather than breadth.")}
            >
              <ul className="mt-8 space-y-3">
                {[
                  {
                    title: t("Same record, both sides of the business"),
                    body: t("The commercial side and the factory side see the same job. Neither has to ask the other what happened."),
                  },
                  {
                    title: t("Offline first, and staying that way"),
                    body: t("Local data, offline licence activation, no account. That is a property of the product, not a limitation to be fixed later."),
                  },
                  {
                    title: t("Nothing claimed that cannot be demonstrated"),
                    body: t("A feature is described on this website when it runs, and the description matches what a customer will see on screen."),
                  },
                ].map((item) => (
                  <li key={item.title} className="flex gap-4 rounded-card border border-line/60 bg-ink-900/50 p-4">
                    <span aria-hidden="true" className="mt-2 h-1.5 w-1.5 shrink-0 rounded-full bg-brand-400" />
                    <div>
                      <p className="text-sm font-semibold text-text-secondary">{t(item.title)}</p>
                      <p className="mt-1 text-sm leading-relaxed text-text-muted">{t(item.body)}</p>
                    </div>
                  </li>
                ))}
              </ul>
            </SectionHeading>

            <Reveal delay={80} className="flow">
              <Link href="/developer" className="surface-raised group block p-6 transition-colors duration-300 hover:border-brand-400/40">
                <p className="eyebrow">{t("Next")}</p>
                <h3 className="mt-3 text-lg font-semibold text-text-primary">{t("The person building it")}</h3>
                <p className="mt-2 text-sm leading-relaxed text-text-muted">
                  ProGlass Automation is developed by {site.developerName}. The developer page covers the build, the
                  technology and the thinking behind it.
                </p>
                <span className="link-underline mt-4 text-sm">
                  Read the developer page <ArrowRight />
                </span>
              </Link>

              <div className="surface p-6">
                <p className="eyebrow mb-3">{t("A note on honesty")}</p>
                <p className="text-sm leading-relaxed text-text-muted">
                  This website contains no customer count, no award, no certification and no percentage improvement claim,
                  because the repository contains none of those things. Where a screen capture does not yet exist, the page
                  says so instead of showing an illustration labelled as a screenshot.
                </p>
              </div>
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
                <h2 className="text-2xl leading-tight sm:text-3xl">If that describes your business, see it working.</h2>
                <p className="mt-3 max-w-xl text-sm leading-relaxed text-text-muted">
                  A demonstration runs on a job you recognise, from the quotation through to the cutting plan.
                </p>
              </div>
              <div className="flex shrink-0 flex-wrap gap-3">
                <ButtonLink href="/contact" size="lg" trailing={<ArrowRight />}>
          {t("Request a Demo")}
        </ButtonLink>
                <ButtonLink href="/optimization" variant="secondary" size="lg">
          {t("The engine")}
        </ButtonLink>
              </div>
            </div>
          </Reveal>
        </Container>
      </Section>
    </>
  );
}

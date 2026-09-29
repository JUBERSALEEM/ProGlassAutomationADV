import type { Metadata } from "next";
import Link from "next/link";
import { Container, Section } from "@/components/ui/Container";
import { PageHero } from "@/components/layout/PageHero";
import { ArrowRight, ButtonLink } from "@/components/ui/Button";
import { Reveal } from "@/components/ui/Reveal";
import { SectionHeading } from "@/components/ui/SectionHeading";
import { StatCounter } from "@/components/ui/Stat";
import { IndexChip } from "@/components/ui/Badge";
import { site } from "@/lib/site";
import { pageMetadata } from "@/lib/metadata";

export const metadata: Metadata = pageMetadata({
  title: "About ProGlass Automation and why it exists",
  description:
    "ProGlass Automation is desktop software for glass fabrication businesses: what it is, the problem it solves, how it is built and what it does not do.",
  path: "/about",
  keywords: ["about ProGlass Automation", "glass industry software", "glass fabrication ERP", "glass business software"],
});

export default function AboutPage() {
  return (
    <>
      <PageHero
        eyebrow="About"
        title="A glass business is not a general business."
        lead="Generic ERP software assumes widgets: identical units, a bill of materials, a production order. Glass is not that. It is sold by area, priced on a build-up, cut from sheets, delivered in part-loads and invoiced against whatever actually left the yard. ProGlass Automation exists because that mismatch is where the money quietly goes missing."
        banner="calculator"
        breadcrumb={[
          { label: "Home", href: "/" },
          { label: "About", href: "/about" },
        ]}
        meta={[
          { label: "What it is", value: "Windows desktop software" },
          { label: "Who it is for", value: "Glass fabrication businesses" },
          { label: "Stage", value: `Version ${site.version}` },
          { label: "Built in", value: `${site.developerLocation}` },
        ]}
        actions={
          <>
            <ButtonLink href="/developer" size="lg" trailing={<ArrowRight />}>
              The developer
            </ButtonLink>
            <ButtonLink href="/contact" variant="secondary" size="lg">
              Request a Demo
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
              eyebrow="The problem"
              title="The same glass gets typed five times, and the fifth number never matches the first."
              lead="In a glass business that has outgrown its spreadsheets, the quotation lives in one file, the cutting plan in another, the stock in a third, and the delivery in an email. Every handover is a chance to lose a piece, a colour, a rate or a margin — and nobody can prove afterwards which version was right."
            />

            <Reveal delay={80}>
              <div className="flow">
                {[
                  {
                    title: "The quote is not a document, it is a calculation",
                    body: "Price depends on the build-up, the pane sizes, the treatment, the air space, the spacer, the wastage and the sheet cost at that moment. Change the sheet price later and a spreadsheet silently re-costs last month's work.",
                  },
                  {
                    title: "The cutting plan has to be cuttable",
                    body: "A layout that looks tidy on a screen is worthless if the table cannot reach the score line. Edge trims, the blade gap and the minimum offcut worth keeping are not decoration — they decide whether the plan is real.",
                  },
                  {
                    title: "Deliveries do not arrive in one piece",
                    body: "A job goes out over three movements and the fourth is a replacement. Without one place that owns 'what is still owed', every conversation starts with somebody counting.",
                  },
                  {
                    title: "A margin you cannot trust is not a margin",
                    body: "If the profit figure changes depending on when you opened the screen, it cannot be used to price the next job. It has to be written once and read forever.",
                  },
                ].map((item, index) => (
                  <div key={item.title} className="flex gap-4">
                    <IndexChip>{String(index + 1).padStart(2, "0")}</IndexChip>
                    <div className="surface flex-1 p-5">
                      <p className="text-sm font-semibold text-text-primary">{item.title}</p>
                      <p className="mt-2 text-sm leading-relaxed text-text-muted">{item.body}</p>
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
            eyebrow="The idea"
            title="Traceability, not more screens."
            lead="The product has exactly one organising principle: a document is a conversion of the document before it, and every number on it keeps the context it was calculated with. Everything else — the wizard, the engine, the delivery chain — follows from that."
            align="center"
          />

          <div className="mt-12 grid gap-6 lg:grid-cols-3">
            {[
              {
                title: "Documents convert, they do not re-enter",
                body: "An inquiry becomes a quotation becomes a job order becomes a delivery becomes an invoice. Each step copies the previous record and keeps the audit trail, so the third version of a number never appears out of nowhere.",
              },
              {
                title: "Money is computed once",
                body: "Proforma Step 9 is the single pricing engine. Profitability, statements and reports read the snapshot it writes. A stale price basis is flagged rather than quietly rescored.",
              },
              {
                title: "The floor gets what the office promised",
                body: "Real grid geometry crosses from quotation to job order — sheet selections and air-spacer gaps included — so the panel that was priced is the panel that gets built.",
              },
            ].map((pillar, index) => (
              <Reveal key={pillar.title} delay={index * 70} className="surface-raised p-6">
                <p className="eyebrow mb-3">{String(index + 1).padStart(2, "0")}</p>
                <h3 className="text-lg font-semibold text-text-primary">{pillar.title}</h3>
                <p className="mt-3 text-sm leading-relaxed text-text-muted">{pillar.body}</p>
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
                eyebrow="How it is built"
                title="One solution, held to one standard."
                lead="The application is a single WPF solution: views and view-models split one folder per business area, one database context per bounded context, and a design system the entire interface is drawn from. Localisation is resource dictionaries, so a new language does not need a code change."
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
                    <dt className="w-32 shrink-0 font-mono text-2xs uppercase tracking-[0.14em] text-text-faint">
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
                <p className="eyebrow mb-3">The discipline that matters most</p>
                <p className="text-sm leading-relaxed text-text-muted">
                  Shipped code contains no <span className="font-mono text-text-secondary">TODO</span>, no{" "}
                  <span className="font-mono text-text-secondary">FIXME</span>, no{" "}
                  <span className="font-mono text-text-secondary">NotImplementedException</span> and no empty{" "}
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
            eyebrow="Who it is for"
            title="The business, not a department."
            lead="ProGlass is a single-company system: one business, its own customers and its own staff. It is not built for multi-tenant hosting, and it does not want to be."
          />

          <div className="mt-10 grid gap-5 sm:grid-cols-2 lg:grid-cols-3">
            {[
              {
                title: "Glass fabrication and processing",
                body: "Cutting, edging, tempering, laminating and assembling — the business model the application was written around.",
              },
              {
                title: "Glazing contractors and installers",
                body: "Where the quoting happens before the site visit and the delivery has to match what the client actually specified.",
              },
              {
                title: "Aluminium and glass contracting",
                body: "Multi-section jobs where each section has its own build-up and cannot all be priced from the same panel.",
              },
              {
                title: "Glass trading and distribution",
                body: "Where the sheet is the product and stock accuracy and reorder levels are the daily problem.",
              },
              {
                title: "The estimation desk",
                body: "Someone who needs a defensible quotation out the same day, built from costs the business actually buys at.",
              },
              {
                title: "The owner who prices the awkward jobs",
                body: "The one who wants the margin visible at the moment of quoting rather than at the end of the quarter.",
              },
            ].map((item, index) => (
              <Reveal key={item.title} delay={index * 50} className="surface p-5">
                <h3 className="text-sm font-semibold text-text-primary">{item.title}</h3>
                <p className="mt-2 text-sm leading-relaxed text-text-muted">{item.body}</p>
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
              eyebrow="Direction"
              title="Finish the factory floor, then stop adding surface area."
              lead="The commercial chain — inquiry through statement — is complete in the application. The production stages are the current wave of work: eight shop-floor workstations and a control centre sitting under the same job order, so the office and the floor finally read from one record. After that the direction is depth rather than breadth."
            >
              <ul className="mt-8 space-y-3">
                {[
                  {
                    title: "Same record, both sides of the business",
                    body: "The commercial side and the factory side see the same job. Neither has to ask the other what happened.",
                  },
                  {
                    title: "Offline first, and staying that way",
                    body: "Local data, offline licence activation, no account. That is a property of the product, not a limitation to be fixed later.",
                  },
                  {
                    title: "Nothing claimed that cannot be demonstrated",
                    body: "A feature is described on this website when it runs, and the description matches what a customer will see on screen.",
                  },
                ].map((item) => (
                  <li key={item.title} className="flex gap-4 rounded-card border border-line/60 bg-ink-900/50 p-4">
                    <span aria-hidden="true" className="mt-2 h-1.5 w-1.5 shrink-0 rounded-full bg-brand-400" />
                    <div>
                      <p className="text-sm font-semibold text-text-secondary">{item.title}</p>
                      <p className="mt-1 text-sm leading-relaxed text-text-muted">{item.body}</p>
                    </div>
                  </li>
                ))}
              </ul>
            </SectionHeading>

            <Reveal delay={80} className="flow">
              <Link href="/developer" className="surface-raised group block p-6 transition-colors duration-300 hover:border-brand-400/40">
                <p className="eyebrow">Next</p>
                <h3 className="mt-3 text-lg font-semibold text-text-primary">The person building it</h3>
                <p className="mt-2 text-sm leading-relaxed text-text-muted">
                  ProGlass Automation is developed by {site.developerName}. The developer page covers the build, the
                  technology and the thinking behind it.
                </p>
                <span className="link-underline mt-4 text-sm">
                  Read the developer page <ArrowRight />
                </span>
              </Link>

              <div className="surface p-6">
                <p className="eyebrow mb-3">A note on honesty</p>
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
      <Section tone="raised" aria-label="Request a demo">
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
                  Request a Demo
                </ButtonLink>
                <ButtonLink href="/optimization" variant="secondary" size="lg">
                  The engine
                </ButtonLink>
              </div>
            </div>
          </Reveal>
        </Container>
      </Section>
    </>
  );
}

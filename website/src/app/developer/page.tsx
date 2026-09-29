import type { Metadata } from "next";
import { Container, Section } from "@/components/ui/Container";
import { PageHero } from "@/components/layout/PageHero";
import { ArrowRight, ButtonLink } from "@/components/ui/Button";
import { Badge, IndexChip } from "@/components/ui/Badge";
import { Reveal } from "@/components/ui/Reveal";
import { SectionHeading } from "@/components/ui/SectionHeading";
import { Accordion } from "@/components/ui/Accordion";
import { site } from "@/lib/site";
import { pageMetadata } from "@/lib/metadata";

export const metadata: Metadata = pageMetadata({
  title: "The developer behind ProGlass Automation",
  description:
    "Designed and developed by Juber Saleem: the technology behind ProGlass Automation, the engineering principles it is held to, and how to get in touch.",
  path: "/developer",
  keywords: ["ProGlass Automation developer", "Juber Saleem", "WPF glass software developer", "glass ERP developer"],
});

/**
 * Every fact on this page comes from the application itself — the Developer
 * section of the About screen (Views/About/Sections/DeveloperSection.xaml,
 * ViewModels/About/AboutViewModel.cs) and the csproj. There is no education,
 * no employer history, no years-of-experience claim and no certification,
 * because the repository does not contain any of those.
 */
const principles = [
  {
    index: "01",
    title: "Deterministic precision",
    body: "Every formula produces the same result every time, with no hidden state and no race condition. If two screens show the same figure, it came from the same computation.",
  },
  {
    index: "02",
    title: "Modular domain isolation",
    body: "Each business domain has its own data boundary, which limits the blast radius when something goes wrong. The invoice database cannot corrupt the stock database.",
  },
  {
    index: "03",
    title: "Automated quality",
    body: "Continuous verification with zero tolerance for warnings. Tests target workflow invariants — company semantics, inquiry-to-quote conversion, price-basis determinism — and one of them scans the source tree for silent error handling.",
  },
];

const stack = [
  { group: "Platform", items: [".NET 8", "WPF"] },
  { group: "Database", items: ["SQLite", "EF Core 8"] },
  { group: "Pattern", items: ["MVVM", "Strict separation of views and view-models"] },
  { group: "Language", items: ["C# 12"] },
  { group: "Document output", items: ["QuestPDF", "ClosedXML", "CsvHelper"] },
  { group: "AI", items: ["Optional cloud provider", "On-device local model"] },
];

export default function DeveloperPage() {
  return (
    <>
      <PageHero
        eyebrow="The developer"
        title={
          <>
            Building software around
            <span className="block text-brand-300">real glass workflows.</span>
          </>
        }
        lead="ProGlass Automation is designed and written by one developer, against the way a glass business actually works rather than the way a generic ERP template assumes it does."
        breadcrumb={[
          { label: "Home", href: "/" },
          { label: "Developer", href: "/developer" },
        ]}
        meta={[
          { label: "Developer", value: site.developerName },
          { label: "Product", value: `${site.name} v${site.version}` },
          { label: "Built in", value: site.developerLocation },
          { label: "Contact", value: site.developerEmail },
        ]}
        actions={
          <>
            <ButtonLink href={`mailto:${site.developerEmail}`} size="lg" trailing={<ArrowRight />} external>
              Email the developer
            </ButtonLink>
            <ButtonLink href="/about" variant="secondary" size="lg">
              About the product
            </ButtonLink>
          </>
        }
      >
        <div className="plate flex flex-col gap-6 p-6 sm:flex-row sm:items-center sm:gap-8 sm:p-8">
          <span
            aria-hidden="true"
            className="grid h-20 w-20 shrink-0 place-items-center rounded-panel border border-line-strong/70 bg-gradient-to-br from-ink-800 to-ink-950 text-2xl font-bold text-brand-300 shadow-panel"
          >
            {site.developerName
              .split(" ")
              .map((part) => part.charAt(0))
              .join("")}
          </span>
          <div className="min-w-0">
            <p className="text-xl font-semibold text-text-primary sm:text-2xl">{site.developerName}</p>
            <p className="mt-1.5 text-sm text-text-muted">
              Principal developer, product design and workflow engineering for ProGlass Automation.
            </p>
            <p className="mt-2 font-mono text-2xs uppercase tracking-[0.16em] text-brand-300/80">
              — Made in {site.developerLocation}
            </p>
            <div className="mt-4 flex flex-wrap gap-2">
              <Badge tone="neutral">Sole developer</Badge>
              <Badge tone="neutral">{site.pattern} architecture</Badge>
              <Badge tone="neutral">Desktop only</Badge>
            </div>
          </div>
        </div>
      </PageHero>

      {/* ── Why it was built ─────────────────────────────────────── */}
      <Section aria-labelledby="why-heading">
        <Container>
          <div className="grid gap-12 lg:grid-cols-[minmax(0,1fr)_minmax(0,1.1fr)] lg:gap-16">
            <SectionHeading
              id="why-heading"
              eyebrow="Why it was built"
              title="Because the tools available assumed the wrong business."
              lead="Glass is priced on a build-up rather than a part number, sold by area rather than by unit, produced from sheets rather than from stock components, and delivered in movements rather than in full. Software written for manufacturing-in-general handles none of that without a battle, so the application was written around the specific case instead."
            />

            <Reveal delay={80}>
              <div className="space-y-4">
                {[
                  {
                    title: "The ceiling of a spreadsheet",
                    body: "A spreadsheet can hold a formula. It cannot hold a document chain, refuse an over-delivery, or prove that the margin on screen is the margin that was quoted. The gap between those two things is where the product starts.",
                  },
                  {
                    title: "Built from the plant, not from a template",
                    body: "Edge trims by glass thickness, the blade gap, the narrowest offcut worth keeping, spacer systems from 6 to 32 mm — these are the values the application works in, because those are the values the job runs on.",
                  },
                  {
                    title: "One developer, one standard",
                    body: "A single hand across the whole solution means the pricing engine, the cut engine and the delivery maths are held to the same discipline rather than drifting apart between teams.",
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

      {/* ── Principles ───────────────────────────────────────────── */}
      <Section tone="raised" aria-labelledby="principles-heading">
        <Container>
          <SectionHeading
            id="principles-heading"
            eyebrow="Engineering principles"
            title="Three rules the codebase is held to."
            lead="These are not aspirations written on a wall. Each one is enforced somewhere in the build, which is why they have stayed true at this size."
          />

          <div className="mt-10 grid gap-5 lg:grid-cols-3">
            {principles.map((principle, index) => (
              <Reveal key={principle.index} delay={index * 70} className="surface-raised p-6">
                <span className="inline-flex h-9 w-9 items-center justify-center rounded-[0.6rem] border border-line-strong/70 bg-ink-800/80 font-mono text-xs font-semibold text-brand-300">
                  {principle.index}
                </span>
                <h3 className="mt-4 text-lg font-semibold text-text-primary">{principle.title}</h3>
                <p className="mt-2 text-sm leading-relaxed text-text-muted">{principle.body}</p>
              </Reveal>
            ))}
          </div>
        </Container>
      </Section>

      {/* ── Stack ────────────────────────────────────────────────── */}
      <Section aria-labelledby="stack-heading">
        <Container>
          <div className="grid gap-12 lg:grid-cols-[minmax(0,0.9fr)_minmax(0,1.1fr)] lg:gap-16">
            <SectionHeading
              id="stack-heading"
              eyebrow="Technology"
              title="Boring, in the best sense."
              lead="The application is built on a Microsoft desktop stack and mature libraries, chosen so that a business can rely on it for years rather than rebuild it in three. There is nothing experimental in the production path."
            />

            <Reveal delay={80} className="grid gap-4 sm:grid-cols-2">
              {stack.map((group) => (
                <div key={group.group} className="surface p-5">
                  <p className="eyebrow mb-3">{group.group}</p>
                  <ul className="space-y-1.5">
                    {group.items.map((item) => (
                      <li key={item} className="font-mono text-sm text-text-secondary">
                        {item}
                      </li>
                    ))}
                  </ul>
                </div>
              ))}
            </Reveal>
          </div>
        </Container>
      </Section>

      {/* ── Working method ───────────────────────────────────────── */}
      <Section tone="sunken" aria-labelledby="method-heading">
        <Container>
          <SectionHeading
            id="method-heading"
            eyebrow="How the work is done"
            title="Written down first, then checked twice."
            lead="Every non-trivial change to the money path or the cut engine starts as a written analysis in the repository, is implemented against it, and is covered by tests before it ships."
          />

          <div className="mt-10 grid gap-5 sm:grid-cols-2 lg:grid-cols-4">
            {[
              {
                title: "Analysis before code",
                body: "Design documents, forensic audits and remediation plans live beside the source, so the reasoning behind a decision is still readable a year later.",
              },
              {
                title: "Tests as contracts",
                body: "The test suite pins behaviour rather than implementations: that conversion is atomic, that the price basis is deterministic, that a status vocabulary cannot silently change.",
              },
              {
                title: "Data safety by default",
                body: "Migrations back the database up before they run, wrap themselves in a transaction and roll back on failure. Restores take a pre-restore snapshot.",
              },
              {
                title: "Errors that explain themselves",
                body: "No silent catches, no raw exception text shown to a customer, and a correlation ID on every failure so a report can be traced to a log.",
              },
            ].map((item, index) => (
              <Reveal key={item.title} delay={index * 50} className="surface p-5">
                <p className="font-mono text-2xs uppercase tracking-[0.14em] text-text-faint">
                  {String(index + 1).padStart(2, "0")}
                </p>
                <h3 className="mt-3 text-sm font-semibold text-text-primary">{item.title}</h3>
                <p className="mt-2 text-sm leading-relaxed text-text-muted">{item.body}</p>
              </Reveal>
            ))}
          </div>
        </Container>
      </Section>

      {/* ── Working with the developer ───────────────────────────── */}
      <Section aria-labelledby="contact-heading">
        <Container>
          <div className="grid gap-12 lg:grid-cols-[minmax(0,1fr)_minmax(0,1.1fr)] lg:gap-16">
            <SectionHeading
              id="contact-heading"
              eyebrow="Working together"
              title="You talk to the person who writes it."
              lead="There is no support tier between a customer and the developer. A question about why the optimizer chose a particular plan, or a request to change how a charge row behaves, reaches the same inbox."
            />

            <Reveal delay={80}>
              <div className="surface-raised p-6 sm:p-8">
                <p className="eyebrow">Direct contact</p>
                <a
                  href={`mailto:${site.developerEmail}`}
                  className="mt-4 flex items-center gap-3 rounded-card border border-line-strong/70 bg-ink-950/60 px-4 py-3.5 transition-colors duration-200 hover:border-brand-400/50"
                >
                  <span
                    aria-hidden="true"
                    className="grid h-8 w-8 shrink-0 place-items-center rounded-lg border border-line-strong/70 text-brand-300"
                  >
                    <svg viewBox="0 0 20 20" className="h-4 w-4" fill="none" stroke="currentColor" strokeWidth="1.6">
                      <rect x="2.5" y="4.5" width="15" height="11" rx="2" />
                      <path d="m3 6 7 5 7-5" strokeLinecap="round" strokeLinejoin="round" />
                    </svg>
                  </span>
                  <span className="min-w-0">
                    <span className="block truncate font-mono text-sm text-text-primary">{site.developerEmail}</span>
                    <span className="block text-xs text-text-faint">Monitored directly</span>
                  </span>
                </a>

                <div className="mt-6 border-t border-line/60 pt-6">
                  <p className="eyebrow mb-3">Best reached about</p>
                  <ul className="space-y-2.5">
                    {[
                      "How a module would fit your workflow before you commit",
                      "Pricing and edition scope for the size of your business",
                      "A specific behaviour in the optimizer or the pricing engine",
                      "Support for an issue you are hitting in your own data",
                    ].map((item) => (
                      <li key={item} className="flex gap-2.5 text-sm leading-relaxed text-text-muted">
                        <span aria-hidden="true" className="mt-2 h-1 w-1 shrink-0 rounded-full bg-brand-400/80" />
                        {item}
                      </li>
                    ))}
                  </ul>
                </div>

                <div className="mt-6 flex flex-wrap gap-3 border-t border-line/60 pt-6">
                  <ButtonLink href="/contact" size="md" trailing={<ArrowRight />}>
                    Use the contact form
                  </ButtonLink>
                  <ButtonLink href="/developer#availability" variant="ghost" size="md">
                    Current status
                  </ButtonLink>
                </div>
              </div>
            </Reveal>
          </div>
        </Container>
      </Section>

      {/* ── Current status ───────────────────────────────────────── */}
      <Section tone="raised" id="availability" aria-labelledby="status-heading">
        <Container>
          <div className="grid gap-10 lg:grid-cols-[minmax(0,0.85fr)_minmax(0,1.15fr)] lg:gap-14">
            <SectionHeading
              id="status-heading"
              eyebrow="Current status"
              title="Where the build is."
              lead="The commercial chain is complete. The production stages are the current wave of work. This page is updated in the same commit as the product, so it cannot drift."
            />
            <Reveal delay={80}>
              <Accordion
                defaultOpen={0}
                items={[
                  {
                    question: "What is finished",
                    answer:
                      "The commercial chain: inquiries with a scope of supply, the nine-step proforma wizard with four build-type calculators and a Step 9 profit snapshot, job orders, sheet-store inventory with buy and use movements, specifications, company and system centres, multi-movement delivery notes with balance tracking, tax invoices with family sequencing, statements, job profitability, VAT submission and the report screens. Plus licensing, activation and the evaluation trial.",
                  },
                  {
                    question: "What is in progress",
                    answer:
                      "The production stage: eight shop-floor workstations — cutting, holes and cutouts, polish, edge deletion, mitring, tempering, pairing, and doubling / lamination — with a process work center and a production control centre over the top. This is the wave of work the current version is adding.",
                  },
                  {
                    question: "What is deliberately not built",
                    answer:
                      "There is no payment or receipt entity, so the invoice status is the end of the money chain rather than a cash ledger. Purchase orders exist as a reference field on the proforma, not as a module. Neither is hidden — both are stated so nobody buys the software expecting them.",
                  },
                  {
                    question: "How to follow the work",
                    answer:
                      "Ask for the version you are considering and you will get its documented scope. For anything ambiguous, email directly — you are talking to the developer, so the answer is a real one rather than a support script.",
                  },
                ]}
              />
            </Reveal>
          </div>
        </Container>
      </Section>

      {/* ── CTA ──────────────────────────────────────────────────── */}
      <Section aria-label="Contact the developer">
        <Container>
          <Reveal>
            <div className="plate flex flex-col gap-6 p-8 sm:flex-row sm:items-center sm:justify-between sm:p-10">
              <div>
                <h2 className="text-2xl leading-tight sm:text-3xl">Ask a direct question.</h2>
                <p className="mt-3 max-w-xl text-sm leading-relaxed text-text-muted">
                  If you run a glass business and want to know whether this fits how you work, describe the workflow and
                  you will get a straight answer — including a no.
                </p>
              </div>
              <div className="flex shrink-0 flex-wrap gap-3">
                <ButtonLink href={`mailto:${site.developerEmail}`} size="lg" trailing={<ArrowRight />} external>
                  Email {site.developerEmail.split("@")[0]}
                </ButtonLink>
                <ButtonLink href="/contact" variant="secondary" size="lg">
                  Contact form
                </ButtonLink>
              </div>
            </div>
          </Reveal>
        </Container>
      </Section>
    </>
  );
}

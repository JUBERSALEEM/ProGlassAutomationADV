import type { Metadata } from "next";
import Link from "next/link";
import { Container, Section } from "@/components/ui/Container";
import { PageHero } from "@/components/layout/PageHero";
import { ArrowRight, ButtonLink } from "@/components/ui/Button";
import { Badge, IndexChip } from "@/components/ui/Badge";
import { Reveal } from "@/components/ui/Reveal";
import { SectionHeading } from "@/components/ui/SectionHeading";
import { Accordion } from "@/components/ui/Accordion";
import { StatCounter, StatBadge } from "@/components/ui/Stat";
import { site } from "@/lib/site";
import { RouteSchema } from "@/components/seo/RouteSchema";
import { pageMetadata } from "@/lib/metadata";

export const metadata: Metadata = pageMetadata({
  title: "The developer — a glass estimator, not a coder",
  description:
    "ProGlass Automation is written by Juber Saleem, a glass estimator rather than a trained engineer. One person, no team, no investors.",
  path: "/developer",
  keywords: [
    "ProGlass Automation developer",
    "Juber Saleem",
    "glass estimator software",
    "self-taught WPF developer",
    "glass fabrication software author",
  ],
  ogEyebrow: "The developer",
});

/**
 * Everything on this page is either a fact about the application (the stack,
 * the counts, the file paths) or a statement the author has made about himself.
 * There is no education history, no employer, no years-of-experience figure and
 * no certification, because none of those are established anywhere. What is
 * established is the work: it is in the repository, and it can be checked.
 */

/** The estimator's day, before any of this existed. */
const before = [
  {
    index: "01",
    title: "The same glass, typed again",
    body: "A size written on an enquiry pad, then again on a quotation sheet, again on the cutting list, again on the delivery note, and once more when the invoice was made. Five chances to mistype a measurement, and no way to see which of the five was wrong.",
  },
  {
    index: "02",
    title: "Pricing from memory",
    body: "What the sheet cost, what the tempering cost, what the holes were charged at, what wastage to allow — held in a head, or in a notebook that only one person could read. If that person was on leave, the pricing waited.",
  },
  {
    index: "03",
    title: "Nothing could be checked later",
    body: "Months afterwards, when a customer queried an invoice, there was no record of why the price was what it was. The figures had been written on a sheet of paper, and the sheet of paper was gone.",
  },
];

/** How that background shows up in the software. Each one is a real decision. */
const consequences = [
  {
    index: "01",
    title: "Plain screens, no jargon",
    body: "Nothing in the application assumes the person using it has read a manual about ERP. Screens are named after the work — Sheet Store, Delivery Note, VAT Submission — not after a data model, because the author had to be able to use it himself.",
  },
  {
    index: "02",
    title: "The price basis is written down",
    body: "Every proforma carries a stamped basis: the rate, the wastage, the charges and the discount that produced the number. It exists because the hardest part of the old way was defending a price six months later.",
  },
  {
    index: "03",
    title: "Nothing lives in the cloud",
    body: "No account to create, no subscription dashboard, no internet connection required to open a job. The data sits in SQLite files on the machine in front of you, which is what a workshop actually has.",
  },
  {
    index: "04",
    title: "Everything prints",
    body: "A quotation, a cutting list, a shop-floor ticket, a delivery note, a statement — each one is a document meant to be handed over or pinned to a machine, not a screen someone stands and reads.",
  },
  {
    index: "05",
    title: "Built in the open about its limits",
    body: "Where a feature is unfinished or a gap is known, the site says so. That is not modesty: it is what one person can honestly promise, and it is the reason the sponsors page lists the gaps by name.",
  },
];

const principles = [
  {
    index: "01",
    title: "Deterministic precision",
    body: "The same inputs produce the same number every time, with no hidden state. If two screens show a figure, both came from the same computation.",
  },
  {
    index: "02",
    title: "Modular domain isolation",
    body: "Each business area keeps its own data boundary, which limits the damage when something goes wrong. The invoice database cannot corrupt the stock database.",
  },
  {
    index: "03",
    title: "Automated quality",
    body: "Tests target the invariants that matter — company semantics, inquiry-to-quote conversion, price-basis determinism — and one of them scans the source tree for silently swallowed errors.",
  },
];

const stack = [
  { group: "Platform", items: [".NET 8", "Windows Presentation Foundation"] },
  { group: "Data", items: ["SQLite", "Entity Framework Core 8"] },
  { group: "Pattern", items: ["MVVM", "Views separated from view-models"] },
  { group: "Language", items: ["C# 12"] },
  { group: "Documents", items: ["QuestPDF", "ClosedXML", "CsvHelper"] },
  { group: "Language files", items: ["15 locales as resource dictionaries"] },
];

const gaps = [
  { title: "A second pair of hands", body: "Someone who writes WPF and wants to work on a real product with real users." },
  { title: "A machine to develop on", body: "Work is done on borrowed and shared computers. A dedicated one changes how much gets finished in a week." },
  { title: "A cutting table to test against", body: "The optimiser writes Bottero .OPT, Intermac .ISO and Lisec .DAT. Those files deserve to be proved on a machine." },
  { title: "Translators who know glass", body: "Fifteen locales are already in the application; the trade vocabulary in each one can be better." },
];

const faq = [
  {
    question: "Was this written by a software company?",
    answer:
      "No. It was written by one person who estimates glass for a living. There is no agency, no studio and no team behind it, and nothing on this site should read as though there were.",
  },
  {
    question: "Is the author a trained programmer?",
    answer:
      "No. He is an estimator who taught himself what the work needed. That shows in the product: it is built around the workflow of a glass shop rather than around a software pattern, and it is deliberately plain to use.",
  },
  {
    question: "Does that make the software less reliable?",
    answer:
      "Judge it by what is in the repository rather than by the author's job title. The application is covered by a test suite, held to zero-warning builds, and every claim about it on this site names the file where the behaviour lives so it can be checked.",
  },
  {
    question: "Who do I speak to if something is wrong?",
    answer:
      `The person who wrote it. A question about why the optimiser chose a particular plan, or a request to change how a charge row behaves, reaches the same inbox: ${site.developerEmail}.`,
  },
];

export default function DeveloperPage() {
  return (
    <>
    {/* Structured data for this route — see lib/schema.ts. */}
    <RouteSchema
      path="/developer"
      title="The developer — an estimator who wrote his own software"
      description="ProGlass Automation is written by Juber Saleem, a glass estimator rather than a trained engineer. One person, no team, no investors — and how that shaped the product."
    />
      <PageHero
        eyebrow="The developer"
        banner="people"
        title={
          <>
            An estimator who got tired of
            <span className="text-gradient block">doing the same job five times.</span>
          </>
        }
        lead="ProGlass Automation was not written by a software company. It was written by one glass estimator, working alone, who found his own job harder than it needed to be and decided to make it simpler — for himself first, and then for anyone doing the same work."
        breadcrumb={[
          { label: "Home", href: "/" },
          { label: "Developer", href: "/developer" },
        ]}
        meta={[
          { label: "Written by", value: site.developerName },
          { label: "Background", value: "Glass estimating, not software engineering" },
          { label: "Team", value: "None — one person" },
          { label: "Contact", value: site.developerEmail },
        ]}
        actions={
          <>
            <ButtonLink href={`mailto:${site.developerEmail}`} size="lg" trailing={<ArrowRight />} external>
              Email the developer
            </ButtonLink>
            <ButtonLink href="/sponsors" variant="secondary" size="lg">
              What he still needs
            </ButtonLink>
          </>
        }
      >
        {/* ── Byline card ─────────────────────────────────────────────── */}
        <div className="surface-raised grid gap-6 p-6 lg:grid-cols-[minmax(0,1.15fr)_minmax(0,1fr)] lg:gap-10 lg:p-8">
          <div className="flex items-start gap-5">
            <span
              aria-hidden="true"
              className="grid h-16 w-16 shrink-0 place-items-center rounded-panel border border-brand-400/40 bg-gradient-to-br from-brand-500/25 to-ink-950 text-xl font-semibold text-brand-100 sm:h-20 sm:w-20 sm:text-2xl"
            >
              {site.developerName
                .split(" ")
                .map((part) => part.charAt(0))
                .join("")}
            </span>
            <div className="min-w-0">
              <p className="text-xl font-semibold text-text-primary sm:text-2xl">{site.developerName}</p>
              <p className="mt-1.5 text-sm leading-relaxed text-text-muted">
                Estimator first. Author of ProGlass Automation second — self-taught, working alone, still estimating.
              </p>
              <p className="mt-3 font-mono text-2xs uppercase tracking-[0.16em] text-brand-300">
                — Made in {site.developerLocation}
              </p>
            </div>
          </div>

          <dl className="grid grid-cols-2 gap-3 self-center sm:grid-cols-4 lg:grid-cols-2">
            {[
              { label: "People on the project", value: "One", hint: "No team, no agency, no contractors." },
              { label: "Own development PC", value: "None yet", hint: "Built on borrowed and shared machines." },
              { label: "Funding", value: "None", hint: "No investors, and no sponsors so far." },
              { label: "Formal training", value: "None in software", hint: "An estimator's background instead." },
            ].map((item) => (
              <div key={item.label} className="stat-tile p-3.5">
                <dt className="font-mono text-[0.5625rem] uppercase tracking-[0.14em] text-text-faint font-medium">
                  {item.label}
                </dt>
                <dd className="mt-1 text-base font-semibold text-text-primary">{item.value}</dd>
                <dd className="mt-1 text-[0.6875rem] leading-relaxed text-text-faint">{item.hint}</dd>
              </div>
            ))}
          </dl>
        </div>
      </PageHero>

      {/* ── How it started ───────────────────────────────────────────── */}
      <Section aria-labelledby="start-heading">
        <Container>
          <div className="grid gap-10 lg:grid-cols-[minmax(0,0.95fr)_minmax(0,1.35fr)] lg:gap-16">
            <div>
              <SectionHeading
                id="start-heading"
                eyebrow="How it started"
                title="The job was repetitive, and the repetition was the problem."
                lead="Estimating glass means holding a lot of small numbers in your head at once: a size, a thickness, a build-up, a rate, a wastage allowance, a delivery charge. None of them are difficult on their own. All of them together, every day, on paper, is where mistakes come from."
              />
              <p className="flow measure mt-6 text-sm leading-relaxed text-text-muted">
                The software began as a way to stop retyping. It was not planned as a product, and there was no
                intention to sell it. It was one estimator removing the parts of his own day he kept getting wrong —
                and then discovering that the same day is happening in glass shops everywhere.
              </p>
            </div>

            <ol className="flow">
              {before.map((item, index) => (
                <Reveal key={`${item.title}-${index}`} delay={index * 60}>
                  <li className="card-live flex gap-4 p-5">
                    <IndexChip>{item.index}</IndexChip>
                    <div className="min-w-0">
                      <h3 className="text-base font-semibold text-text-primary">{item.title}</h3>
                      <p className="mt-2 text-sm leading-relaxed text-text-muted">{item.body}</p>
                    </div>
                  </li>
                </Reveal>
              ))}
            </ol>
          </div>
        </Container>
      </Section>

      {/* ── What that background produced ────────────────────────────── */}
      <Section tone="raised" aria-labelledby="consequences-heading">
        <Container>
          <SectionHeading
            id="consequences-heading"
            eyebrow="What that background produced"
            title="Being a user first is the reason the screens look like this."
            lead="A trained engineer would have started from the data model. This started from the working day, and it shows in decisions that are unusual for business software — and deliberate."
          />

          <div className="mt-10 grid gap-4 md:grid-cols-2 xl:grid-cols-5">
            {consequences.map((item, index) => (
              <Reveal key={`${item.title}-${index}`} delay={index * 50}>
                <article className="card-live card-interactive flex h-full flex-col p-5">
                  <span className="font-mono text-2xs text-brand-300">{item.index}</span>
                  <h3 className="mt-2 text-base font-semibold text-text-primary">{item.title}</h3>
                  <p className="mt-2.5 text-sm leading-relaxed text-text-muted">{item.body}</p>
                </article>
              </Reveal>
            ))}
          </div>
        </Container>
      </Section>

      {/* ── Principles ───────────────────────────────────────────────── */}
      <Section aria-labelledby="principles-heading">
        <Container>
          <SectionHeading
            id="principles-heading"
            eyebrow="How the work is done"
            title="Three rules the codebase is held to."
            lead="These are not aspirations written on a wall. Each is enforced somewhere in the build, which is why they have survived at this size — and why a solo author can still answer for the result."
          />

          <div className="mt-10 grid gap-4 lg:grid-cols-3">
            {principles.map((item, index) => (
              <Reveal key={`${item.title}-${index}`} delay={index * 60}>
                <article className="surface h-full p-6">
                  <div className="flex items-center gap-3">
                    <IndexChip>{item.index}</IndexChip>
                    <h3 className="text-base font-semibold text-text-primary">{item.title}</h3>
                  </div>
                  <p className="mt-4 text-sm leading-relaxed text-text-muted">{item.body}</p>
                </article>
              </Reveal>
            ))}
          </div>

          <div className="mt-6 grid gap-4 sm:grid-cols-3">
            <StatCounter
              value={site.testCount}
              label="Test methods"
              hint={`Across ${site.testFiles} test files, plus ${site.testInlineCases.toLocaleString("en-GB")} [InlineData] cases.`}
            />
            <StatBadge value={site.pattern} label="Architecture" hint="Views never contain business logic." />
            <StatBadge
              value={`${site.productionFiles.toLocaleString("en-GB")} files`}
              label="Production source"
              hint={`${site.productionLines.toLocaleString("en-GB")} lines outside obj/ and bin/, counted against the application tree.`}
            />
          </div>
        </Container>
      </Section>

      {/* ── Stack ────────────────────────────────────────────────────── */}
      <Section tone="sunken" aria-labelledby="stack-heading">
        <Container>
          <div className="grid gap-10 lg:grid-cols-[minmax(0,1fr)_minmax(0,1.3fr)] lg:gap-16">
            <SectionHeading
              id="stack-heading"
              eyebrow="Technology"
              title="Boring, in the best sense."
              lead="A Microsoft desktop stack and mature libraries, chosen so a business can rely on it for years rather than rebuild it in three. Nothing experimental is in the production path — which matters more, not less, when one person is maintaining it."
            />

            <dl className="grid gap-3 sm:grid-cols-2">
              {stack.map((row) => (
                <div key={row.group} className="surface p-4">
                  <dt className="mono-label">{row.group}</dt>
                  <dd className="mt-2 flex flex-wrap gap-1.5">
                    {row.items.map((item) => (
                      <span key={item} className="chip">
                        {item}
                      </span>
                    ))}
                  </dd>
                </div>
              ))}
            </dl>
          </div>
        </Container>
      </Section>

      {/* ── What is still missing ────────────────────────────────────── */}
      <Section aria-labelledby="gaps-heading">
        <Container>
          <div className="grid gap-10 lg:grid-cols-[minmax(0,1.2fr)_minmax(0,1fr)] lg:items-center lg:gap-16">
            <div>
              <SectionHeading
                id="gaps-heading"
                eyebrow="Honestly, what is missing"
                title="One person is not a team, and this reads better than pretending otherwise."
                lead="There is no office, no second developer, no designer, no translator and no one on support. The build is as far along as one estimator working evenings could take it. The next stage needs help, and the gaps are listed by name rather than hinted at."
              />
              <div className="mt-8 flex flex-wrap gap-3">
                <ButtonLink href="/sponsors" size="lg" trailing={<ArrowRight />}>
                  See the eight gaps
                </ButtonLink>
                <ButtonLink href="/contact" variant="secondary" size="lg">
                  Offer a hand
                </ButtonLink>
              </div>
            </div>

            <ul className="grid gap-3 sm:grid-cols-2">
              {gaps.map((gap) => (
                <li key={gap.title} className="card-accent p-4">
                  <h3 className="text-sm font-semibold text-text-primary">{gap.title}</h3>
                  <p className="mt-2 text-xs leading-relaxed text-text-secondary">{gap.body}</p>
                </li>
              ))}
            </ul>
          </div>
        </Container>
      </Section>

      {/* ── FAQ ──────────────────────────────────────────────────────── */}
      <Section tone="raised" aria-labelledby="devfaq-heading">
        <Container>
          <SectionHeading
            id="devfaq-heading"
            eyebrow="Reasonable questions"
            title="The four things people ask about a one-person product."
          />
          <Reveal className="mt-8">
            <Accordion items={faq} />
          </Reveal>
        </Container>
      </Section>

      {/* ── Closing ──────────────────────────────────────────────────── */}
      <Section aria-labelledby="dev-cta-heading">
        <Container>
          <div className="card-accent grid gap-8 p-8 lg:grid-cols-[minmax(0,1.4fr)_minmax(0,1fr)] lg:items-center lg:p-10">
            <div>
              <h2 id="dev-cta-heading" className="text-2xl leading-tight sm:text-3xl">
                You talk to the person who writes it.
              </h2>
              <p className="measure mt-4 text-sm leading-relaxed text-text-secondary">
                There is no support tier between a customer and the author. Describe the job you do and where it goes
                wrong, and the answer comes back from the person who can change the software — usually with a question
                about how your shop actually works.
              </p>
              <div className="mt-7 flex flex-wrap gap-3">
                <ButtonLink href="/contact" size="lg" trailing={<ArrowRight />}>
                  Request a Demo
                </ButtonLink>
                <ButtonLink href="/about" variant="secondary" size="lg">
                  About the product
                </ButtonLink>
              </div>
            </div>

            <div className="space-y-3">
              <div className="flex flex-wrap gap-2">
                <Badge tone="neutral">Sole author</Badge>
                <Badge tone="neutral">Self-taught</Badge>
                <Badge tone="neutral">Desktop only</Badge>
                <Badge tone="neutral">Version {site.version}</Badge>
              </div>
              <p className="text-xs leading-relaxed text-text-faint font-medium">
                Every claim about the software on this site names the file it comes from. If something here cannot be
                checked, it should not be here — say so and it will be corrected.{" "}
                <Link href="/sponsors" className="link-underline">
                  Sponsorship and contribution
                </Link>
              </p>
            </div>
          </div>
        </Container>
      </Section>
    </>
  );
}

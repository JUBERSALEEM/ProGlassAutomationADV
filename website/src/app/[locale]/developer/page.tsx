import type { Metadata } from "next";
import { LocaleLink as Link } from "@/components/ui/LocaleLink";
import { Container, Section } from "@/components/ui/Container";
import { PageHero } from "@/components/layout/PageHero";
import { ArrowRight, ButtonLink } from "@/components/ui/Button";
import { Badge, IndexChip } from "@/components/ui/Badge";
import { Reveal } from "@/components/ui/Reveal";
import { SectionHeading } from "@/components/ui/SectionHeading";
import { Accordion } from "@/components/ui/Accordion";
import { history, workingDocuments, workingMethod } from "@/data/developerHistory";
import { StatCounter, StatBadge } from "@/components/ui/Stat";
import { site } from "@/lib/site";
import { RouteSchema } from "@/components/seo/RouteSchema";
import { pageMetadata } from "@/lib/metadata";
import { translatorFor } from "@/i18n/server";

type PageProps = { params: Promise<{ locale: string }> };

export async function generateMetadata({ params }: PageProps): Promise<Metadata> {
  const t = await translatorFor(params);
  return pageMetadata({
  locale: t.locale,
  title: t("The developer — one glass estimator, working alone"),
  description: t("ProGlass Automation is written by Juber Saleem, a glass estimator rather than a trained engineer. Its audits, fixes and open work are dated and public."),
  path: "/developer",
  keywords: [t("ProGlass Automation developer"), t("Juber Saleem"), "glass estimator software", t("self-taught WPF developer"), "glass fabrication software author"],
  ogEyebrow: t("The developer"),
  });
}

/**
 * Everything on this page is either a fact about the application (the stack,
 * the counts, the file paths) or a statement the author has made about himself.
 * There is no education history, no employer, no years-of-experience figure and
 * no certification, because none of those are established anywhere. What is
 * established is the work: it is in the repository, and it can be checked.
 */

/** The estimator's day, before any of this existed. */

/** How that background shows up in the software. Each one is a real decision. */


const stack = [
  { group: "Platform", items: [".NET 8", "Windows Presentation Foundation"] },
  { group: "Data", items: ["SQLite", "Entity Framework Core 8"] },
  { group: "Pattern", items: ["MVVM", "Views separated from view-models"] },
  { group: "Language", items: ["C# 12"] },
  { group: "Documents", items: ["QuestPDF", "ClosedXML", "CsvHelper"] },
  { group: "Language files", items: ["15 locales as resource dictionaries"] },
];



export default async function DeveloperPage({ params }: PageProps) {
  const t = await translatorFor(params);

const before = [
  {
    index: "01",
    title: t("The same glass, typed again"),
    body: t("A size written on an enquiry pad, then again on a quotation sheet, again on the cutting list, again on the delivery note, and once more when the invoice was made. Five chances to mistype a measurement, and no way to see which of the five was wrong."),
  },
  {
    index: "02",
    title: t("Pricing from memory"),
    body: t("What the sheet cost, what the tempering cost, what the holes were charged at, what wastage to allow — held in a head, or in a notebook that only one person could read. If that person was on leave, the pricing waited."),
  },
  {
    index: "03",
    title: t("Nothing could be checked later"),
    body: t("Months afterwards, when a customer queried an invoice, there was no record of why the price was what it was. The figures had been written on a sheet of paper, and the sheet of paper was gone."),
  },
];
const consequences = [
  {
    index: "01",
    title: t("Plain screens, no jargon"),
    body: t("Nothing in the application assumes the person using it has read a manual about ERP. Screens are named after the work — Sheet Store, Delivery Note, VAT Submission — not after a data model, because the author had to be able to use it himself."),
  },
  {
    index: "02",
    title: t("The price basis is written down"),
    body: t("Every proforma carries a stamped basis: the rate, the wastage, the charges and the discount that produced the number. It exists because the hardest part of the old way was defending a price six months later."),
  },
  {
    index: "03",
    title: t("Nothing lives in the cloud"),
    body: t("No account to create, no subscription dashboard, no internet connection required to open a job. The data sits in SQLite files on the machine in front of you, which is what a workshop actually has."),
  },
  {
    index: "04",
    title: t("Everything prints"),
    body: t("A quotation, a cutting list, a shop-floor ticket, a delivery note, a statement — each one is a document meant to be handed over or pinned to a machine, not a screen someone stands and reads."),
  },
  {
    index: "05",
    title: t("Built in the open about its limits"),
    body: t("Where a feature is unfinished or a gap is known, the site says so. That is not modesty: it is what one person can honestly promise, and it is the reason the sponsors page lists the gaps by name."),
  },
];
const principles = [
  {
    index: "01",
    title: t("Deterministic precision"),
    body: t("The same inputs produce the same number every time, with no hidden state. If two screens show a figure, both came from the same computation."),
  },
  {
    index: "02",
    title: t("Modular domain isolation"),
    body: t("Each business area keeps its own data boundary, which limits the damage when something goes wrong. The invoice database cannot corrupt the stock database."),
  },
  {
    index: "03",
    title: t("Automated quality"),
    body: t("Tests target the invariants that matter — company semantics, inquiry-to-quote conversion, price-basis determinism — and one of them scans the source tree for silently swallowed errors."),
  },
];
const gaps = [
  { title: t("A second pair of hands"), body: t("Someone who writes WPF and wants to work on a real product with real users.") },
  { title: t("A machine to develop on"), body: t("Work is done on borrowed and shared computers. A dedicated one changes how much gets finished in a week.") },
  { title: t("A cutting table to test against"), body: t("The optimiser writes Bottero .OPT, Intermac .ISO and Lisec .DAT. Those files deserve to be proved on a machine.") },
  { title: t("Translators who know glass"), body: t("Fifteen locales are already in the application; the trade vocabulary in each one can be better.") },
];
const faq = [
  {
    question: t("Was this written by a software company?"),
    answer: t("No. It was written by one person who estimates glass for a living. There is no agency, no studio and no team behind it, and nothing on this site should read as though there were."),
  },
  {
    question: t("Is the author a trained programmer?"),
    answer: t("No. He is an estimator who taught himself what the work needed. That shows in the product: it is built around the workflow of a glass shop rather than around a software pattern, and it is deliberately plain to use."),
  },
  {
    question: t("Does that make the software less reliable?"),
    answer: t("Judge it by what is in the repository rather than by the author's job title. The application is covered by a test suite, held to zero-warning builds, and every claim about it on this site names the file where the behaviour lives so it can be checked."),
  },
  {
    question: t("Who do I speak to if something is wrong?"),
    answer:
      `The person who wrote it. A question about why the optimiser chose a particular plan, or a request to change how a charge row behaves, reaches the same inbox: ${site.developerEmail}.`,
  },
];
  const locale = t.locale;
  return (
    <>
    {/* Structured data for this route — see lib/schema.ts. */}
    <RouteSchema
      locale={locale}
      path="/developer"
      title={t("The developer — an estimator who wrote his own software")}
      description={t("ProGlass Automation is written by Juber Saleem, a glass estimator rather than a trained engineer. One person, no team, no investors — and how that shaped the product.")}
    />
      <PageHero
        eyebrow={t("The developer")}
        banner="people"
        title={
          <>
            One glass estimator,
            <span className="text-gradient block">working alone, writing his own tools.</span>
          </>
        }
        lead={t("ProGlass Automation has no company behind it. It was written by Juber Saleem, who estimates glass for a living and started building the software he needed because the paper version of the job kept costing him time. He is still estimating. How the application got to version 2.0.11 is written down, dated, and open to reading — and that record is a better description of its author than any biography.")}
        breadcrumb={[
          { label: t("Home"), href: "/" },
          { label: t("Developer"), href: "/developer" },
        ]}
        meta={[
          { label: t("Written by"), value: site.developerName },
          { label: t("Background"), value: t("Glass estimating, not software engineering") },
          { label: t("Team"), value: t("None — one person") },
          { label: t("Contact"), value: site.developerEmail },
        ]}
        actions={
          <>
            <ButtonLink href={`mailto:${site.developerEmail}`} size="lg" trailing={<ArrowRight />} external>
          {t("Email the developer")}
        </ButtonLink>
            <ButtonLink href="/sponsors" variant="secondary" size="lg">
          {t("What he still needs")}
        </ButtonLink>
          </>
        }
      >
        {/*
          The profile. This is the first thing on the page because the question
          a visitor arrives with is "who wrote this?", and the answer is a
          person rather than a company. Everything asserted here is either a
          fact about the application (the stack, the counts, the file paths) or
          a statement the author has made about himself; there is no school, no
          employer and no years-of-experience figure, because none of those are
          established anywhere.
        */}
        <div className="surface-raised grid gap-8 p-6 lg:grid-cols-[minmax(0,1.25fr)_minmax(0,1fr)] lg:gap-12 lg:p-9">
          <div>
            <div className="flex items-start gap-5">
              <span
                aria-hidden="true"
                className="grid h-16 w-16 shrink-0 place-items-center rounded-panel border-2 border-brand-400/50 bg-gradient-to-br from-brand-500/30 to-ink-950 text-xl font-semibold text-brand-100 sm:h-20 sm:w-20 sm:text-2xl"
              >
                {site.developerName
                  .split(" ")
                  .map((part) => part.charAt(0))
                  .join("")}
              </span>
              <div className="min-w-0">
                <p className="text-2xl font-semibold leading-tight text-text-primary">{site.developerName}</p>
                <p className="mt-1.5 text-sm font-medium leading-relaxed text-text-secondary">
          {t("Glass estimator · author of ProGlass Automation")}
        </p>
                <p className="mt-2.5 font-mono text-2xs uppercase tracking-[0.16em] text-brand-300">
                  Working from {site.developerLocation} · version {site.version}
                </p>
              </div>
            </div>

            <p className="mt-5 text-sm leading-relaxed text-text-secondary">
              He prices glass for a living, and the software began as a way to stop retyping the same measurements.
              There was no plan to sell it and no company formed to build it: the first version was one estimator
              removing the parts of his own day he kept getting wrong. What made it a product is the discovery that the
              same day is happening in glass shops everywhere.
            </p>

            {/*
              Three things he still does himself. Each one is stated elsewhere on
              this site — estimating, maintaining, supporting — so this is a
              summary rather than a new claim.
            */}
            <dl className="mt-6 grid gap-3 sm:grid-cols-3">
              {[
                { label: t("His day job"), value: t("Estimating glass"), note: t("Sizing, pricing, wastage, delivery.") },
                { label: t("On this project"), value: t("Everything"), note: t("Code, documents, tests, this site.") },
                { label: t("On support"), value: t("The only tier"), note: t("Questions arrive at his inbox.") },
              ].map((item) => (
                <div key={item.label} className="stat-tile p-3.5">
                  <dt className="mono-label">{item.label}</dt>
                  <dd className="mt-1.5 text-base font-semibold text-text-primary">{item.value}</dd>
                  <dd className="mt-1 text-[0.75rem] leading-relaxed text-text-muted">{item.note}</dd>
                </div>
              ))}
            </dl>

            <div className="mt-6 flex flex-wrap gap-2">
              <Badge tone="neutral">{t("Sole author")}</Badge>
              <Badge tone="neutral">{t("Self-taught")}</Badge>
              <Badge tone="neutral">Estimator, not engineer</Badge>
              <Badge tone="neutral">No team, no investors</Badge>
            </div>
          </div>

          {/*
            The other half of the answer: the four facts a visitor actually
            weighs when they find out a product is one person's work.
          */}
          <dl className="grid grid-cols-2 gap-3 self-center">
            {[
              { label: t("People on the project"), value: t("One"), hint: "No team, no agency, no contractors." },
              { label: t("Own development PC"), value: t("None yet"), hint: "Built on borrowed and shared machines." },
              { label: t("Funding"), value: t("None"), hint: "No investors, and no sponsors so far." },
              { label: t("Formal training"), value: t("None in software"), hint: "An estimator's background instead." },
            ].map((item) => (
              <div key={item.label} className="stat-tile p-3.5">
                <dt className="mono-label">{item.label}</dt>
                <dd className="mt-1.5 text-base font-semibold text-text-primary">{item.value}</dd>
                <dd className="mt-1 text-[0.75rem] leading-relaxed text-text-muted">{item.hint}</dd>
              </div>
            ))}
          </dl>
        </div>
      </PageHero>

      {/* ── The working history, from the repository's own documents ──── */}
      <Section aria-labelledby="history-heading">
        <Container>
          <div className="grid gap-10 lg:grid-cols-[minmax(0,0.85fr)_minmax(0,1.5fr)] lg:gap-16">
            <div className="lg:sticky lg:top-28 lg:self-start">
              <SectionHeading
                id="history-heading"
                eyebrow={t("The developer's history")}
                title={t("The project's history is written down, dated, and open to reading.")}
                lead={t("There is no CV here, because a CV is the part of a person you cannot check. What exists instead is six working documents at the root of the repository, each carrying the date it was written, describing what broke, what the evidence was, what changed and what is still open. Read together they are a working history of the software — and of its author — more specific than any biography.")}
              />

              <div className="surface mt-7 p-4">
                <p className="mono-label">{t("The documents")}</p>
                <ul className="mt-3 space-y-2">
                  {workingDocuments.map((doc) => (
                    <li key={doc} className="font-mono text-[0.6875rem] leading-relaxed text-text-muted">
                      {doc}
                    </li>
                  ))}
                </ul>
                <p className="mt-3.5 text-xs leading-relaxed text-text-faint">
          {t("All six are at the root of the repository. Nothing on this page quotes a figure that is not in one of them.")}
        </p>
              </div>
            </div>

            <ol className="relative space-y-4 border-l-2 border-line-strong/60 pl-6 sm:pl-8">
              {history.map((entry, index) => (
                <Reveal key={`${entry.date}-${entry.title}`} delay={index * 45}>
                  <li className="relative">
                    {/* The marker on the rule. */}
                    <span
                      aria-hidden="true"
                      className="absolute -left-[1.9rem] top-6 h-3 w-3 rounded-full border-2 border-brand-400 bg-ink-950 sm:-left-[2.4rem]"
                    />
                    <article className="card-live card-interactive p-5 sm:p-6">
                      <div className="flex flex-wrap items-baseline gap-x-3 gap-y-1">
                        <span className="font-mono text-2xs uppercase tracking-[0.16em] text-brand-300">{entry.tag}</span>
                        <span className="font-mono text-2xs uppercase tracking-[0.14em] text-text-faint">
                          {entry.date}
                        </span>
                      </div>
                      <h3 className="mt-2 text-lg font-semibold leading-snug text-text-primary">{entry.title}</h3>
                      <p className="mt-2.5 text-sm leading-relaxed text-text-secondary">{entry.body}</p>
                      <p className="mt-3.5 border-t border-line/60 pt-3 font-mono text-[0.6875rem] leading-relaxed text-text-faint">
                        {entry.source}
                      </p>
                    </article>
                  </li>
                </Reveal>
              ))}
            </ol>
          </div>

          {/* How the work is done — habits the documents themselves show. */}
          <div className="mt-14">
            <SectionHeading
              eyebrow={t("The method")}
              title={t("Four habits you can check against the files above.")}
              lead={t("None of these is a claim about character. Each one is visible in the documents this page just cited, which is the only reason it is on the page at all.")}
            />
            <div className="mt-8 grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
              {workingMethod.map((item, index) => (
                <Reveal key={item.title} delay={index * 50}>
                  <article className="surface h-full p-5">
                    <IndexChip>{item.index}</IndexChip>
                    <h3 className="mt-3 text-base font-semibold text-text-primary">{item.title}</h3>
                    <p className="mt-2.5 text-sm leading-relaxed text-text-muted">{item.body}</p>
                  </article>
                </Reveal>
              ))}
            </div>
          </div>
        </Container>
      </Section>

      {/* ── How it started ───────────────────────────────────────────── */}
      <Section aria-labelledby="start-heading">
        <Container>
          <div className="grid gap-10 lg:grid-cols-[minmax(0,0.95fr)_minmax(0,1.35fr)] lg:gap-16">
            <div>
              <SectionHeading
                id="start-heading"
                eyebrow={t("How it started")}
                title={t("The job was repetitive, and the repetition was the problem.")}
                lead={t("Estimating glass means holding a lot of small numbers in your head at once: a size, a thickness, a build-up, a rate, a wastage allowance, a delivery charge. None of them are difficult on their own. All of them together, every day, on paper, is where mistakes come from.")}
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
            eyebrow={t("What that background produced")}
            title={t("Being a user first is the reason the screens look like this.")}
            lead={t("A trained engineer would have started from the data model. This started from the working day, and it shows in decisions that are unusual for business software — and deliberate.")}
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
            eyebrow={t("How the work is done")}
            title={t("Three rules the codebase is held to.")}
            lead={t("These are not aspirations written on a wall. Each is enforced somewhere in the build, which is why they have survived at this size — and why a solo author can still answer for the result.")}
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
              eyebrow={t("Technology")}
              title={t("Boring, in the best sense.")}
              lead={t("A Microsoft desktop stack and mature libraries, chosen so a business can rely on it for years rather than rebuild it in three. Nothing experimental is in the production path — which matters more, not less, when one person is maintaining it.")}
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
                eyebrow={t("Honestly, what is missing")}
                title={t("One person is not a team, and this reads better than pretending otherwise.")}
                lead={t("There is no office, no second developer, no designer, no translator and no one on support. The build is as far along as one estimator working evenings could take it. The next stage needs help, and the gaps are listed by name rather than hinted at.")}
              />
              <div className="mt-8 flex flex-wrap gap-3">
                <ButtonLink href="/sponsors" size="lg" trailing={<ArrowRight />}>
          {t("See the eight gaps")}
        </ButtonLink>
                <ButtonLink href="/contact" variant="secondary" size="lg">
          {t("Offer a hand")}
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
            eyebrow={t("Reasonable questions")}
            title={t("The four things people ask about a one-person product.")}
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
          {t("You talk to the person who writes it.")}
        </h2>
              <p className="measure mt-4 text-sm leading-relaxed text-text-secondary">
                There is no support tier between a customer and the author. Describe the job you do and where it goes
                wrong, and the answer comes back from the person who can change the software — usually with a question
                about how your shop actually works.
              </p>
              <div className="mt-7 flex flex-wrap gap-3">
                <ButtonLink href="/contact" size="lg" trailing={<ArrowRight />}>
          {t("Request a Demo")}
        </ButtonLink>
                <ButtonLink href="/about" variant="secondary" size="lg">
          {t("About the product")}
        </ButtonLink>
              </div>
            </div>

            <div className="space-y-3">
              <div className="flex flex-wrap gap-2">
                <Badge tone="neutral">{t("Sole author")}</Badge>
                <Badge tone="neutral">{t("Self-taught")}</Badge>
                <Badge tone="neutral">{t("Desktop only")}</Badge>
                <Badge tone="neutral">Version {site.version}</Badge>
              </div>
              <p className="text-xs leading-relaxed text-text-faint font-medium">
                Every claim about the software on this site names the file it comes from. If something here cannot be
                checked, it should not be here — say so and it will be corrected.{" "}
                <Link href="/sponsors" className="link-underline">
          {t("Sponsorship and contribution")}
        </Link>
              </p>
            </div>
          </div>
        </Container>
      </Section>
    </>
  );
}

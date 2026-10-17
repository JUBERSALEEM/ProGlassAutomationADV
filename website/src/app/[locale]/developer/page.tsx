import type { Metadata } from "next";
import { LocaleLink as Link } from "@/components/ui/LocaleLink";
import { Container, Section } from "@/components/ui/Container";
import { PageHero } from "@/components/layout/PageHero";
import { footerIconFor } from "@/data/footerIcons";
import { ArrowRight, ButtonLink } from "@/components/ui/Button";
import { Badge, IconChip, IndexChip } from "@/components/ui/Badge";
import { Reveal } from "@/components/ui/Reveal";
import { SectionHeading } from "@/components/ui/SectionHeading";
import { Accordion } from "@/components/ui/Accordion";
import {
  dream,
  history,
  journey,
  overthinking,
  technicalWorkflowArchitecture,
  workingDocuments,
  workingMethod,
} from "@/data/developerHistory";
import { modules } from "@/data/modules";
import { StatCounter, StatBadge } from "@/components/ui/Stat";
import { site } from "@/lib/site";
import { RouteSchema } from "@/components/seo/RouteSchema";
import { pageMetadata } from "@/lib/metadata";
import { translatorFor } from "@/i18n/server";
import { PageOverlay } from "@/i18n/PageOverlay";

type PageProps = { params: Promise<{ locale: string }> };


/*
 * The dream's four cards, pictured. Drawn here rather than in the data file
 * because the data file is the author's own ledger in his own words — the
 * marks are the page's furniture: collaborators for the team card, the
 * licence key for the product card, the reports bars for shops-count growth,
 * and an hourglass for the years already in it.
 */
const dreamCardIcons = [
  /* two figures and a joined hand — the Collaborate nav glyph */
  "M6 7.5a2 2 0 1 0 0-4 2 2 0 0 0 0 4zM12.5 7a1.6 1.6 0 1 0 0-3.2 1.6 1.6 0 0 0 0 3.2zM2.5 13c0-1.9 1.6-3.2 3.5-3.2S9.5 11.1 9.5 13M10.5 10.2c1.6.2 3 1.4 3 2.8",
  /* key — the licence card, shared with the legal footer */
  "M9 7.5a3 3 0 1 0-3 3.8L7.5 10v2l1.5 1.5v-1h1.5v-2zM9.9 6h.4",
  /* bars going up — the reports glyph */
  "M2.5 13.5h11M4 13.5V9M7 13.5V5.5M10 13.5V8M13 13.5V3.5",
  /* hourglass */
  "M4 2.5h8M4 13.5h8M5.5 2.5v3l2.5 3 2.5-3v-3M5.5 13.5v-3l2.5-3 2.5 3v3",
];

export async function generateMetadata({ params }: PageProps): Promise<Metadata> {
  const t = await translatorFor(params);
  return pageMetadata({
  locale: t.locale,
  title: t("The developer — one glass estimator"),
  description: t("ProGlass Automation is written by Juber Saleem, a glass estimator rather than a trained engineer: how it started, and what he intends to build."),
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
  { title: t("A cutting table to test against"), body: t("The optimiser writes generic DXF and a cut-list CSV today; Bottero .OPT, Intermac .ISO and Lisec .DAT are declared but not yet written — each waits on one sample file from that table. A cutting table is what finishes them.") },
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
    question: t("Is the author looking for help with it?"),
    answer: t("Yes, and it is the one part of this page that is a request rather than a record. The application is one person's work on borrowed machines, and it needs a fabricator's performance data, a backer, a machine or a second pair of hands to go further. What each of those means, and what comes back for it, is set out on the collaboration page."),
  },
  {
    question: t("Who do I speak to if something is wrong?"),
    answer:
      t.template`The person who wrote it. A question about why the optimiser chose a particular plan, or a request to change how a charge row behaves, reaches the same inbox: ${site.developerEmail}.`,
  },
];
  const locale = t.locale;
  return (
    <PageOverlay locale={t.locale} route="developer">
    <>
    {/* Structured data for this route — see lib/schema.ts. */}
    <RouteSchema
      locale={locale}
      path="/developer"
      title={t("The developer — one glass estimator")}
      description={t("ProGlass Automation is written by Juber Saleem, a glass estimator rather than a trained engineer. One person, no team, no investors — and how that shaped the product.")}
    />
      <PageHero
      t={t}
        glyph={footerIconFor("/developer")!}
        eyebrow={t("The developer")}
        banner="people"
        title={
          <>{t("One glass estimator,")}<span className="text-gradient block">{t("working alone, writing his own tools.")}</span>
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
            <ButtonLink href="/collaborate" variant="secondary" size="lg">
          {t("What he is asking for")}
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
        <div className="surface-raised grid gap-6 lg:grid-cols-[minmax(0,1.25fr)_minmax(0,1fr)] lg:gap-10 card-pad">
          <div>
            <div className="flex items-start gap-5">
              <span
                aria-hidden="true"
                className="grid h-16 w-16 shrink-0 place-items-center rounded-panel border-2 border-line-strong/60 bg-ink-900/70 text-xl font-semibold text-brand-200 sm:h-20 sm:w-20 sm:text-2xl"
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
                  {t.template`Working from ${site.developerLocation} · version ${site.version}`}
                </p>
              </div>
            </div>

            <p className="mt-5 text-sm leading-relaxed text-text-secondary">{t("He prices glass for a living, and the software began as a way to stop retyping the same measurements. There was no plan to sell it and no company formed to build it: the first version was one estimator removing the parts of his own day he kept getting wrong. What made it a product is the discovery that the same day is happening in glass shops everywhere.")}</p>

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
                <div key={t(item.label)} className="surface-raised p-3.5">
                  <dt className="mono-label">{t(item.label)}</dt>
                  <dd className="mt-1.5 text-base font-semibold text-text-primary">{t(item.value)}</dd>
                  <dd className="mt-1 text-[0.75rem] leading-relaxed text-text-muted">{t(item.note)}</dd>
                </div>
              ))}
            </dl>

            <div className="mt-6 flex flex-wrap gap-2">
              <Badge tone="neutral">{t("Sole author")}</Badge>
              <Badge tone="neutral">{t("Self-taught")}</Badge>
              <Badge tone="neutral">{t("Estimator, not engineer")}</Badge>
              <Badge tone="neutral">{t("No team, no investors")}</Badge>
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
              <div key={t(item.label)} className="surface-raised p-3.5">
                <dt className="mono-label">{t(item.label)}</dt>
                <dd className="mt-1.5 text-base font-semibold text-text-primary">{t(item.value)}</dd>
                <dd className="mt-1 text-[0.75rem] leading-relaxed text-text-muted">{t(item.hint)}</dd>
              </div>
            ))}
          </dl>
        </div>
      </PageHero>

      {/* ── The journey ─────────────────────────────────────────────────
          Four phases that happened and one that has not. The engineering
          record sits under this, dated and checkable; this is the story the
          record is evidence for, because "who wrote this" is a question about
          the person and not about the commit log. */}
      <Section aria-labelledby="journey-heading">
        <Container>
          <SectionHeading
            id="journey-heading"
            eyebrow={t("The journey")}
            title={t("From a glass counter to a product nobody else had to write.")}
            lead={t("An estimator who could not find software that knew what an SGU air space was, and wrote one instead. The order below is the order it happened in — the last entry is the part still being decided.")}
          />

          <ol className="mt-8 grid gap-5 lg:grid-cols-2">
            {journey.map((phase, index) => {
              /* The open phase is the last one and has no proof to cite, so it
                 is the one card drawn with an accent edge rather than
                 pretending to be another closed chapter. */
              const open = index === journey.length - 1;
              return (
                <Reveal key={phase.index} as="li" delay={Math.min(index, 4) * 60}>
                  <article
                    className={
                      open
                        ? "surface-raised card-pad card-interactive flex h-full flex-col border-brand-400/35"
                        : "surface card-pad card-interactive flex h-full flex-col"
                    }
                  >
                    <div className="flex items-start justify-between gap-3">
                      <div className="flex items-center gap-3">
                        <IndexChip>{phase.index}</IndexChip>
                        <h3 className="card-title">{t(phase.title)}</h3>
                      </div>
                      <span className="mono-label shrink-0">{t(phase.when)}</span>
                    </div>
                    <p className="mt-4 text-sm leading-relaxed text-text-muted">{t(phase.body)}</p>
                    {phase.proof ? (
                      <p className="mono-label mt-auto pt-5">
                        <span className="sr-only">{t("In the source tree:")}</span>
                        {t(phase.proof)}
                      </p>
                    ) : null}
                  </article>
                </Reveal>
              );
            })}
          </ol>
        </Container>
      </Section>

      {/* ── When overthinking is used in success ───────────────────────── */}
      <Section tone="raised" aria-labelledby="overthinking-heading">
        <Container>
          <div className="grid gap-6 lg:grid-cols-[minmax(0,0.95fr)_minmax(0,1.25fr)] lg:gap-10">
            <div className="lg:sticky lg:top-28 lg:self-start">
              <SectionHeading
                id="overthinking-heading"
                eyebrow={t("When overthinking is used in success")}
                title={t(overthinking.thesis)}
                lead={t(overthinking.lead)}
              />
              <p className="mt-6 rounded-card border border-line/60 bg-ink-950/40 p-4 text-xs font-medium leading-relaxed text-text-faint">
                {t(overthinking.cost)}
              </p>
            </div>

            <ol className="flow">
              {overthinking.points.map((point, index) => (
                <Reveal key={point.index} as="li" delay={index * 60}>
                  <div className="surface flex gap-4 card-pad">
                    <IndexChip>{point.index}</IndexChip>
                    <div className="min-w-0">
                      <h3 className="card-title">{t(point.title)}</h3>
                      <p className="mt-2.5 text-sm leading-relaxed text-text-muted">{t(point.body)}</p>
                      {point.proof ? (
                        <p className="mono-label mt-3.5 border-t border-line/60 pt-3">
                          <span className="sr-only">{t("In the source tree:")}</span>
                          {point.proof}
                        </p>
                      ) : null}
                    </div>
                  </div>
                </Reveal>
              ))}
            </ol>
          </div>
        </Container>
      </Section>

      {/* ── The dream ────────────────────────────────────────────────── */}
      <Section aria-labelledby="dream-heading">
        <Container>
          <div className="surface-raised card-pad">
            <div className="border-b border-line/60 pb-2">
              <div className="min-w-0">
                <p className="eyebrow">{t("What it is for")}</p>
                <h2 id="dream-heading" className="display-2 mt-3">
                  {t("Not a side project that stays one.")}
                </h2>
              </div>
              <Badge tone="accent">{t("Stated as an intention")}</Badge>
            </div>

            <p className="max-w-3xl pt-6 text-sm leading-relaxed text-text-secondary">{t(dream.lead)}</p>

            <div className="feature-plates mt-8 grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
              {dream.intentions.map((item, index) => (
                <div key={t(item.title)} className="surface-raised p-5">
                  <IconChip path={dreamCardIcons[index % dreamCardIcons.length]!} className="mb-4" />
                  <h3 className="card-title">{t(item.title)}</h3>
                  <p className="mt-2.5 text-sm leading-relaxed text-text-muted">{t(item.body)}</p>
                </div>
              ))}
            </div>

            <figure className="mt-8 border-t border-line/60 pt-6">
              <blockquote className="max-w-3xl text-base leading-relaxed text-text-secondary sm:text-lg">
                &ldquo;{t(dream.quote)}&rdquo;
              </blockquote>
              <figcaption className="mono-label mt-4">{t(dream.attribution)}</figcaption>
            </figure>
          </div>
        </Container>
      </Section>

      {/* ── The working history, from the repository's own documents ──── */}
      <Section aria-labelledby="history-heading">
        <Container>
          <div className="grid gap-6 lg:grid-cols-[minmax(0,0.85fr)_minmax(0,1.5fr)] lg:gap-10">
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
                <Reveal key={`${entry.date}-${t(entry.title)}`} delay={index * 45}>
                  <li className="relative">
                    {/* The marker on the rule. */}
                    <span
                      aria-hidden="true"
                      className="absolute -left-[1.9rem] top-6 h-3 w-3 rounded-full border-2 border-brand-400 bg-ink-950 sm:-left-[2.4rem]"
                    />
                    <article className="surface card-interactive card-pad">
                      <div className="flex flex-wrap items-baseline gap-x-3 gap-y-1">
                        <span className="font-mono text-2xs uppercase tracking-[0.16em] text-brand-300">{entry.tag}</span>
                        <span className="font-mono text-2xs uppercase tracking-[0.14em] text-text-muted">
                          {entry.date}
                        </span>
                      </div>
                      <h3 className="mt-2 text-lg font-semibold leading-snug text-text-primary">{t(entry.title)}</h3>
                      <p className="mt-2.5 text-sm leading-relaxed text-text-secondary">{t(entry.body)}</p>
                      <p className="mt-3.5 border-t border-line/60 pt-3 font-mono text-[0.6875rem] leading-relaxed text-text-muted">
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
            <div className="feature-plates mt-8 grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
              {workingMethod.map((item, index) => (
                <Reveal key={t(item.title)} delay={index * 50}>
                  <article className="surface h-full p-5">
                    <IndexChip>{item.index}</IndexChip>
                    <h3 className="mt-3 text-base font-semibold text-text-primary">{t(item.title)}</h3>
                    <p className="mt-2.5 text-sm leading-relaxed text-text-muted">{t(item.body)}</p>
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
          <div className="grid gap-6 lg:grid-cols-[minmax(0,0.95fr)_minmax(0,1.35fr)] lg:gap-10">
            <div>
              <SectionHeading
                id="start-heading"
                eyebrow={t("How it started")}
                title={t("The job was repetitive, and the repetition was the problem.")}
                lead={t("Estimating glass means holding a lot of small numbers in your head at once: a size, a thickness, a build-up, a rate, a wastage allowance, a delivery charge. None of them are difficult on their own. All of them together, every day, on paper, is where mistakes come from.")}
              />
              <p className="flow measure mt-6 text-sm leading-relaxed text-text-muted">{t("The software began as a way to stop retyping. It was not planned as a product, and there was no intention to sell it. It was one estimator removing the parts of his own day he kept getting wrong — and then discovering that the same day is happening in glass shops everywhere.")}</p>
            </div>

            <ol className="flow">
              {before.map((item, index) => (
                <Reveal key={`${t(item.title)}-${index}`} delay={index * 60}>
                  <li className="surface flex gap-4 p-5">
                    <IndexChip>{item.index}</IndexChip>
                    <div className="min-w-0">
                      <h3 className="text-base font-semibold text-text-primary">{t(item.title)}</h3>
                      <p className="mt-2 text-sm leading-relaxed text-text-muted">{t(item.body)}</p>
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

          <div className="mt-8 grid gap-4 md:grid-cols-2 xl:grid-cols-5">
            {consequences.map((item, index) => (
              <Reveal key={`${t(item.title)}-${index}`} delay={index * 50}>
                <article className="surface card-interactive flex h-full flex-col p-5">
                  <span className="font-mono text-2xs text-brand-300">{item.index}</span>
                  <h3 className="mt-2 text-base font-semibold text-text-primary">{t(item.title)}</h3>
                  <p className="mt-2.5 text-sm leading-relaxed text-text-muted">{t(item.body)}</p>
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

          <div className="mt-8 grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
            {principles.map((item, index) => (
              <Reveal key={`${t(item.title)}-${index}`} delay={index * 60}>
                <article className="surface h-full card-pad-sm">
                  <div className="flex items-center gap-3">
                    <IndexChip>{item.index}</IndexChip>
                    <h3 className="text-base font-semibold text-text-primary">{t(item.title)}</h3>
                  </div>
                  <p className="mt-4 text-sm leading-relaxed text-text-muted">{t(item.body)}</p>
                </article>
              </Reveal>
            ))}
          </div>

          <div className="mt-6 grid gap-4 sm:grid-cols-3">
            <StatCounter
              value={site.testCount}
              label={t("Test methods")}
              hint={t.template`Across ${site.testFiles} test files, plus ${site.testInlineCases.toLocaleString("en-GB")} [InlineData] cases.`}
            />
            <StatBadge value={site.pattern} label="Architecture" hint={t("Views never contain business logic.")} />
            <StatBadge
              value={`${site.productionFiles.toLocaleString("en-GB")} files`}
              label={t("Production source")}
              hint={t.template`${site.productionLines.toLocaleString("en-GB")} lines outside obj/ and bin/, counted against the application tree.`}
            />
          </div>
        </Container>
      </Section>

      {/* ── Stack ────────────────────────────────────────────────────── */}
      <Section tone="sunken" aria-labelledby="stack-heading">
        <Container>
          <div className="grid gap-6 lg:grid-cols-[minmax(0,1fr)_minmax(0,1.3fr)] lg:gap-10">
            <SectionHeading
              id="stack-heading"
              eyebrow={t("Technology")}
              title={t("Boring, in the best sense.")}
              lead={t("A Microsoft desktop stack and mature libraries, chosen so a business can rely on it for years rather than rebuild it in three. Nothing experimental is in the production path — which matters more, not less, when one person is maintaining it.")}
            />

            <dl className="grid gap-3 sm:grid-cols-2">
              {stack.map((row) => (
                <div key={t(row.group)} className="surface p-4">
                  <dt className="mono-label">{t(row.group)}</dt>
                  <dd className="mt-2 flex flex-wrap gap-1.5">
                    {row.items.map((item) => (
                      <span key={item} className="chip">
                        {t(item)}
                      </span>
                    ))}
                  </dd>
                </div>
              ))}
            </dl>
          </div>

          {/* ── Engineering Scale & Codebase Metrics ──────────────────────── */}
          <div className="mt-14 pt-12 border-t border-line/50">
            <div className="flex flex-col items-center gap-6 text-center">
              <div>
                <p className="eyebrow mb-2 text-brand-300">{t("Engineering Metrics")}</p>
                <h3 className="text-xl font-bold tracking-tight text-text-primary sm:text-2xl">
                  {t("What the repository contains.")}
                </h3>
                <p className="mt-2 max-w-xl text-sm leading-relaxed text-text-muted">
                  {t("Every number below is counted directly from the source tree: bounded contexts, test suites, and localization dictionaries.")}
                </p>
              </div>
              <p className="mx-auto max-w-xs text-xs leading-relaxed text-text-muted font-medium">
                {t("Measured on {v1}: {v2}, {v3}, {v4}. obj/, bin/ and .git/ excluded.", {
                  v1: site.statsMeasuredOn,
                  v2: site.framework,
                  v3: site.pattern,
                  v4: site.database,
                })}
              </p>
            </div>

            <div className="mt-8 grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
              <StatCounter
                value={modules.length}
                label={t("Navigation targets")}
                hint={t("Every address the sidebar rail can open, across nine groups.")}
              />
              <StatCounter
                value={site.databaseCount}
                label={t("SQLite databases")}
                hint={t("One DbContext per bounded context, with additive-only migrations.")}
              />
              <StatCounter
                value={site.locales}
                label={t("Languages")}
                hint={t("Locales are resource dictionaries — adding one needs no code change.")}
              />
              <StatCounter
                value={site.testCount}
                label={t("Test methods")}
                hint={t.template`[Fact] and [Theory] methods across ${site.testFiles} test files, plus ${site.testInlineCases.toLocaleString("en-GB")} [InlineData] cases.`}
              />
            </div>
          </div>

          {/* ── Workflow Implementation & C# Service Mapping ────────────────── */}
          <div className="mt-14 pt-12 border-t border-line/50">
            <div>
              <p className="eyebrow mb-2 text-brand-300">{t("Workflow Architecture")}</p>
              <h3 className="text-xl font-bold tracking-tight text-text-primary sm:text-2xl">
                {t("C# services & implementation guarantees.")}
              </h3>
              <p className="mt-2 max-w-2xl text-sm leading-relaxed text-text-muted">
                {t("How the nine business workflow stages are structured across the application code. Each entry lists the authoritative services enforcing transitions, pricing calculations, and balance safety.")}
              </p>
            </div>

            <div className="mt-8 grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
              {technicalWorkflowArchitecture.map((item) => (
                <div
                  key={item.index}
                  className="surface card-interactive p-5 flex flex-col justify-between"
                >
                  <div>
                    <div className="flex items-center justify-between gap-2">
                      <span className="font-mono text-2xs text-brand-300 font-bold">
                        {item.index}
                      </span>
                      <span className="text-2xs font-mono text-text-muted">
                        {item.stage}
                      </span>
                    </div>
                    <h4 className="mt-2 text-sm font-semibold text-text-primary">
                      {t(item.stage)}
                    </h4>
                    <p className="mt-2 text-xs leading-relaxed text-text-muted">
                      {t(item.architecture)}
                    </p>
                  </div>
                  <div className="mt-4 pt-3 border-t border-line/40">
                    <p className="font-mono text-2xs text-text-muted uppercase tracking-wider mb-1.5">
                      {t("Service files")}
                    </p>
                    <ul className="space-y-1">
                      {item.sourceFiles.map((src) => (
                        <li
                          key={src}
                          className="font-mono text-2xs text-brand-200/90 break-all bg-ink-950/60 rounded px-1.5 py-0.5"
                        >
                          {src}
                        </li>
                      ))}
                    </ul>
                  </div>
                </div>
              ))}
            </div>
          </div>
        </Container>
      </Section>

      {/* ── What is still missing ────────────────────────────────────── */}
      <Section aria-labelledby="gaps-heading">
        <Container>
          <div className="grid gap-6 lg:grid-cols-[minmax(0,1.2fr)_minmax(0,1fr)] lg:items-center lg:gap-10">
            <div>
              <SectionHeading
                id="gaps-heading"
                eyebrow={t("Honestly, what is missing")}
                title={t("One person is not a team, and this reads better than pretending otherwise.")}
                lead={t("There is no office, no second developer, no designer, no translator and no one on support. The build is as far along as one estimator working evenings could take it. The next stage needs help, and the gaps are listed by name rather than hinted at.")}
              />
              <div className="mt-8 flex flex-wrap gap-3">
                <ButtonLink href="/collaborate" size="lg" trailing={<ArrowRight />}>
          {t("See what is being asked for")}
        </ButtonLink>
                <ButtonLink href="/sponsors" variant="secondary" size="lg">
          {t("See the eight gaps")}
        </ButtonLink>
              </div>
            </div>

            <ul className="grid gap-3 sm:grid-cols-2">
              {gaps.map((gap) => (
                /* The gaps are the section's MAIN content cards, so they
                   wear the standard surface, and so does every other card
                   on this page: no pastel tiles, no accent slabs — the
                   family's dynamic recipe (surface / surface-raised) is
                   the only card language here, so the page reads the same
                   in every appearance and mode. */
                <li key={t(gap.title)} className="surface p-4">
                  <h3 className="text-sm font-semibold text-text-primary">{t(gap.title)}</h3>
                  <p className="mt-2 text-xs leading-relaxed text-text-secondary">{t(gap.body)}</p>
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
          <div className="surface-raised grid gap-6 lg:grid-cols-[minmax(0,1.4fr)_minmax(0,1fr)] lg:items-center card-pad">
            <div>
              <h2 id="dev-cta-heading" className="display-2">
          {t("You talk to the person who writes it.")}
        </h2>
              <p className="measure mt-4 text-sm leading-relaxed text-text-secondary">{t("There is no support tier between a customer and the author. Describe the job you do and where it goes wrong, and the answer comes back from the person who can change the software — usually with a question about how your shop actually works.")}</p>
              <div className="mt-7 flex flex-wrap gap-3">
                <ButtonLink href="/contact" size="lg" trailing={<ArrowRight />}>
          {t("Request a Demo")}
        </ButtonLink>
                <ButtonLink href="/collaborate" variant="secondary" size="lg">
          {t("Work with the developer")}
        </ButtonLink>
              </div>
            </div>

            <div className="space-y-3">
              <div className="flex flex-wrap gap-2">
                <Badge tone="neutral">{t("Sole author")}</Badge>
                <Badge tone="neutral">{t("Self-taught")}</Badge>
                <Badge tone="neutral">{t("Desktop only")}</Badge>
                <Badge tone="neutral">Version {site.version}</Badge>
                <Badge tone="neutral">{t.template`Development line ${site.developmentVersion}`}</Badge>
              </div>
              <p className="text-xs leading-relaxed text-text-faint font-medium">
{t("Every claim about the software on this site names the file it comes from. If something here cannot be checked, it should not be here — say so and it will be corrected.")}.{" "}
                <Link href="/collaborate" className="link-underline">
          {t("Collaboration")}
        </Link>{" "}
                {t("and")}{" "}
                <Link href="/sponsors" className="link-underline">
          {t("sponsorship")}
        </Link>
              </p>
            </div>
          </div>
        </Container>
      </Section>
    </>
    </PageOverlay>
  );
}

import type { Metadata } from "next";
import { LocaleLink as Link } from "@/components/ui/LocaleLink";
import { Container, Section } from "@/components/ui/Container";
import { PageHero } from "@/components/layout/PageHero";
import { ArrowRight, ButtonLink } from "@/components/ui/Button";
import { Badge } from "@/components/ui/Badge";
import { Reveal } from "@/components/ui/Reveal";
import { SectionHeading } from "@/components/ui/SectionHeading";
import { Accordion } from "@/components/ui/Accordion";
import { StatBadge, StatCounter } from "@/components/ui/Stat";
import { sponsorFaq, sponsorNeeds } from "@/data/sponsors";
import { site } from "@/lib/site";
import { RouteSchema } from "@/components/seo/RouteSchema";
import { pageMetadata } from "@/lib/metadata";
import { translatorFor } from "@/i18n/server";

type PageProps = { params: Promise<{ locale: string }> };

export async function generateMetadata({ params }: PageProps): Promise<Metadata> {
  const t = await translatorFor(params);
  return pageMetadata({
  locale: t.locale,
  title: t("Sponsorship and collaboration"),
  description: t("ProGlass Automation is built by one developer. What sponsorship funds, which work is open to collaborators, and how to get involved without buying influence."),
  path: "/sponsors",
  keywords: ["sponsor glass software", t("open collaboration WPF project"), "glass software developer sponsorship", t("ProGlass Automation contributors")],
  ogEyebrow: t("Sponsorship"),
  });
}

const SHAPE_TONE = {
  Time: "accent",
  Money: "success",
  Hardware: "warning",
  Reach: "neutral",
} as const;

export default async function SponsorsPage({ params }: PageProps) {
  const t = await translatorFor(params);
  const locale = t.locale;
  return (
    <>
    {/* Structured data for this route — see lib/schema.ts. */}
    <RouteSchema
      locale={locale}
      path="/sponsors"
      title={t("Sponsorship and collaboration")}
      description={t("ProGlass Automation is built by one developer. What sponsorship funds, which work is open to collaborators, and how to get involved without buying influence.")}
    />
      <PageHero
      t={t}
        eyebrow={t("Support the build")}
        title={t("One developer, one desktop product, and room for help.")}
        lead={t("ProGlass Automation is a complete commercial chain with a cutting-optimization engine behind it, written and maintained by a single person. It is not short of ideas. It is short of hours, hardware to test against, and reviewed translations.")}
        banner="support"
        breadcrumb={[
          { label: t("Home"), href: "/" },
          { label: t("Sponsors"), href: "/sponsors" },
        ]}
        meta={[
          { label: t("Maintainers"), value: "1" },
          { label: t("Sponsors so far"), value: t("None — this page is the ask") },
          { label: t("Licence model"), value: t("Owned, not rented") },
          { label: t("Contact"), value: site.developerEmail },
        ]}
        actions={
          <>
            <ButtonLink href="/contact" size="lg" trailing={<ArrowRight />}>
          {t("Talk to the developer")}
        </ButtonLink>
            <ButtonLink href="#ways" variant="secondary" size="lg">
          {t("What help looks like")}
        </ButtonLink>
          </>
        }
      />

      {/* ── Why this page exists ─────────────────────────────────── */}
      <Section aria-labelledby="why-heading">
        <Container>
          <div className="grid gap-12 lg:grid-cols-[minmax(0,1.05fr)_minmax(0,0.95fr)] lg:gap-16">
            <div>
              <SectionHeading
                id="why-heading"
                eyebrow={t("Why this page exists")}
                title={t("A product this specific does not get built by a marketing budget.")}
                lead={t("Generic ERP software is written once and sold broadly. Software that knows what an SGU air space is, why a trim cut into laminated stock ruins the sheet, and how the eighth production stage differs from the first — that gets written by someone who has watched it happen.")}
              />
              <div className="mt-8 space-y-4 text-sm leading-relaxed text-text-muted">
                <p>{t("That is the situation here. The application is real and in use on the commercial side: inquiry through statement, with the optimization engine and the eight production stages behind it. What it does not have is a second pair of hands, a test table to send cut files to, or a translation reviewed by somebody who works in the language.")}</p>
                <p>{t("This page states those gaps plainly. It is a request, not a record of support — there are no sponsors to thank yet, and inventing a tier list with badges and logos would be exactly the kind of thing this website refuses to do everywhere else.")}</p>
                <p className="text-text-secondary">{t("If you are a glass fabricator, a WPF developer, a translator or simply someone who thinks a one-person industrial product is worth backing, there is a slot below that fits what you already have.")}</p>
              </div>
            </div>

            <Reveal delay={80}>
              <div className="grid gap-4 sm:grid-cols-2">
                <StatCounter
                  value={site.moduleCount}
                  label={t("Navigation targets")}
                  hint={t("One product, from dashboard to diagnostics.")}
                />
                <StatCounter
                  value={site.testCount}
                  label={t("Test methods")}
                  hint={`Across ${site.testFiles} files; invariants, not getters.`}
                />
                <StatBadge
                  value="One"
                  label="Maintainer"
                  hint={t("No team, no agency, no outsourced roadmap.")}
                />
                <StatBadge
                  value="None yet"
                  label={t("Sponsors so far")}
                  hint={t("Stated because the site does not pad numbers.")}
                />
              </div>

              <div className="card-accent mt-4 p-6">
                <p className="eyebrow">{t("The rule this page keeps")}</p>
                <p className="mt-3 text-sm leading-relaxed text-text-secondary">{t("Sponsorship funds work. It does not buy a roadmap position, a feature nobody needs, or a claim on the website that the software cannot demonstrate.")}</p>
              </div>
            </Reveal>
          </div>
        </Container>
      </Section>

      {/* ── Ways to help ─────────────────────────────────────────── */}
      <Section tone="raised" id="ways" aria-labelledby="ways-heading">
        <Container>
          <SectionHeading
            id="ways-heading"
            eyebrow={t("What help looks like")}
            title={t("Eight specific gaps, not one vague appeal.")}
            lead={t("Each entry says where the work currently stands and what would change if somebody took it on. Where the work maps to the codebase, the file is named so it can be read before anything is offered.")}
            align="center"
          />

          <ul className="mt-12 grid gap-5 lg:grid-cols-2">
            {sponsorNeeds.map((need, index) => (
              <Reveal key={need.id} as="li" delay={Math.min(index, 6) * 55}>
                <article className="surface card-interactive flex h-full flex-col p-6">
                  <div className="flex items-start justify-between gap-4">
                    <span
                      aria-hidden="true"
                      className="grid h-9 w-9 shrink-0 place-items-center rounded-xl border border-brand-400/30 bg-brand-400/10 text-brand-300"
                    >
                      <NeedIcon name={need.icon} />
                    </span>
                    <Badge tone={SHAPE_TONE[need.shape]}>{need.shape}</Badge>
                  </div>

                  <h3 className="mt-4 text-base font-semibold text-text-primary">{t(need.title)}</h3>
                  <p className="mt-2.5 text-sm leading-relaxed text-text-muted">{t(need.status)}</p>

                  <div className="mt-4 rounded-card border border-line/60 bg-ink-950/40 p-4">
                    <p className="mono-label">{t("What would change")}</p>
                    <p className="mt-2 text-sm leading-relaxed text-text-secondary">{t(need.ask)}</p>
                  </div>

                  {need.evidence ? (
                    <p className="mono-label mt-auto pt-5">
                      <span className="sr-only">{t("In the source tree:")}</span>
                      {t(need.evidence)}
                    </p>
                  ) : null}
                </article>
              </Reveal>
            ))}
          </ul>
        </Container>
      </Section>

      {/* ── How support works ────────────────────────────────────── */}
      <Section aria-labelledby="how-heading">
        <Container>
          <div className="grid gap-12 lg:grid-cols-[minmax(0,0.95fr)_minmax(0,1.05fr)] lg:gap-16">
            <div>
              <SectionHeading
                id="how-heading"
                eyebrow={t("How it works")}
                title={t("Say what the project is worth to you, and what you want back.")}
                lead={t("There is no sponsorship platform wired up yet, no tier list and no published amounts. Until there is, this is a conversation by email — which suits a product built this way.")}
              />

              <ol className="mt-8 space-y-3">
                {[
                  {
                    step: "01",
                    title: t("Write, briefly"),
                    body: t("What you do, and which gap on this page you are looking at. One paragraph is enough."),
                  },
                  {
                    step: "02",
                    title: t("Get an honest answer"),
                    body: t("Including a no, if the fit is wrong — a project this size cannot absorb work it cannot maintain."),
                  },
                  {
                    step: "03",
                    title: t("Decide the shape"),
                    body: t("Time, funding, a machine day or a translation review. Terms are agreed directly, in writing, before anything starts."),
                  },
                  {
                    step: "04",
                    title: t("See it land"),
                    body: t("Whatever is agreed gets named in the release note for the version it appears in."),
                  },
                ].map((item) => (
                  <li
                    key={t(item.step)}
                    className="flex gap-4 rounded-card border border-line/60 bg-ink-900/50 p-4"
                  >
                    <span className="font-mono text-2xs text-brand-300">{t(item.step)}</span>
                    <div>
                      <p className="text-sm font-semibold text-text-secondary">{t(item.title)}</p>
                      <p className="mt-1 text-sm leading-relaxed text-text-muted">{t(item.body)}</p>
                    </div>
                  </li>
                ))}
              </ol>
            </div>

            <Reveal delay={80}>
              <div className="flow">
                <div className="plate p-6 sm:p-7">
                  <p className="eyebrow">{t("Where to write")}</p>
                  <h3 className="mt-3 text-lg font-semibold text-text-primary">{t("One address, read by the person who writes the code.")}</h3>
                  <p className="mt-2 text-sm leading-relaxed text-text-muted">{t("There is no sales team and no support queue between you and the developer. Mention &ldquo;sponsorship&rdquo; or the specific gap you are interested in, and the reply will come from {v1}.", { v1: site.developerName })}</p>
                  <Link
                    href={`mailto:${site.developerEmail}?subject=ProGlass%20Automation%20—%20sponsorship`}
                    className="mt-5 inline-flex items-center gap-3 rounded-card border border-line-strong/70 bg-ink-950/60 px-4 py-3 font-mono text-sm text-text-primary transition-colors duration-200 hover:border-brand-400/50"
                  >
                    {site.developerEmail}
                    <ArrowRight />
                  </Link>
                  <div className="mt-5 flex flex-wrap gap-3">
                    <ButtonLink href="/contact" size="sm" trailing={<ArrowRight />}>
          {t("Use the contact form")}
        </ButtonLink>
                    <ButtonLink href="/developer" variant="secondary" size="sm">
          {t("Who is building it")}
        </ButtonLink>
                  </div>
                </div>

                <div className="surface p-6">
                  <p className="eyebrow">{t("What is definitely not on offer")}</p>
                  <ul className="mt-3 space-y-2.5">
                    {[
                      "Priority support that jumps the queue for everyone else",
                      "A feature on the roadmap because it was paid for directly",
                      "Exclusivity by market, sector or customer list",
                      "A logo wall that implies customers or partners this product does not have",
                    ].map((line) => (
                      <li key={line} className="flex gap-3 text-sm leading-relaxed text-text-muted">
                        <span aria-hidden="true" className="mt-2 h-1 w-1 shrink-0 rounded-full bg-state-danger/80" />
                        {line}
                      </li>
                    ))}
                  </ul>
                </div>
              </div>
            </Reveal>
          </div>
        </Container>
      </Section>

      {/* ── Questions ────────────────────────────────────────────── */}
      <Section tone="raised" aria-labelledby="sponsor-faq-heading">
        <Container>
          <SectionHeading
            id="sponsor-faq-heading"
            eyebrow={t("Questions")}
            title={t("The awkward ones, answered first.")}
            lead={t("If an answer here is thinner than you would like, that is usually because the honest answer is still being worked out — not because it is being avoided.")}
            align="center"
          />
          <div className="mx-auto mt-10 max-w-3xl">
            <Accordion items={sponsorFaq} defaultOpen={0} />
          </div>
        </Container>
      </Section>

      {/* ── CTA ──────────────────────────────────────────────────── */}
      <Section aria-label={t("Support the project")}>
        <Container>
          <Reveal>
            <div className="plate flex flex-col gap-6 p-8 sm:flex-row sm:items-center sm:justify-between sm:p-10">
              <div>
                <h2 className="text-2xl leading-tight sm:text-3xl">{t("Back the build, or join it.")}</h2>
                <p className="mt-3 max-w-xl text-sm leading-relaxed text-text-muted">{t("Funding, a developer with spare capacity, a cutting table that can be tested against, or a translation that deserves a native speaker — all four move this product forward.")}</p>
              </div>
              <div className="flex shrink-0 flex-wrap gap-3">
                <ButtonLink href={`mailto:${site.developerEmail}?subject=ProGlass%20Automation%20—%20sponsorship`} size="lg" trailing={<ArrowRight />}>
          {t("Email the developer")}
        </ButtonLink>
                <ButtonLink href="/pricing" variant="secondary" size="lg">
          {t("How licensing works")}
        </ButtonLink>
              </div>
            </div>
          </Reveal>
        </Container>
      </Section>
    </>
  );
}

function NeedIcon({ name }: { name: string }) {
  const common = {
    "aria-hidden": true,
    viewBox: "0 0 20 20",
    className: "h-4.5 w-4.5",
    fill: "none",
    stroke: "currentColor",
    strokeWidth: 1.5,
    strokeLinecap: "round" as const,
    strokeLinejoin: "round" as const,
  };

  switch (name) {
    case "code":
      return (
        <svg {...common}>
          <path d="M7 6 3.5 10 7 14M13 6l3.5 4L13 14" />
        </svg>
      );
    case "test":
      return (
        <svg {...common}>
          <path d="M8 3h4M9 3v5.2L5.4 14.8A1.6 1.6 0 0 0 6.8 17h6.4a1.6 1.6 0 0 0 1.4-2.2L11 8.2V3" />
          <path d="M6.8 12h6.4" />
        </svg>
      );
    case "translate":
      return (
        <svg {...common}>
          <path d="M3 4.5h7M6.5 3v1.5M8.6 4.5c-.6 3-2.4 5.2-4.6 6.4" />
          <path d="M5 8.2c1.2 1.7 2.8 2.9 4.6 3.6M13 7.5 10 16.5M17 16.5 14 7.5M11.1 13.4h5.8" />
        </svg>
      );
    case "hardware":
      return (
        <svg {...common}>
          <rect x="2.5" y="3.5" width="15" height="10" rx="1.6" />
          <path d="M7 17h6M10 13.5V17" />
        </svg>
      );
    case "docs":
      return (
        <svg {...common}>
          <path d="M4 3.5h7.5L16 8v8.5H4z" />
          <path d="M11 3.5V8h4.5M7 11h6M7 13.8h4" />
        </svg>
      );
    default:
      return (
        <svg {...common}>
          <circle cx="10" cy="10" r="6.5" />
          <path d="M10 6.5v4l2.6 1.6" />
        </svg>
      );
  }
}

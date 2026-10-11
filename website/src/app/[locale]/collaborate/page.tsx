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
  collabFaq,
  collaboratorRoles,
  dataOwners,
  howItStarts,
  notOnOffer,
  performanceModule,
  whatYouGet,
} from "@/data/collaboration";
import { RouteSchema } from "@/components/seo/RouteSchema";
import { pageMetadata } from "@/lib/metadata";
import { translatorFor } from "@/i18n/server";
import { site } from "@/lib/site";
import { PageOverlay } from "@/i18n/PageOverlay";

type PageProps = { params: Promise<{ locale: string }> };


/*
 * The six collaboration roles, each pitched by the thing the role holds: the
 * fabricator holds plates, the investor holds the banknote, hardware is a
 * laptop silhouette, the engineer is the developer's `</>`, the translator
 * is two scripts side by side, and the operator is the shop's truck.
 * Same 16-unit grid and one-hand stroke as the rest of the site.
 */
const roleIcons: Record<string, string> = {
  fabricator: "M2.8 2.8h7v7h-7zM9.8 6.2h3.4v3.4M9.8 9.6h3.4v3.4M2.8 9.8h7v3.4",
  investor: "M2.5 5.5h11v6h-11zM8 7.3a1.9 1.9 0 1 0 0 3.8 1.9 1.9 0 0 0 0-3.8z",
  hardware: "M3 4h10v6H3zM2 12h12M6 10v2M10 10v2",
  engineer: "M6 4 2.5 8 6 12M10 4l3.5 4L10 12M8.8 3.5 7.2 12.5",
  translator: "M3 3.5h9v3H8.5M6.5 3.5V2.5M9 6.5c-1 2-3.2 3.8-5.5 4.4M4 7.2c.8 1.8 3 3.6 5 4.4M13 9.5h2.5M14.2 9.5c-.3 2-1.6 3.6-3.4 4.4",
  operator: "M2 4.5h8v7H2zM10 7h2.3l1.7 2.3v2.2h-4zM4.7 13.2a1.3 1.3 0 1 0 0-2.6 1.3 1.3 0 0 0 0 2.6zM11.4 13.2a1.3 1.3 0 1 0 0-2.6 1.3 1.3 0 0 0 0 2.6z",
};

export async function generateMetadata({ params }: PageProps): Promise<Metadata> {
  const t = await translatorFor(params);
  return pageMetadata({
    locale: t.locale,
    title: t("Work with ProGlass"),
    description: t(
      "ProGlass Automation needs a data owner, a machine and a second pair of hands: fabricators, backers, hardware providers and collaborators.",
    ),
    path: "/collaborate",
    keywords: [
      t("glass software partnership"),
      t("glass performance data calculator"),
      t("U-value calculation software"),
      t("glass fabrication software collaboration"),
      t("invest in glass software"),
    ],
    ogEyebrow: t("Collaboration"),
  });
}

/**
 * The collaboration page.
 *
 * The developer page answers "who wrote this"; the sponsorship page answers
 * "what is missing"; this one answers "what would a partner do about it". It is
 * separate on purpose: a fabricator weighing whether to hand over its
 * performance data, and a technician looking for a WPF project to help with,
 * are not looking for the same page, and burying the first inside a funding
 * appeal costs the conversation that matters most.
 *
 * Every claim on this page is either a fact about the application, a statement
 * the author has made about his own situation, or an intention clearly labelled
 * as one. No partnership is asserted and no return is promised — both rules are
 * written onto the page rather than kept in a comment.
 */
export default async function CollaboratePage({ params }: PageProps) {
  const t = await translatorFor(params);
  const locale = t.locale;

  return (
    <PageOverlay locale={t.locale} route="collaborate">
    <>
      {/* Structured data for this route — see lib/schema.ts. */}
      <RouteSchema
        locale={locale}
        path="/collaborate"
        title={t("Work with ProGlass")}
        description={t(
          "ProGlass Automation needs a data owner, a machine and a second pair of hands: fabricators, backers, hardware providers and collaborators.",
        )}
      />

      <PageHero
        t={t}
        glyph={footerIconFor("/collaborate")!}
        eyebrow={t("Collaboration")}
        title={
          <>
            {t("The software is one person.")}
            <span className="block text-brand-300">{t("The next stage is not.")}</span>
          </>
        }
        lead={t(
          "ProGlass Automation is written and supported by one glass estimator, on borrowed machines, in the hours around a full-time job. It is in use on the commercial side and it is not finished. This page is the list of people who would change that — a fabricator with performance data, a backer, a machine, a second pair of hands — and what each of them would actually get.",
        )}
        banner="people"
        breadcrumb={[
          { label: t("Home"), href: "/" },
          { label: t("Collaborate"), href: "/collaborate" },
        ]}
        meta={[
          { label: t("Open roles"), value: `${collaboratorRoles.length}` },
          { label: t("Partners signed"), value: t("None yet") },
          { label: t("Maintainers"), value: t("One") },
          { label: t("Contact"), value: site.developerEmail },
        ]}
        actions={
          <>
            <ButtonLink href={`mailto:${site.developerEmail}?subject=ProGlass%20Automation%20—%20collaboration`} size="lg" trailing={<ArrowRight />}>
              {t("Email the developer")}
            </ButtonLink>
            <ButtonLink href="/developer" variant="secondary" size="lg">
              {t("Who is behind it")}
            </ButtonLink>
          </>
        }
      />

      {/* ── The reason, stated first ─────────────────────────────────── */}
      <Section aria-labelledby="why-heading">
        <Container>
          <div className="grid gap-10 lg:grid-cols-[minmax(0,1.05fr)_minmax(0,0.95fr)] lg:gap-16">
            <div>
              <SectionHeading
                id="why-heading"
                eyebrow={t("Why this page exists")}
                title={t("There is more software here than there are hours to finish it.")}
                lead={t(
                  "The application carries a job from inquiry to statement, with a guillotine optimizer and eight production stages behind it, and it was written by someone whose day job is estimating glass. What it does not have is a second contributor, a machine of its own, or a data owner for the one module every fabricator's customer asks about.",
                )}
              />
              <div className="mt-8 space-y-4 text-sm leading-relaxed text-text-muted">
                <p>
                  {t(
                    "That is the whole situation, and there is nothing to dress up. The product is real and its records are dated — audits, remediation plans, engine analyses, all of them at the root of the repository and all of them readable. What has never existed is anybody else in the room.",
                  )}
                </p>
                <p className="text-text-secondary">
                  {t(
                    "If you make glass, fund small industrial software, supply machines, translate, test, or simply run a shop that does the job this is for — there is a slot below written for you specifically, with what it asks for and what it returns.",
                  )}
                </p>
              </div>
            </div>

            <Reveal delay={80} className="flow">
              <div className="glass-primary p-6 sm:p-7">
                <p className="eyebrow">{t("What would change the trajectory")}</p>
                <ul className="mt-4 space-y-3.5">
                  {[
                    { value: t("Hours"), body: t("A funded day a week moves the roadmap faster than a year of evenings.") },
                    { value: t("A machine"), body: t("A development PC and a second machine to test on. Both are borrowed or shared today.") },
                    { value: t("A data set"), body: t("One fabricator's published performance figures turn a described module into a working one.") },
                    { value: t("A second person"), body: t("One engineer, translator or operator who answers once, properly.") },
                  ].map((row) => (
                    <li key={row.value} className="flex gap-3.5">
                      <span aria-hidden="true" className="mt-2 h-1 w-1 shrink-0 rounded-full bg-brand-400/80" />
                      <p className="text-sm leading-relaxed text-text-muted">
                        <span className="font-semibold text-text-secondary">{row.value}</span> — {row.body}
                      </p>
                    </li>
                  ))}
                </ul>
              </div>

              <p className="text-xs font-medium leading-relaxed text-text-faint">
                {t(
                  "The rule this page keeps: a partner is named here only after something is agreed in writing. Until then this page states what is being asked for, and nothing more.",
                )}
              </p>
            </Reveal>
          </div>
        </Container>
      </Section>

      {/* ── The performance-data calculator ──────────────────────────── */}
      <Section tone="raised" id="performance" aria-labelledby="performance-heading">
        <Container>
          <div className="glass-primary card-pad">
            <div className="glass-primary-head">
              <div className="min-w-0">
                <p className="eyebrow">{t("Industry partnership")}</p>
                <h2 id="performance-heading" className="display-2 mt-3">
                  {t("Your performance data, inside the quotation.")}
                </h2>
              </div>
              <Badge tone="warning">{t("Not built yet")}</Badge>
            </div>

            <div className="grid gap-8 pt-6 lg:grid-cols-[minmax(0,1fr)_minmax(0,1fr)] lg:gap-12">
              <div>
                <p className="text-sm leading-relaxed text-text-secondary">
                  {t(
                    "{v1} is the one figure an estimator still leaves the software to find: the U-value, solar factor and light transmission of the exact build-up being quoted. Everything else in that build-up is already here.",
                    { v1: t(performanceModule.name) },
                  )}
                </p>
                <ul className="mt-5 space-y-3.5 border-t border-line/60 pt-5">
                  {performanceModule.why.map((line) => (
                    <li key={t(line)} className="flex gap-3 text-sm leading-relaxed text-text-muted">
                      <span aria-hidden="true" className="mt-2 h-1 w-1 shrink-0 rounded-full bg-brand-400/70" />
                      {t(line)}
                    </li>
                  ))}
                </ul>

                <dl className="mt-6 grid gap-4 border-t border-line/60 pt-5 sm:grid-cols-2">
                  <div>
                    <dt className="mono-label">{t("Status")}</dt>
                    <dd className="mt-2 text-sm font-semibold text-state-warning">{t(performanceModule.status)}</dd>
                  </div>
                  <div>
                    <dt className="mono-label">{t("First step")}</dt>
                    <dd className="mt-2 text-sm leading-relaxed text-text-muted">
                      {t("One data set, from one manufacturer, in whatever form it already exists.")}
                    </dd>
                  </div>
                </dl>
              </div>

              <div className="grid gap-4 sm:grid-cols-1">
                <div className="surface-raised p-5">
                  <p className="mono-label">{t("What the fabricator provides")}</p>
                  <ul className="mt-3 space-y-2.5">
                    {performanceModule.fromPartner.map((line) => (
                      <li key={t(line)} className="flex gap-3 text-sm leading-relaxed text-text-muted">
                        <span aria-hidden="true" className="mt-1.5 shrink-0 text-brand-300">+</span>
                        {t(line)}
                      </li>
                    ))}
                  </ul>
                </div>
                <div className="surface-raised p-5">
                  <p className="mono-label">{t("What ProGlass does with it")}</p>
                  <ul className="mt-3 space-y-2.5">
                    {performanceModule.fromProGlass.map((line) => (
                      <li key={t(line)} className="flex gap-3 text-sm leading-relaxed text-text-muted">
                        <span aria-hidden="true" className="mt-1.5 shrink-0 text-brand-300">+</span>
                        {t(line)}
                      </li>
                    ))}
                  </ul>
                </div>
              </div>
            </div>

            <p className="mono-label mt-7 border-t border-line/60 pt-5">
              {performanceModule.rules.map((rule) => t(rule)).join(" · ")}
            </p>
          </div>

          <Reveal delay={80} className="mt-6 grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
            {dataOwners.map((owner) => (
              <div key={owner.name} className="surface p-5">
                <p className="font-mono text-2xs uppercase tracking-[0.14em] text-text-faint font-medium">
                  {t("Data owner")}
                </p>
                <h3 className="card-title mt-2">{owner.name}</h3>
                <p className="mt-2 text-sm leading-relaxed text-text-muted">{t(owner.note)}</p>
              </div>
            ))}
          </Reveal>

          <Reveal delay={120}>
            <p className="mt-6 max-w-4xl text-xs font-medium leading-relaxed text-text-faint">
              <span className="font-mono uppercase tracking-[0.14em] text-state-warning">
                {t("These names are not partners.")}{" "}
              </span>
              {t(
                "They are named because their published figures are what an estimator working on a Gulf specification reaches for, and the module is designed to hold data from any manufacturer. None of these companies has been contacted, none is involved in this project, and none has agreed to anything. If that changes, the agreement is what this page will say — and it will say it in writing, with the date.",
              )}
            </p>
          </Reveal>
        </Container>
      </Section>

      {/* ── Six kinds of partner ─────────────────────────────────────── */}
      <Section aria-labelledby="roles-heading">
        <Container>
          <SectionHeading
            id="roles-heading"
            eyebrow={t("Who this is for")}
            title={t("Six ways in, none of them a donation.")}
            lead={t(
              "Every entry names what the person brings and what comes back. Two need no money at all, and one needs half an hour of somebody's working day.",
            )}
            align="center"
          />

          <ul className="mt-12 grid gap-5 md:grid-cols-2 xl:grid-cols-3">
            {collaboratorRoles.map((role, index) => (
              <Reveal key={role.id} as="li" delay={Math.min(index, 5) * 55} id={role.id}>
                <article className="surface-raised card-interactive flex h-full flex-col p-6">
                  <div className="flex items-center gap-3">
                    <IconChip path={roleIcons[role.id] ?? roleIcons.fabricator!} className="h-8 w-8" />
                    <h3 className="card-title">{t(role.title)}</h3>
                  </div>
                  <p className="mt-4 text-sm leading-relaxed text-text-secondary">{t(role.who)}</p>

                  <dl className="mt-5 flex-1 space-y-3.5 border-t border-line/60 pt-5">
                    <div>
                      <dt className="mono-label">{t("What you bring")}</dt>
                      <dd className="mt-1.5 text-sm leading-relaxed text-text-muted">{t(role.brings)}</dd>
                    </div>
                    <div>
                      <dt className="mono-label">{t("What comes back")}</dt>
                      <dd className="mt-1.5 text-sm leading-relaxed text-text-muted">{t(role.gets)}</dd>
                    </div>
                  </dl>
                </article>
              </Reveal>
            ))}
          </ul>

          <Reveal delay={140} className="mt-8">
            <p className="text-sm leading-relaxed text-text-muted">
              {t(
                "The eight specific gaps behind these roles — test engineering, a cutting table to test against, translation review, documentation, running costs — are listed with what each one would change on the",
              )}{" "}
              <Link href="/sponsors" className="link-underline text-brand-200">
                {t("sponsorship page")}
              </Link>
              .
            </p>
          </Reveal>
        </Container>
      </Section>

      {/* ── What a partner gets, and what nobody gets ────────────────── */}
      <Section tone="sunken" aria-labelledby="terms-heading">
        <Container>
          <SectionHeading
            id="terms-heading"
            eyebrow={t("The terms in advance")}
            title={t("What is on offer, and what is not.")}
            lead={t(
              "Written before anybody writes an email, so the first conversation can start at the second question rather than the first.",
            )}
          />

          <div className="mt-10 grid gap-6 lg:grid-cols-[minmax(0,1.15fr)_minmax(0,1fr)] lg:gap-12">
            <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-1 xl:grid-cols-2">
              {whatYouGet.map((item, index) => (
                <Reveal key={t(item.title)} delay={index * 55}>
                  <div className="surface card-pad h-full">
                    <h3 className="card-title">{t(item.title)}</h3>
                    <p className="mt-2.5 text-sm leading-relaxed text-text-muted">{t(item.body)}</p>
                  </div>
                </Reveal>
              ))}
            </div>

            <Reveal delay={120}>
              <div className="card-accent card-pad h-full">
                <p className="eyebrow">{t("What is definitely not on offer")}</p>
                <ul className="mt-4 space-y-3">
                  {notOnOffer.map((line) => (
                    <li key={t(line)} className="flex gap-3 text-sm leading-relaxed text-text-muted">
                      <span aria-hidden="true" className="mt-2 h-1 w-1 shrink-0 rounded-full bg-state-danger/80" />
                      {t(line)}
                    </li>
                  ))}
                </ul>
                <p className="mt-5 border-t border-line/60 pt-4 text-xs font-medium leading-relaxed text-text-faint">
                  {t(
                    "The same rule the sponsorship page keeps: help funds work. It does not buy a claim on this website that the software cannot demonstrate.",
                  )}
                </p>
              </div>
            </Reveal>
          </div>
        </Container>
      </Section>

      {/* ── How it starts ────────────────────────────────────────────── */}
      <Section aria-labelledby="start-heading">
        <Container>
          <div className="grid gap-10 lg:grid-cols-[minmax(0,0.9fr)_minmax(0,1.3fr)] lg:gap-16">
            <div className="lg:sticky lg:top-28 lg:self-start">
              <SectionHeading
                id="start-heading"
                eyebrow={t("How it starts")}
                title={t("Four steps, and the first one is an email.")}
                lead={t(
                  "There is no application form and no portal. The reply comes from the person who writes the code, and it will be honest about whether the fit is right — including when it is not.",
                )}
              />
              <div className="mt-8 flex flex-wrap gap-3">
                <ButtonLink
                  href={`mailto:${site.developerEmail}?subject=ProGlass%20Automation%20—%20collaboration`}
                  size="lg"
                  trailing={<ArrowRight />}
                >
                  {t("Start the conversation")}
                </ButtonLink>
                <ButtonLink href="/contact" variant="secondary" size="lg">
                  {t("Use the contact form")}
                </ButtonLink>
              </div>
            </div>

            <ol className="flow">
              {howItStarts.map((item, index) => (
                <Reveal key={item.step} as="li" delay={index * 60}>
                  <div className="plate flex gap-4 p-5">
                    <IndexChip>{item.step}</IndexChip>
                    <div className="min-w-0">
                      <h3 className="card-title">{t(item.title)}</h3>
                      <p className="mt-2 text-sm leading-relaxed text-text-muted">{t(item.body)}</p>
                    </div>
                  </div>
                </Reveal>
              ))}
            </ol>
          </div>
        </Container>
      </Section>

      {/* ── Questions ────────────────────────────────────────────────── */}
      <Section tone="raised" aria-labelledby="collab-faq-heading">
        <Container>
          <SectionHeading
            id="collab-faq-heading"
            eyebrow={t("Questions")}
            title={t("What a partner asks before writing.")}
          />
          <Reveal className="mt-8">
            <Accordion items={collabFaq} />
          </Reveal>
        </Container>
      </Section>

      {/* ── Closing ──────────────────────────────────────────────────── */}
      <Section aria-labelledby="collab-cta-heading">
        <Container>
          <div className="glass-primary flex flex-col items-center gap-6 p-8 text-center sm:p-10">
            <div>
              <h2 id="collab-cta-heading" className="display-2">
                {t("One conversation is the whole ask.")}
              </h2>
              <p className="mx-auto mt-3 max-w-xl text-sm leading-relaxed text-text-muted">
                {t(
                  "A machine, a data set, a funded day a week or an hour of an operator's time — any one of them moves this further than another year of evenings. Write in plain words and the answer will be plain words back.",
                )}
              </p>
            </div>
            <div className="flex flex-wrap justify-center gap-3">
              <ButtonLink
                href={`mailto:${site.developerEmail}?subject=ProGlass%20Automation%20—%20collaboration`}
                size="lg"
                trailing={<ArrowRight />}
              >
                {t("Email the developer")}
              </ButtonLink>
              <ButtonLink href="/sponsors" variant="secondary" size="lg">
                {t("Support the build")}
              </ButtonLink>
            </div>
          </div>
        </Container>
      </Section>
    </>
    </PageOverlay>
  );
}

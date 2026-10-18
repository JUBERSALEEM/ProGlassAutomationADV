import type { Metadata } from "next";
import { LocaleLink as Link } from "@/components/ui/LocaleLink";
import { Container, Section } from "@/components/ui/Container";
import { PageHero } from "@/components/layout/PageHero";
import { footerIconFor } from "@/data/footerIcons";
import { ArrowRight, ButtonLink } from "@/components/ui/Button";
import { Badge } from "@/components/ui/Badge";
import { Reveal } from "@/components/ui/Reveal";
import { CallToActionCard } from "@/components/ui/CallToActionCard";
import { SectionHeading } from "@/components/ui/SectionHeading";
import { Accordion } from "@/components/ui/Accordion";
import { editions, licenceFacts, licenceGates, openModules } from "@/data/editions";
import { checkoutPath, durationById, formatPrice, isSkuBuyable, licenseDurations, pricedSkus } from "@/data/plans";
import { site } from "@/lib/site";
import { editionIcon } from "@/data/groupIcons";
import { Icon } from "@/components/ui/Icon";
import { faq } from "@/data/faq";
import { RouteSchema } from "@/components/seo/RouteSchema";
import { pageMetadata } from "@/lib/metadata";
import { translatorFor } from "@/i18n/server";
import { PageOverlay } from "@/i18n/PageOverlay";

type PageProps = { params: Promise<{ locale: string }> };

export async function generateMetadata({ params }: PageProps): Promise<Metadata> {
  const t = await translatorFor(params);
  return pageMetadata({
  locale: t.locale,
  title: t("Pricing and licensing editions"),
  description: t("ProGlass Automation editions — Trial, Basic, Business, Factory and Enterprise: which modules each tier unlocks, and how offline licensing works."),
  path: "/pricing",
  keywords: ["glass software pricing", t("glass ERP licence"), t("ProGlass Automation editions"), "glass software licence"],
  ogEyebrow: t("Editions"),
  });
}

export default async function PricingPage({ params }: PageProps) {
  const t = await translatorFor(params);
  const locale = t.locale;
  const priced = pricedSkus();
  return (
    <PageOverlay locale={t.locale} route="pricing">
    <>
    {/* Structured data for this route — see lib/schema.ts. */}
    <RouteSchema
      locale={locale}
      path="/pricing"
      title={t("Pricing and licensing editions")}
      description={t("ProGlass Automation editions — Trial, Basic, Business, Factory and Enterprise: which modules each tier unlocks, and how offline licensing works.")}
    />
      <PageHero
        variant="showcase"
      t={t}
        glyph={footerIconFor("/pricing")!}
        eyebrow={t("Pricing")}
        title={t("Editions, not subscriptions.")}
        lead={t("ProGlass Automation is licensed per business with a signed licence file activated offline. The editions below are the ones the application itself defines, and the feature gating is the same map the navigation rail uses — so what this page says and what the application does cannot drift apart.")}
        banner="licence"
        breadcrumb={[
          { label: t("Home"), href: "/" },
          { label: t("Pricing"), href: "/pricing" },
        ]}
        meta={[
          { label: t("Editions"), value: t("5 (Trial → Enterprise)") },
          { label: t("Trial"), value: t("30 days, all features") },
          { label: t("Activation"), value: t("Offline licence file") },
          { label: t("Pricing"), value: priced.length > 0 ? t("Published below · AED") : t("Quoted per business") },
        ]}
        actions={
          <>
            <ButtonLink href="/contact" size="lg" trailing={<ArrowRight />}>
          {t("Request pricing")}
        </ButtonLink>
            <ButtonLink href="/contact" variant="secondary" size="lg">
          {t("Request a Demo")}
        </ButtonLink>
          </>
        }
      />

      {/* ── Why there is no price on this page ───────────────────── */}
      <Section aria-label={t("How pricing works")}>
        <Container>
          <Reveal>
            <div className="surface-raised grid gap-5 sm:grid-cols-3 card-pad">
              <div className="sm:col-span-2">
                <h2 className="display-3">{priced.length > 0 ? t("Standard licences are priced right here.") : t("There is no price on this page, and that is deliberate.")}</h2>
                <p className="mt-4 text-sm leading-relaxed text-text-muted">{priced.length > 0 ? t("Basic, Business and Factory are published below in AED, excluding 5% VAT — the number you see is the licence price for that edition and term. Multi-site deployments and custom integration work (Enterprise) are still quoted per business, because no two of those have the same shape.") : t("ProGlass is sold to glass businesses with different shapes: a single estimator, a five-person office, or a factory running eight production stages. What you need ranges from the proforma wizard alone to the full chain plus the cutting-optimization engine. Quoting one number for all of those would mean either overcharging the first or underdelivering the last.")}</p>
                <p className="mt-3 text-sm leading-relaxed text-text-muted">{t("Tell us how many people will use it, which modules matter, and whether cutting optimization is part of your day. You get a written quote for that scope.")}</p>
                <div className="mt-6 flex flex-wrap gap-3">
                  <ButtonLink href="/contact" size="md" trailing={<ArrowRight />}>
          {t("Request pricing")}
        </ButtonLink>
                  <ButtonLink href="/contact" variant="secondary" size="md">
          {t("Request a Demo")}
        </ButtonLink>
                </div>
              </div>
              <ul className="space-y-3 sm:border-l sm:border-line/60 sm:pl-6">
                {licenceFacts.map((fact) => (
                  <li key={fact} className="flex gap-3 text-sm leading-relaxed text-text-muted">
                    <span aria-hidden="true" className="mt-2 h-1 w-1 shrink-0 rounded-full bg-brand-400/80" />
                    {fact}
                  </li>
                ))}
              </ul>
            </div>
          </Reveal>
          {/*
            The same facts, with the code that enforces them — the licence
            facts above are claims until a reader can open the file that makes
            them true. Paths are printed, not translated, like every other
            evidence line on the site.
          */}
          <p className="mono-label mt-5">
            {t("Enforced in code:")} Licensing/Contracts/LicenseService.cs ·
            Licensing/Contracts/OfflineActivationService.cs ·
            Licensing/Contracts/LicenseKeyArmor.cs ·
            Licensing/ProGlass.Licensing.Tests/
          </p>
        </Container>
      </Section>

      {/* ── Editions ─────────────────────────────────────────────── */}
      <Section tone="raised" aria-labelledby="editions-heading">
        <Container>
          <SectionHeading
            id="editions-heading"
            eyebrow={t("Editions")}
            title={t("Five tiers, in the order the application compares them.")}
            lead={t("The application compares editions by order, not by name, so a stored licence keeps its meaning as new tiers are appended. The descriptions below are the definitions written on the edition enum itself.")}
          />

          <div className="mt-8 grid gap-5 md:grid-cols-2 lg:grid-cols-4">
            {editions.map((edition, index) => (
              <Reveal
                key={edition.id}
                delay={index * 60}
                className={
                  edition.featured
                    ? "pricing-card--featured surface-raised relative flex flex-col border-brand-400/40 card-pad-sm shadow-glow"
                    : "pricing-card--basic surface relative flex flex-col card-pad-sm"
                }
              >
                {edition.featured ? (
                  <span className="absolute -top-3 left-6">
                    <Badge tone="accent">{t("Includes the optimizer")}</Badge>
                  </span>
                ) : null}

                <div className="flex items-baseline justify-between gap-3">
                  <h3 className="flex items-center gap-2.5 text-lg font-semibold text-text-primary">
                    <span className="grid h-8 w-8 place-items-center rounded-card border border-line/60 bg-ink-900/50 text-brand-300">
                      <Icon path={editionIcon(edition.id)} className="h-4 w-4" />
                    </span>
                    {t(edition.name)}
                  </h3>
                  <span className="font-mono text-2xs uppercase tracking-[0.14em] text-text-faint font-medium">
                    Tier {edition.order}
                  </span>
                </div>
                <p className="mt-1 font-mono text-2xs uppercase tracking-[0.14em] text-brand-300">
                  {t(edition.positioning)}
                </p>

                <p className="mt-4 rounded-card border border-line/60 bg-ink-950/50 p-3.5 text-sm leading-relaxed text-text-secondary">
                  {t(edition.definition)}
                </p>

                <ul className="mt-5 flex-1 space-y-2.5 border-t border-line/60 pt-5">
                  {edition.includes.map((item) => (
                    <li key={item} className="flex gap-2.5 text-sm leading-relaxed text-text-muted">
                      <span aria-hidden="true" className="mt-2 h-1 w-1 shrink-0 rounded-full bg-brand-400/70" />
                      {item}
                    </li>
                  ))}
                </ul>

                <ButtonLink
                  href="/contact"
                  variant={edition.featured ? "primary" : "secondary"}
                  size="sm"
                  className="mt-6"
                  trailing={<ArrowRight />}
                >
                  Ask about {t(edition.name)}
                </ButtonLink>
              </Reveal>
            ))}
          </div>
        </Container>
      </Section>

      {/* ── Licence terms ────────────────────────────────────────── */}
      <Section aria-labelledby="terms-heading">
        <Container>
          <SectionHeading
            id="terms-heading"
            eyebrow={t("Licence terms")}
            title={t("Five terms, from a week to two years.")}
            lead={t("Every edition is sold for a term. These are the durations in the licence plan catalogue the application itself carries — the same names the licence generator offers and the application checks when a key is pasted in. Activation is offline: a signed licence file, no phone-home, and the term is verified against the catalogue on both ends.")}
          />

          <div className="mt-8 grid gap-5 sm:grid-cols-2 lg:grid-cols-5">
            {licenseDurations.map((duration, index) => (
              <Reveal
                key={duration.id}
                delay={index * 50}
                className={
                  duration.featured
                    ? "surface-raised relative flex flex-col border-brand-400/40 card-pad-sm shadow-glow"
                    : "surface relative flex flex-col card-pad-sm"
                }
              >
                {duration.featured ? (
                  <span className="absolute -top-3 left-5">
                    <Badge tone="accent">{t("Standard term")}</Badge>
                  </span>
                ) : null}
                <p className="text-lg font-semibold text-text-primary">{t(duration.name)}</p>
                <p className="mt-1 font-mono text-2xs uppercase tracking-[0.14em] text-brand-300">
                  {duration.days} {t("days")}
                </p>
                <p className="mt-3 text-sm leading-relaxed text-text-muted">{t(duration.note)}</p>
              </Reveal>
            ))}
          </div>

          {/*
            Priced SKUs render whenever src/data/plans.ts publishes a
            number. Each row's button follows the money path that exists
            for that SKU today: hosted checkout once it carries an https
            payment link, otherwise WhatsApp — where GCC trade buyers
            already are — with edition, term and price pre-written.
          */}
          {priced.length > 0 ? (
            <>
              <p className="mt-8 text-sm leading-relaxed text-text-muted">
                {t("Published licence prices — AED, excluding 5% VAT. Until card checkout opens, buying runs through WhatsApp: bank transfer or a card link, then your signed licence key arrives by email.")}
              </p>
              <ul className="mt-4 space-y-3">
                {priced.map((sku) => {
                  const edition = editions.find((entry) => entry.id === sku.editionId);
                  const duration = durationById(sku.durationId);
                  if (!edition || !duration || !sku.price) return null;
                  const months = duration.days >= 365 ? (duration.days / 365) * 12 : duration.days === 182 ? 6 : 0;
                  const monthly = months > 0 ? Math.round(sku.price.amount / months) : null;
                  const buyable = isSkuBuyable(sku);
                  const waText = `Hello, I want to buy ProGlass Automation — ${edition.name} · ${duration.name} (${formatPrice(sku.price)}). Please share payment details.`;
                  return (
                    <li
                      key={sku.id}
                      className="surface-raised flex flex-wrap items-center justify-between gap-4 card-pad-sm"
                    >
                      <div>
                        <p className="text-base font-semibold text-text-primary">
                          {t(edition.name)} · {t(duration.name)}
                        </p>
                        <p className="mt-1 text-sm text-text-muted">
                          {formatPrice(sku.price)}
                          {monthly ? (
                            <span>
                              {" "}· ≈ {formatPrice({ amount: monthly, currency: sku.price.currency })}/{t("month")}
                            </span>
                          ) : null}
                        </p>
                      </div>
                      <ButtonLink
                        href={buyable ? checkoutPath(sku.id) : `https://wa.me/${site.whatsappNumber}?text=${encodeURIComponent(waText)}`}
                        external
                        size="md"
                        trailing={<ArrowRight />}
                      >
                        {buyable ? t("Buy licence") : t("Buy on WhatsApp")}
                      </ButtonLink>
                    </li>
                  );
                })}
              </ul>
            </>
          ) : (
            <p className="mt-6 text-sm leading-relaxed text-text-muted">
              {t("Online checkout opens when prices are published on this page. Until then, every term above is quoted per business —")}{" "}
              <Link href="/contact" className="link-underline text-brand-200">
                {t("request pricing")}
              </Link>{" "}
              {t("and a person answers.")}
            </p>
          )}
        </Container>
      </Section>

      {/* ── Gating map ───────────────────────────────────────────── */}
      <Section aria-labelledby="gating-heading">
        <Container>
          <SectionHeading
            id="gating-heading"
            eyebrow={t("Feature gating")}
            title={t("Exactly what each edition unlocks.")}
            lead={t("This is the same map the navigation rail consults. A module that is not in this table is open in every edition, and that is written down explicitly rather than left implied.")}
          />

          <div className="mt-8 grid gap-5 lg:grid-cols-2">
            <div className="flow">
              {licenceGates.map((gate) => (
                <Reveal key={t(gate.group)} className="surface-raised card-pad-sm">
                  <div className="flex flex-wrap items-center justify-between gap-3">
                    <h3 className="text-base font-semibold text-text-primary">{t(gate.group)}</h3>
                    <Badge tone="accent">{gate.minimumEdition} and above</Badge>
                  </div>
                  <p className="mt-2 font-mono text-xs text-text-faint font-medium">requires {gate.requiredFeature}</p>
                  <ul className="mt-4 flex flex-wrap gap-2">
                    {gate.modules.map((module) => (
                      <li
                        key={module}
                        className="rounded-lg border border-line-strong/70 bg-ink-900/60 px-2.5 py-1 text-xs text-text-secondary"
                      >
                        {t(module)}
                      </li>
                    ))}
                  </ul>
                </Reveal>
              ))}
            </div>

            <Reveal delay={80} className="surface card-pad-sm">
              <div className="flex flex-wrap items-center justify-between gap-3">
                <h3 className="text-base font-semibold text-text-primary">{t("Open in every edition")}</h3>
                <Badge tone="success">{t("No gate")}</Badge>
              </div>
              <p className="mt-3 text-sm leading-relaxed text-text-muted">{t("These modules are explicitly listed as ungated, so a missing entry can never be mistaken for a deliberate free one. A test fails the build if a new module ships without somebody deciding either way.")}</p>
              <ul className="mt-5 flex flex-wrap gap-2">
                {openModules.map((module) => (
                  <li
                    key={module}
                    className="rounded-lg border border-line/60 bg-ink-950/50 px-2.5 py-1 text-xs text-text-muted"
                  >
                    {module}
                  </li>
                ))}
              </ul>
            </Reveal>
          </div>

          <Reveal delay={120} className="mt-6">
            <div className="surface flex flex-col gap-3 border-brand-400/25 bg-brand-400/[0.04] p-5 sm:flex-row sm:items-start sm:gap-4">
              <Badge tone="accent" className="shrink-0">
          {t("Trial")}
        </Badge>
              <p className="text-sm leading-relaxed text-text-muted">{t("During an active 30-day evaluation trial, every feature set is available — including the production stages and the optimization engine. When the trial ends, the normal edition restrictions apply and the application moves to read-only rather than locking you away from your records.")}</p>
            </div>
          </Reveal>
        </Container>
      </Section>

      {/* ── Deployment ───────────────────────────────────────────── */}
      <Section tone="sunken" aria-labelledby="deployment-heading">
        <Container>
          <SectionHeading
            id="deployment-heading"
            eyebrow={t("What you need")}
            title={t("One Windows PC to start.")}
            lead={t("No server, no database administrator and no browser. A small office can point several workstations at a shared database file on the local network.")}
          />

          <div className="dense-card-grid mt-8 grid gap-5 grid-cols-2 sm:grid-cols-4">
            {[
              { title: t("Operating system"), detail: t("Windows 10 or Windows 11, 64-bit.") },
              { title: t("Runtime"), detail: t(".NET 8 desktop runtime, installed with the application.") },
              { title: t("Storage"), detail: t("Local SQLite database files in your user profile, plus a licence file.") },
              { title: t("Network"), detail: t("Not required to run. Needed only for updates and any cloud AI provider you choose to configure.") },
            ].map((item, index) => (
              <Reveal key={`${t(item.title)}-${index}`} delay={index * 50} className="surface p-5">
                <p className="text-sm font-semibold text-text-primary">{t(item.title)}</p>
                <p className="mt-2 text-sm leading-relaxed text-text-muted">{t(item.detail)}</p>
              </Reveal>
            ))}
          </div>
        </Container>
      </Section>

      {/* ── FAQ ──────────────────────────────────────────────────── */}
      <Section aria-label={t("Licensing questions")}>
        <Container>
          <SectionHeading
            eyebrow={t("Questions")}
            title={t("Licensing, deployment and data.")}
            lead={t("The answers below are limited to what the application actually implements.")}
            align="center"
          />
          <div className="mx-auto mt-8 max-w-3xl">
            <Accordion
              items={faq
                .filter((item) => item.group !== "Product")
                .map(({ question, answer }) => ({ question, answer }))}
              defaultOpen={0}
            />
          </div>
        </Container>
      </Section>

      {/* ── Worked example ───────────────────────────────────────── */}
      <Section tone="raised" aria-label={t("Worked example")}>
        <Container>
          <Reveal>
            <div className="surface flex flex-col gap-5 sm:flex-row sm:items-center sm:justify-between card-pad">
              <div>
                <p className="eyebrow">{t("Before you ask for a number")}</p>
                <p className="mt-3 max-w-3xl text-sm leading-relaxed text-text-secondary">
{t("Editions are priced by what a business needs, so there is nothing to publish as a list. What can be shown is a job: the")}{" "}
                  <Link href="/worked-example" className="link-underline text-brand-200">
                    worked example
                  </Link>{" "}
                  {t("runs one seeded job end to end — 22 quotation lines, a partial delivery and the tax invoice raised from it — so you can see what an edition actually carries before deciding which one fits.")}
                </p>
              </div>
              <ButtonLink href="/worked-example" variant="secondary" size="sm" trailing={<ArrowRight />}>
          {t("See the job")}
        </ButtonLink>
            </div>
          </Reveal>
        </Container>
      </Section>

      {/* ── CTA ──────────────────────────────────────────────────── */}
      <Section tone="raised" aria-label={t("Request pricing")}>
        <Container>
          <Reveal>
            <CallToActionCard>
              <div>
                <h2 className="display-2">{t("Tell us what you need, and get a number.")}</h2>
                <p className="mx-auto mt-3 max-w-xl text-sm leading-relaxed text-text-muted">{t("How many people, which modules, and whether the cutting table is part of it. Every request is answered by a person.")}</p>
              </div>
              <div className="flex flex-wrap justify-center gap-3">
                <ButtonLink href="/contact" size="lg" trailing={<ArrowRight />}>
          {t("Request pricing")}
        </ButtonLink>
                <ButtonLink href="/features" variant="secondary" size="lg">
          {t("What is included")}
        </ButtonLink>
              </div>
            </CallToActionCard>
          </Reveal>
        </Container>
      </Section>
    </>
    </PageOverlay>
  );
}

import type { Metadata } from "next";
import { Container, Section } from "@/components/ui/Container";
import { PageHero } from "@/components/layout/PageHero";
import { footerIconFor } from "@/data/footerIcons";
import { ArrowRight, ButtonLink } from "@/components/ui/Button";
import { Reveal } from "@/components/ui/Reveal";
import { SectionHeading } from "@/components/ui/SectionHeading";
import { DemoRequestForm } from "@/components/forms/DemoRequestForm";
import { site } from "@/lib/site";
import { RouteSchema } from "@/components/seo/RouteSchema";
import { pageMetadata } from "@/lib/metadata";
import { translatorFor } from "@/i18n/server";
import { PageOverlay } from "@/i18n/PageOverlay";

type PageProps = { params: Promise<{ locale: string }> };

export async function generateMetadata({ params }: PageProps): Promise<Metadata> {
  const t = await translatorFor(params);
  return pageMetadata({
  locale: t.locale,
  title: t("Contact — request a demo"),
  description: t("Request a demo of ProGlass Automation: your glass types, team size, and whether cutting optimization matters — one reply per request."),
  path: "/contact",
  keywords: ["request glass software demo", t("contact ProGlass Automation"), t("glass ERP demo")],
  ogEyebrow: t("Request a demo"),
  });
}

export default async function ContactPage({ params }: PageProps) {
  const t = await translatorFor(params);
  const locale = t.locale;
  return (
    <PageOverlay locale={t.locale} route="contact">
    <>
    {/* Structured data for this route — see lib/schema.ts. */}
    <RouteSchema
      locale={locale}
      path="/contact"
      title={t("Contact — request a demo")}
      description={t("Request a demo of ProGlass Automation: your glass types, team size, and whether cutting optimization matters — one reply per request.")}
    />
      <PageHero
      t={t}
        glyph={footerIconFor("/contact")!}
        eyebrow={t("Contact")}
        title={t("Request a demo.")}
        lead={t("A demonstration is a walkthrough of the application on a job you recognise — a multi-section quotation, its cutting list, and the delivery behind it. Tell us a little about your business and we will arrange a time.")}
        banner="support"
        breadcrumb={[
          { label: t("Home"), href: "/" },
          { label: t("Contact"), href: "/contact" },
        ]}
        meta={[
          { label: t("Response"), value: t("By email") },
          { label: t("Direct address"), value: site.developerEmail },
          { label: t("Product"), value: `ProGlass Automation v${site.version}` },
          { label: t("Platform"), value: t("Windows desktop") },
        ]}
      />

      <Section aria-labelledby="form-heading">
        <Container>
          <div className="grid gap-12 lg:grid-cols-[minmax(0,1.25fr)_minmax(0,0.75fr)] lg:gap-16">
            <div>
              <SectionHeading
                id="form-heading"
                eyebrow={t("Demo request")}
                title={t("Tell us what you run.")}
                lead={t("Fields marked with an asterisk are required. Nothing you type here is stored on this website — the form sends your request and forgets it.")}
              />
              <div className="mt-8">
                <DemoRequestForm />
              </div>
            </div>

            <Reveal delay={80} className="space-y-5">
              <div className="surface-raised p-6">
                <p className="eyebrow mb-4">{t("What helps us prepare")}</p>
                <ul className="space-y-3.5">
                  {[
                    {
                      title: t("The glass types you process"),
                      detail: t("Whether you cut single lites, build units, laminate, or do all three."),
                    },
                    {
                      title: t("How many people would use it"),
                      detail: t("One estimator, a five-person office, or a factory floor as well."),
                    },
                    {
                      title: t("Whether the cutting table matters"),
                      detail: t("If optimization is part of your day, bring a real cutting list."),
                    },
                    {
                      title: t("Where the work hurts now"),
                      detail: t("Quoting, stock accuracy, deliveries, or the margin you cannot see until too late."),
                    },
                  ].map((item) => (
                    <li key={t(item.title)}>
                      <p className="text-sm font-medium text-text-secondary">{t(item.title)}</p>
                      <p className="mt-0.5 text-xs leading-relaxed text-text-faint font-medium">{t(item.detail)}</p>
                    </li>
                  ))}
                </ul>
              </div>

              <div className="surface p-6">
                <p className="eyebrow mb-4">{t("Prefer email?")}</p>
                <p className="text-sm leading-relaxed text-text-muted">{t("Write directly and include the same details. The address reaches the developer, not a queue.")}</p>
                <a
                  href={`mailto:${site.developerEmail}`}
                  className="mt-4 flex items-center gap-3 rounded-card border border-line-strong/70 bg-ink-950/60 px-4 py-3 transition-colors duration-200 hover:border-brand-400/50"
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
                  <span className="min-w-0 font-mono text-sm text-text-primary">{site.developerEmail}</span>
                </a>
              </div>

              <div className="surface p-6">
                <p className="eyebrow mb-3">{t("Before you ask")}</p>
                <p className="text-sm leading-relaxed text-text-muted">{t("This website is not an application. There is no account to create, no online trial to start and no dashboard to log in to. ProGlass Automation is Windows software that you install and activate offline — the trial starts on your machine, not in a browser.")}</p>
              </div>
            </Reveal>
          </div>

          <div className="mt-16 border-t border-line/60 pt-14">
            <SectionHeading
              eyebrow={t("The walkthrough")}
              title={t("What a demonstration runs through.")}
              lead={t("A demonstration runs on the current release build of ProGlass Automation, shown on a screen share. It is the software working, not a slide deck about software.")}
            />
            <div className="mt-10 grid gap-5 lg:grid-cols-3">
              {[
                {
                  title: t("From quotation to cutting list"),
                  detail: t("The session opens with a quotation written the way your office would write one — a customer, the sections they asked for, the glass build-up and the hardware. You watch that quotation become a cutting list with the dimensions carried across, and a quotation total that shows the margin line by line."),
                },
                {
                  title: t("Into the workshop"),
                  detail: t("The job then becomes workshop work. The cutting engine runs your real cutting list against stock or standard sheet sizes, the constraints for laminated and coated glass are respected, and the machine export is written in the formats the optimization page lists for your cutting table. A confirmed order leaves with its delivery note and route slip printed from the same record."),
                },
                {
                  title: t("What you will not see"),
                  detail: t("No online demo account and no sales presentation: the person answering is the person who wrote the application. If a particular part of the system matters to your business — records synchronization between a head office and a branch, or one of the reports — name it in the request, and that is where the demonstration starts."),
                },
              ].map((item) => (
                <div key={item.title} className="surface-raised p-6">
                  <p className="text-sm font-semibold text-text-primary">{item.title}</p>
                  <p className="mt-2.5 text-sm leading-relaxed text-text-muted">{item.detail}</p>
                </div>
              ))}
            </div>
          </div>
        </Container>
      </Section>

      {/* ── Next step ────────────────────────────────────────────── */}
      <Section tone="raised" aria-label={t("Next step")}>
        <Container>
          <Reveal>
            <div className="glass-primary flex flex-col items-center gap-6 p-8 text-center sm:p-10">
              <div>
                <h2 className="display-2">{t("While you wait for a reply.")}</h2>
                <p className="mx-auto mt-3 max-w-xl text-sm leading-relaxed text-text-muted">{t("The interface tour shows the screens a demonstration runs through, and the optimization page explains what the cutting engine does with a real cutting list.")}</p>
              </div>
              <div className="flex flex-wrap justify-center gap-3">
                <ButtonLink href="/screenshots" size="lg" trailing={<ArrowRight />}>
          {t("See the interface")}
        </ButtonLink>
                <ButtonLink href="/optimization" variant="secondary" size="lg">
          {t("The optimization engine")}
        </ButtonLink>
              </div>
            </div>
          </Reveal>
        </Container>
      </Section>
    </>
    </PageOverlay>
  );
}

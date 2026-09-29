import type { Metadata } from "next";
import { Container, Section } from "@/components/ui/Container";
import { PageHero } from "@/components/layout/PageHero";
import { ArrowRight, ButtonLink } from "@/components/ui/Button";
import { Reveal } from "@/components/ui/Reveal";
import { SectionHeading } from "@/components/ui/SectionHeading";
import { DemoRequestForm } from "@/components/forms/DemoRequestForm";
import { site } from "@/lib/site";
import { RouteSchema } from "@/components/seo/RouteSchema";
import { pageMetadata } from "@/lib/metadata";

export const metadata: Metadata = pageMetadata({
  title: "Contact — request a demo of ProGlass Automation",
  description:
    "Request a demonstration of ProGlass Automation: tell us your glass types, team size and whether cutting optimization matters, and you will get a reply.",
  path: "/contact",
  keywords: ["request glass software demo", "contact ProGlass Automation", "glass ERP demo"],
  ogEyebrow: "Request a demo",
});

export default function ContactPage() {
  return (
    <>
    {/* Structured data for this route — see lib/schema.ts. */}
    <RouteSchema
      path="/contact"
      title="Contact — request a demo of ProGlass Automation"
      description="Request a demonstration of ProGlass Automation: tell us your glass types, team size and whether cutting optimization matters, and you will get a reply."
    />
      <PageHero
        eyebrow="Contact"
        title="Request a demo."
        lead="A demonstration is a walkthrough of the application on a job you recognise — a multi-section quotation, its cutting list, and the delivery behind it. Tell us a little about your business and we will arrange a time."
        banner="support"
        breadcrumb={[
          { label: "Home", href: "/" },
          { label: "Contact", href: "/contact" },
        ]}
        meta={[
          { label: "Response", value: "By email" },
          { label: "Direct address", value: site.developerEmail },
          { label: "Product", value: `ProGlass Automation v${site.version}` },
          { label: "Platform", value: "Windows desktop" },
        ]}
      />

      <Section aria-labelledby="form-heading">
        <Container>
          <div className="grid gap-12 lg:grid-cols-[minmax(0,1.25fr)_minmax(0,0.75fr)] lg:gap-16">
            <div>
              <SectionHeading
                id="form-heading"
                eyebrow="Demo request"
                title="Tell us what you run."
                lead="Fields marked with an asterisk are required. Nothing you type here is stored on this website — the form sends your request and forgets it."
              />
              <div className="mt-8">
                <DemoRequestForm />
              </div>
            </div>

            <Reveal delay={80} className="space-y-5">
              <div className="surface-raised p-6">
                <p className="eyebrow mb-4">What helps us prepare</p>
                <ul className="space-y-3.5">
                  {[
                    {
                      title: "The glass types you process",
                      detail: "Whether you cut single lites, build units, laminate, or do all three.",
                    },
                    {
                      title: "How many people would use it",
                      detail: "One estimator, a five-person office, or a factory floor as well.",
                    },
                    {
                      title: "Whether the cutting table matters",
                      detail: "If optimization is part of your day, bring a real cutting list.",
                    },
                    {
                      title: "Where the work hurts now",
                      detail: "Quoting, stock accuracy, deliveries, or the margin you cannot see until too late.",
                    },
                  ].map((item) => (
                    <li key={item.title}>
                      <p className="text-sm font-medium text-text-secondary">{item.title}</p>
                      <p className="mt-0.5 text-xs leading-relaxed text-text-faint font-medium">{item.detail}</p>
                    </li>
                  ))}
                </ul>
              </div>

              <div className="surface p-6">
                <p className="eyebrow mb-4">Prefer email?</p>
                <p className="text-sm leading-relaxed text-text-muted">
                  Write directly and include the same details. The address reaches the developer, not a queue.
                </p>
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
                <p className="eyebrow mb-3">Before you ask</p>
                <p className="text-sm leading-relaxed text-text-muted">
                  This website is not an application. There is no account to create, no online trial to start and no
                  dashboard to log in to. ProGlass Automation is Windows software that you install and activate offline —
                  the trial starts on your machine, not in a browser.
                </p>
              </div>
            </Reveal>
          </div>
        </Container>
      </Section>

      {/* ── Next step ────────────────────────────────────────────── */}
      <Section tone="raised" aria-label="Next step">
        <Container>
          <Reveal>
            <div className="plate flex flex-col gap-6 p-8 sm:flex-row sm:items-center sm:justify-between sm:p-10">
              <div>
                <h2 className="text-2xl leading-tight sm:text-3xl">While you wait for a reply.</h2>
                <p className="mt-3 max-w-xl text-sm leading-relaxed text-text-muted">
                  The interface tour shows the screens a demonstration runs through, and the optimization page explains
                  what the cutting engine does with a real cutting list.
                </p>
              </div>
              <div className="flex shrink-0 flex-wrap gap-3">
                <ButtonLink href="/screenshots" size="lg" trailing={<ArrowRight />}>
                  See the interface
                </ButtonLink>
                <ButtonLink href="/optimization" variant="secondary" size="lg">
                  The optimization engine
                </ButtonLink>
              </div>
            </div>
          </Reveal>
        </Container>
      </Section>
    </>
  );
}

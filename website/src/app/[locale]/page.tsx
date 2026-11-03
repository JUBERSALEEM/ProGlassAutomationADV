import type { Metadata } from "next";
import { LogoMark } from "@/components/brand/Logo";
import { ArrowRight } from "@/components/ui/Button";
import { LocaleLink } from "@/components/ui/LocaleLink";
import { PageOverlay } from "@/i18n/PageOverlay";
import { translatorFor } from "@/i18n/server";
import { pageMetadata } from "@/lib/metadata";

type PageProps = { params: Promise<{ locale: string }> };

export async function generateMetadata({ params }: PageProps): Promise<Metadata> {
  const t = await translatorFor(params);
  return pageMetadata({
    locale: t.locale,
    title: t("Welcome to ProGlass Automation"),
    description: t(
      "ProGlass Automation helps glass businesses manage estimation, production and delivery. Sign in, create an inquiry or request a proforma invoice.",
    ),
    path: "/",
  });
}

const workflow = [
  { number: "01", label: "Estimate glass projects" },
  { number: "02", label: "Plan stock and production" },
  { number: "03", label: "Track jobs through delivery" },
] as const;

export default async function HomePage({ params }: PageProps) {
  const t = await translatorFor(params);

  return (
    <PageOverlay locale={t.locale} route="">
      <section className="relative isolate flex min-h-[calc(100svh-76px)] items-center overflow-hidden px-5 py-12 text-text-primary sm:px-8 sm:py-16 lg:py-20">
        <div aria-hidden="true" className="pointer-events-none absolute inset-0 -z-10 overflow-hidden">
          <div className="absolute -top-48 start-[8%] h-[30rem] w-[30rem] rounded-full bg-brand-500/10 blur-[110px]" />
          <div className="absolute -bottom-56 end-[3%] h-[28rem] w-[28rem] rounded-full bg-glass-cyan/10 blur-[110px]" />
        </div>

        <div className="mx-auto grid w-full max-w-7xl items-center gap-10 lg:grid-cols-[minmax(0,1.08fr)_minmax(320px,.92fr)] lg:gap-16">
          <div className="max-w-3xl">
            <div className="mb-6 inline-flex items-center gap-2.5 rounded-full border border-line-strong/80 bg-ink-900/80 px-3.5 py-2 text-xs font-bold tracking-wide text-text-secondary shadow-panel sm:mb-8">
              <span aria-hidden="true" className="h-2 w-2 rounded-full bg-state-success" />
              {t("Glass fabrication business software")}
            </div>

            <h1 className="max-w-3xl text-balance text-[clamp(2.65rem,6vw,5.25rem)] font-semibold leading-[1.02] tracking-[-.045em] text-text-primary">
              {t("Welcome to")} <span className="text-brand-300">ProGlass Automation</span>
            </h1>
            <p className="mt-6 max-w-2xl text-pretty text-lg leading-8 text-text-secondary sm:text-xl sm:leading-9">
              {t("A Windows desktop system for glass businesses to manage estimates, production, stock and delivery in one connected workflow.")}
            </p>
            <p className="mt-3 max-w-2xl text-pretty text-base leading-7 text-text-muted">
              {t("Use this customer portal to tell us about a project, request a proforma invoice, or access your account.")}
            </p>

            <div className="mt-8 flex flex-wrap items-center gap-3">
              <LocaleLink
                href="/inquiries/submit"
                className="group inline-flex min-h-12 items-center justify-center gap-2.5 rounded-xl bg-brand-600 px-5 py-3 text-sm font-bold text-accent-ink shadow-accent transition-[background-color,box-shadow,transform] duration-150 hover:-translate-y-px hover:bg-brand-500 hover:shadow-accent-strong active:translate-y-0 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-brand-300"
              >
                {t("Start an inquiry")}
                <ArrowRight />
              </LocaleLink>
              <LocaleLink
                href="/inquiries/submit/proforma"
                className="inline-flex min-h-12 items-center justify-center gap-2 rounded-xl border border-line-strong bg-ink-900/80 px-5 py-3 text-sm font-bold text-text-primary transition-[background-color,border-color,transform] duration-150 hover:-translate-y-px hover:border-brand-400/60 hover:bg-ink-850 active:translate-y-0 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-brand-300"
              >
                {t("Request a PI")}
              </LocaleLink>
              <LocaleLink
                href="/account"
                className="inline-flex min-h-11 items-center rounded-xl px-3 py-2 text-sm font-semibold text-text-secondary underline decoration-line-strong underline-offset-4 transition-colors duration-150 hover:text-text-primary focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-brand-300"
              >
                {t("Customer account")}
              </LocaleLink>
            </div>
            <p className="mt-4 text-xs leading-relaxed text-text-muted">
              {t("Start from Create inquiry in the top panel whenever you’re ready.")}
            </p>
          </div>

          <aside className="relative mx-auto w-full max-w-[34rem]" aria-label={t("What ProGlass does")}>
            <div aria-hidden="true" className="absolute -inset-4 rounded-[2.25rem] bg-brand-500/10 blur-2xl" />
            <div className="relative overflow-hidden rounded-panel border border-line-strong/80 bg-ink-900/95 p-6 shadow-raised sm:p-8">
              <div className="flex items-center gap-4">
                <span className="grid h-14 w-14 shrink-0 place-items-center rounded-2xl border border-brand-400/35 bg-brand-500/10 text-brand-300">
                  <LogoMark size={36} />
                </span>
                <div className="min-w-0">
                  <p className="text-xs font-bold uppercase tracking-[.16em] text-brand-300 sm:text-sm">
                    {t("ProGlass Automation")}
                  </p>
                  <p className="mt-1 text-sm text-text-secondary">{t("From estimate to delivery")}</p>
                </div>
              </div>

              <div className="my-6 h-px bg-line/80" />
              <div className="flex items-end justify-between gap-3">
                <div>
                  <p className="text-xs font-bold uppercase tracking-[.16em] text-text-muted">
                    {t("One connected workflow")}
                  </p>
                  <h2 className="mt-1 text-xl font-bold tracking-tight text-text-primary sm:text-2xl">
                    {t("Plan every step with confidence")}
                  </h2>
                </div>
                <span aria-hidden="true" className="hidden h-10 w-10 shrink-0 place-items-center rounded-full bg-brand-500/10 text-brand-300 sm:grid">
                  <ArrowRight />
                </span>
              </div>

              <ol className="mt-6 space-y-3">
                {workflow.map(({ number, label }) => (
                  <li key={number} className="flex items-center gap-3 rounded-2xl border border-line/80 bg-ink-950/45 p-3.5 sm:gap-4 sm:p-4">
                    <span className="grid h-10 w-10 shrink-0 place-items-center rounded-xl border border-brand-400/35 bg-brand-500/10 font-mono text-xs font-bold text-brand-300">
                      {number}
                    </span>
                    <span className="text-sm font-semibold leading-6 text-text-primary sm:text-base">{t(label)}</span>
                  </li>
                ))}
              </ol>

              <div className="mt-5 rounded-2xl border border-state-success/30 bg-state-success/10 px-4 py-3.5">
                <p className="text-sm leading-6 text-text-secondary">
                  {t("Need a quote or project review? Start with an inquiry and our team will follow up.")}
                </p>
              </div>
            </div>
          </aside>
        </div>
      </section>
    </PageOverlay>
  );
}

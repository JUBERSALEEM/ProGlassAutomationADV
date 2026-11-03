import type { Metadata } from "next";
import { LogoMark } from "@/components/brand/Logo";
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
      <section className="welcome-home relative isolate flex min-h-[calc(100svh-76px)] items-center overflow-hidden px-5 py-14 text-text-primary sm:px-8 sm:py-20 lg:py-16">
        <div aria-hidden="true" className="pointer-events-none absolute inset-0 -z-10 overflow-hidden">
          <div className="absolute -top-44 start-[8%] h-[30rem] w-[30rem] rounded-full bg-brand-500/[.09] blur-[110px]" />
          <div className="absolute -bottom-52 end-[4%] h-[28rem] w-[28rem] rounded-full bg-glass-cyan/[.08] blur-[115px]" />
          <div className="absolute inset-0 bg-[radial-gradient(ellipse_at_center,transparent_45%,rgb(var(--ink-950)/.14)_100%)]" />
        </div>

        <div className="mx-auto grid w-full max-w-7xl items-center gap-12 lg:grid-cols-[minmax(0,1.08fr)_minmax(340px,.92fr)] lg:gap-14 xl:gap-20">
          <div className="max-w-3xl">
            <div className="mb-7 flex items-center gap-4 sm:mb-8">
              <span className="grid h-[4.5rem] w-[4.5rem] shrink-0 place-items-center rounded-[1.4rem] border border-brand-400/25 bg-brand-500/[.09] shadow-[0_12px_36px_rgb(var(--brand-600)/.12)] sm:h-20 sm:w-20">
                <LogoMark size={56} className="sm:!h-14 sm:!w-14" />
              </span>
              <div>
                <p className="text-xs font-bold uppercase tracking-[.2em] text-brand-300 sm:text-sm">
                  {t("Glass fabrication business software")}
                </p>
                <p className="mt-1.5 text-sm font-medium text-text-secondary sm:text-base">
                  {t("From estimate to delivery")}
                </p>
              </div>
            </div>

            <h1 className="max-w-3xl text-balance text-[clamp(2.75rem,5.2vw,4.65rem)] font-semibold leading-[1.04] tracking-[-.045em] text-text-primary">
              {t("Welcome to")} {" "}
              <span className="text-brand-300">ProGlass Automation</span>
            </h1>
            <p className="mt-7 max-w-2xl text-pretty text-lg leading-8 text-text-secondary sm:text-xl sm:leading-9">
              {t("A Windows desktop system for glass businesses to manage estimates, production, stock and delivery in one connected workflow.")}
            </p>
            <p className="mt-4 max-w-2xl text-pretty text-base leading-7 text-text-muted">
              {t("Use this customer portal to tell us about a project, request a proforma invoice, or access your account.")}
            </p>

            <div className="mt-8 inline-flex max-w-full items-start gap-3 rounded-2xl border border-line/75 bg-ink-900/65 px-4 py-3.5 text-sm font-semibold leading-6 text-text-secondary sm:items-center sm:rounded-full sm:px-5">
              <svg aria-hidden="true" viewBox="0 0 20 20" className="mt-0.5 h-5 w-5 shrink-0 text-brand-300 sm:mt-0" fill="none" stroke="currentColor" strokeWidth="1.7" strokeLinecap="round" strokeLinejoin="round">
                <path d="M4 10h11M10 5l5 5-5 5" />
              </svg>
              <span>{t("Start from the Create inquiry menu above whenever you’re ready.")}</span>
            </div>
          </div>

          <aside className="relative mx-auto w-full max-w-[31rem]" aria-label={t("What ProGlass does")}>
            <div aria-hidden="true" className="absolute -inset-4 rounded-[2.25rem] bg-brand-500/[.09] blur-2xl" />
            <div className="relative overflow-hidden rounded-[1.8rem] border border-line/80 bg-ink-900/95 p-6 shadow-[0_28px_80px_rgb(var(--ink-950)/.22)] sm:rounded-[2rem] sm:p-8">
              <div className="flex items-center gap-4">
                <span className="grid h-14 w-14 place-items-center rounded-2xl border border-brand-400/25 bg-brand-500/[.09]">
                  <LogoMark size={38} />
                </span>
                <div>
                  <p className="text-xs font-bold uppercase tracking-[.16em] text-brand-300 sm:text-sm">
                    {t("ProGlass Automation")}
                  </p>
                  <p className="mt-1 text-sm text-text-muted">{t("From estimate to delivery")}</p>
                </div>
              </div>

              <div className="my-6 h-px bg-line/75 sm:my-7" />
              <p className="text-xs font-bold uppercase tracking-[.17em] text-text-muted sm:text-sm">
                {t("One connected workflow")}
              </p>
              <ol className="mt-5 space-y-3.5 sm:space-y-4">
                {workflow.map(({ number, label }) => (
                  <li key={number} className="flex items-center gap-4">
                    <span className="grid h-10 w-10 shrink-0 place-items-center rounded-xl border border-brand-400/25 bg-brand-500/[.08] font-mono text-xs font-semibold text-brand-300">
                      {number}
                    </span>
                    <span className="text-sm font-semibold text-text-primary sm:text-base">{t(label)}</span>
                  </li>
                ))}
              </ol>

              <div className="mt-7 rounded-2xl border border-line/75 bg-ink-800/75 px-4 py-4 sm:px-5">
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

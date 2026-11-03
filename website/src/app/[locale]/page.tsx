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

export default async function HomePage({ params }: PageProps) {
  const t = await translatorFor(params);

  return (
    <PageOverlay locale={t.locale} route="">
      <section className="relative isolate flex min-h-[calc(100svh-76px)] items-center overflow-hidden px-5 py-16 sm:px-8 sm:py-24">
        <div aria-hidden="true" className="pointer-events-none absolute inset-0 -z-10 overflow-hidden">
          <div className="absolute -top-48 start-1/2 h-[34rem] w-[34rem] -translate-x-1/2 rounded-full bg-brand-500/15 blur-[110px]" />
          <div className="absolute -bottom-64 -end-32 h-[30rem] w-[30rem] rounded-full bg-cyan-400/10 blur-[120px]" />
          <div className="absolute inset-0 bg-[radial-gradient(ellipse_at_center,transparent_0%,rgb(8_5_22/.55)_75%)]" />
        </div>

        <div className="mx-auto grid w-full max-w-7xl items-center gap-14 lg:grid-cols-[minmax(0,1.2fr)_minmax(300px,.8fr)] lg:gap-20">
          <div className="max-w-3xl">
            <div className="mb-8 inline-flex items-center gap-2 rounded-full border border-brand-300/20 bg-brand-400/10 px-3.5 py-2 text-xs font-semibold tracking-wide text-brand-100">
              <span className="h-2 w-2 rounded-full bg-emerald-300 shadow-[0_0_14px_rgb(110_231_183/.8)]" />
              {t("Glass fabrication business software")}
            </div>
            <h1 className="text-balance text-5xl font-semibold leading-[1.04] tracking-[-0.045em] text-white sm:text-6xl lg:text-7xl">
              {t("Welcome to")} <span className="bg-gradient-to-r from-brand-200 via-violet-200 to-cyan-200 bg-clip-text text-transparent">ProGlass Automation</span>
            </h1>
            <p className="mt-7 max-w-2xl text-pretty text-lg leading-8 text-text-muted sm:text-xl">
              {t("A Windows desktop system for glass businesses to manage estimates, production, stock and delivery in one connected workflow.")}
            </p>
            <p className="mt-4 max-w-2xl text-pretty text-base leading-7 text-text-faint">
              {t("Use this customer portal to tell us about a project, request a proforma invoice, or access your account.")}
            </p>

            <p className="mt-8 inline-flex items-center gap-2 text-sm font-semibold text-brand-100">
              <span aria-hidden="true" className="h-px w-8 bg-brand-300/60" />
              {t("Start from the Create inquiry menu above whenever you’re ready.")}
            </p>
          </div>

          <aside className="relative mx-auto w-full max-w-md" aria-label={t("What ProGlass does")}>
            <div className="absolute -inset-5 rounded-[2.5rem] bg-gradient-to-br from-brand-400/20 via-transparent to-cyan-300/10 blur-2xl" />
            <div className="relative overflow-hidden rounded-[2rem] border border-white/10 bg-slate-950/70 p-7 shadow-2xl shadow-black/30 backdrop-blur-xl sm:p-9">
              <div className="flex items-center gap-4">
                <span className="grid h-14 w-14 place-items-center rounded-2xl border border-brand-300/20 bg-brand-400/10">
                  <LogoMark size={32} />
                </span>
                <div>
                  <p className="text-xs font-semibold uppercase tracking-[.18em] text-brand-200">{t("ProGlass Automation")}</p>
                  <p className="mt-1 text-sm text-text-faint">{t("From estimate to delivery")}</p>
                </div>
              </div>
              <div className="my-7 h-px bg-gradient-to-r from-transparent via-white/15 to-transparent" />
              <p className="text-sm font-semibold uppercase tracking-[.16em] text-text-faint">{t("One connected workflow")}</p>
              <ol className="mt-5 space-y-4">
                {[
                  ["01", t("Estimate glass projects")],
                  ["02", t("Plan stock and production")],
                  ["03", t("Track jobs through delivery")],
                ].map(([number, label]) => (
                  <li key={number} className="flex items-center gap-4">
                    <span className="grid h-9 w-9 shrink-0 place-items-center rounded-xl border border-white/10 bg-white/[.04] font-mono text-xs text-brand-200">{number}</span>
                    <span className="text-sm font-medium text-text-primary">{label}</span>
                  </li>
                ))}
              </ol>
              <div className="mt-7 rounded-2xl border border-emerald-300/15 bg-emerald-300/[.06] px-4 py-3.5">
                <p className="text-sm leading-6 text-emerald-100/90">{t("Need a quote or project review? Start with an inquiry and our team will follow up.")}</p>
              </div>
            </div>
          </aside>
        </div>
      </section>
    </PageOverlay>
  );
}

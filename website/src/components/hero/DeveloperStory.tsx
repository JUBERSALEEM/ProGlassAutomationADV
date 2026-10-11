import { LocaleLink as Link } from "@/components/ui/LocaleLink";
import { ArrowRight, ButtonLink } from "@/components/ui/Button";
import { Reveal } from "@/components/ui/Reveal";
import { Spotlight } from "@/components/ui/Spotlight";
import { GlassCubeEmblem, BrandKeyline } from "@/components/brand/GlassCubeEmblem";
import { journey, dream } from "@/data/developerHistory";
import { site } from "@/lib/site";
import type { Translator } from "@/i18n/core";

/**
 * The developer story, on the front page.
 *
 * Why it is here and not only on `/developer`: the single most unusual fact
 * about this product is not a feature. It is that a glass estimator wrote an
 * ERP, alone, and published the audit trail of everything that is wrong with
 * it. A visitor who reads three module descriptions and leaves never learns
 * that, and it is the reason the software looks the way it does — every screen
 * is named after a job rather than after a table because the author had to be
 * able to use it himself.
 *
 * So it sits in the opening of the page, immediately under the hero: the
 * product's claim, then the person who is accountable for it.
 *
 * Nothing here is invented. Every line comes from `src/data/developerHistory.ts`,
 * which is itself drawn from the dated working documents at the root of the
 * repository, and the figures are the counts in `src/lib/site.ts` that are
 * measured from the application tree. There is deliberately no photograph, no
 * biography and no years-of-experience number, because none of those exist to
 * be honest about — the paper trail does, and the paper trail is what is shown.
 */
export function DeveloperStory({ t }: { t: Translator }) {
  /*
   * The four phases that have already happened, and the fifth, which has not.
   *
   * The fifth is looked up rather than indexed: `journey[4]` is `JourneyPhase |
   * undefined` as far as the compiler is concerned, and the honest way to
   * handle a phase that may not be in the data is to render the card only when
   * it is. If the author closes that phase and removes the entry, the card
   * disappears instead of throwing at build time.
   */
  const phases = journey.filter((phase) => phase.index !== "05");
  const intention = journey.find((phase) => phase.index === "05");

  const figures = [
    { value: site.productionLines.toLocaleString("en-US"), label: t("lines of production C#") },
    { value: site.testCount.toLocaleString("en-US"), label: t("tests in the build") },
    { value: String(site.moduleCount), label: t("modules, inquiry to statement") },
    { value: String(site.locales), label: t("languages in the interface") },
  ];

  return (
    <section
      id="developer-story"
      aria-labelledby="developer-story-heading"
      className="band-rich relative overflow-hidden"
    >
      {/* The aurora sits behind the band's own wash and drifts very slowly. It
          is decoration, so it is hidden from assistive technology. */}
      <div aria-hidden="true" className="pointer-events-none absolute inset-0">
        <div className="aurora-field aurora-drift" />
      </div>

      <div className="shell relative py-16 sm:py-20 lg:py-24">
        <div className="grid items-start gap-12 text-center lg:grid-cols-[minmax(0,1.06fr)_minmax(0,1fr)] lg:gap-16 lg:text-left">
          {/* ── The story ─────────────────────────────────────────────── */}
          <div>
            <Reveal>
              <p className="eyebrow-chip mb-6 inline-flex">
                <GlassCubeEmblem size={14} label={null} className="h-3.5 w-3.5" />
                {t("About the developer")}
              </p>
            </Reveal>

            <Reveal delay={70}>
              <h2 id="developer-story-heading" className="display-1">
                {t("Written by one glass estimator,")}
                <span className="text-gradient-rich block">{t("not by a software house.")}</span>
              </h2>
            </Reveal>

            <Reveal delay={130}>
              <p className="lede-lg mt-6 max-w-lede max-lg:mx-auto">
                {t("ProGlass Automation is the work of")}{" "}
                <strong className="font-semibold text-text-primary">{site.developerName}</strong>
                {t(", who prices glass for a living. He wrote the first version to stop retyping the same")
                }{" "}
                {t("measurement five times a day, and kept going when it turned out that the day he was")
                }{" "}
                {t("fixing is the same day in every glass shop.")}
              </p>
            </Reveal>

            {/* The author's own words, in a reading face. A pull-quote is the
                one place on the site where the serif is not an alternative the
                visitor has to choose: this is a person speaking, and a
                quotation set in the interface face reads as marketing copy. */}
            <Reveal delay={190}>
              <figure className="mt-9 max-w-2xl border-s-2 border-brand-500/60 ps-6 max-lg:mx-auto max-lg:border-s-0 max-lg:border-t-2 max-lg:pt-6 max-lg:ps-0">
                <blockquote className="font-[var(--font-serif)] text-lg leading-[1.68] text-text-secondary sm:text-xl">
                  <span aria-hidden="true" className="me-1 text-brand-400">&ldquo;</span>
                  {t(dream.quote)}
                  <span aria-hidden="true" className="ms-1 text-brand-400">&rdquo;</span>
                </blockquote>
                <figcaption className="mono-label mt-4">{t(dream.attribution)}</figcaption>
              </figure>
            </Reveal>

            <Reveal delay={240}>
              <div className="mt-9 flex flex-wrap items-center justify-center gap-3 lg:justify-start">
                <ButtonLink href="/developer" size="md" trailing={<ArrowRight />}>
                  {t("Read the developer story")}
                </ButtonLink>
                <ButtonLink href="/collaborate" variant="secondary" size="md">
                  {t("Work on it with him")}
                </ButtonLink>
              </div>
            </Reveal>
          </div>

          {/* ── The record ────────────────────────────────────────────── */}
          <Reveal delay={120} variant="scale">
            <Spotlight className="panel-feature edge-conic p-6 sm:p-8">
              {/* The emblem, the name, and the descriptor the application
                  prints under its own wordmark on the splash screen. */}
              <div className="flex items-center gap-5">
                <div className="relative grid place-items-center">
                  {/* The splash&apos;s own fluid glow, as a gradient rather than
                      a blurred disc — same light, no filter kernel. */}
                  <span aria-hidden="true" className="orb orb-wide left-1/2 top-1/2 h-28 w-28 -translate-x-1/2 -translate-y-1/2" />
                  <GlassCubeEmblem size={72} className="relative" />
                </div>
                <div className="min-w-0">
                  <p className="mono-label">{t("Author &amp; maintainer")}</p>
                  <p className="mt-1.5 text-xl font-semibold tracking-tight text-text-primary">
                    {site.developerName}
                  </p>
                  <p className="mt-1 text-sm text-text-muted">
                    {t("Glass estimator ·")} {site.developerLocation}
                  </p>
                </div>
              </div>

              <BrandKeyline
                label={t("Glass cutting automation suite")}
                className="mt-7"
              />

              {/* Four measured counts. These are figures from the application
                  tree, not claims about performance — the same discipline the
                  rest of the site holds. */}
              <dl className="mt-7 grid grid-cols-2 gap-x-6 gap-y-5">
                {figures.map((figure) => (
                  <div key={figure.label}>
                    <dt className="sr-only">{figure.label}</dt>
                    <dd>
                      <span className="figure figure-md block">{figure.value}</span>
                      <span className="mt-1 block text-2xs font-medium leading-snug text-text-faint">
                        {figure.label}
                      </span>
                    </dd>
                  </div>
                ))}
              </dl>

              <hr className="glass-divider my-7" />

              {/* The journey, condensed to its four finished phases. The spine
                  is the app's own stage indicator drawn in CSS. */}
              <p className="mono-label mb-5">{t("How it got here")}</p>
              <ol className="spine space-y-5">
                {phases.map((phase) => (
                  <li key={phase.index} className="relative">
                    <span aria-hidden="true" className="spine-node" />
                    <p className="font-mono text-2xs font-semibold uppercase tracking-[0.16em] text-brand-300">
                      {t(phase.when)}
                    </p>
                    <p className="mt-1 text-sm font-semibold text-text-primary">{t(phase.title)}</p>
                    <p className="mt-1 text-xs leading-relaxed text-text-muted">{t(phase.body)}</p>
                  </li>
                ))}
              </ol>

              {/* Phase five has not happened, and the card says so rather than
                  listing it beside the four that have. A dashed edge is the
                  site's own convention for "this is a plan, not a record". */}
              {intention ? (
                <div className="mt-7 rounded-card border border-dashed border-brand-500/45 bg-brand-600/12 p-4">
                  <p className="font-mono text-2xs font-semibold uppercase tracking-[0.16em] text-brand-300">
                    {t(intention.when)} · {t("the intention")}
                  </p>
                  <p className="mt-1.5 text-sm font-semibold text-text-primary">{t(intention.title)}</p>
                  <p className="mt-1 text-xs leading-relaxed text-text-muted">{t(intention.body)}</p>
                </div>
              ) : null}

              <p className="mt-6 text-2xs leading-relaxed text-text-faint">
                {t("Every claim on this page traces to a dated working document at the root of the")}{" "}
                <Link
                  href="/developer"
                  className="link-grow font-semibold text-brand-300 hover:text-brand-200"
                >
                  {t("repository")}
                </Link>
                {t(", named beside the entry it supports.")}
              </p>
            </Spotlight>
          </Reveal>
        </div>
      </div>
    </section>
  );
}

import Image from "next/image";
import { ArrowRight, ButtonLink } from "@/components/ui/Button";
import { AppFrame } from "@/components/product/AppFrame";
import { LiveSurface } from "@/components/product/LiveSurface";

/* Measurement note: `next/dynamic` was tried here to move the demo's script
   off the first-load batch. It does not apply to an SSR-rendered dynamic
   component — Next preloads its chunk with the page regardless, because the
   markup it hydrates is part of the first paint's content. Verified on the
   built assets: identical first-load weight either way. A direct import is
   the same outcome with less machinery, so the split was reverted. */
import { Reveal } from "@/components/ui/Reveal";
import { MotionReveal } from "@/components/motion/MotionReveal";
import { HeroField } from "@/components/hero/HeroField";
import { Spotlight } from "@/components/ui/Spotlight";
import { Marquee, MarqueeItem } from "@/components/ui/Marquee";
import { GlassCubeEmblem, BrandKeyline } from "@/components/brand/GlassCubeEmblem";
import { brandBanner, heroBackdrop } from "@/data/assets";
import { LocaleLink as Link } from "@/components/ui/LocaleLink";
import { chainVerbs } from "@/data/workflows";
import { site } from "@/lib/site";
import type { Translator } from "@/i18n/core";

/**
 * Homepage hero.
 *
 * The headline is the product's own positioning: the README calls ProGlass an
 * "industrial glass-fabrication ERP", which is accurate but is jargon to the
 * person who actually runs a glass shop. The first line therefore says what it
 * is for, and the second line names the chain the application implements.
 *
 * Three things this opening carries that a hero normally leaves to a later
 * section, and each is here because it answers a question the visitor has
 * before they have finished reading the headline:
 *
 *   the byline     who is accountable for this — one named person, linked to
 *                  the developer story that opens immediately below
 *   the keyline    what the product calls itself, in the application's own
 *                  words, from the splash screen it opens with
 *   the fact row   what kind of software it is, moving slowly enough to read
 */
export function ProductHero({ t }: { t: Translator }) {
  return (
    <HeroField className="relative overflow-hidden pb-12 pt-14 sm:pb-16 sm:pt-20 lg:pb-24 lg:pt-24">
      {/* ── Banner backdrop ──────────────────────────────────────────────
          A stack of glass sheets, drawn in the site's own geometry and tokens
          by `scripts/render-hero-backdrop.mts` and rendered at true 2×.

          It replaced a 1584 px photograph of a workshop, drawn full-bleed and
          then dimmed to a third of its opacity. That image was being enlarged
          about 2.3× on a 1920 px screen and diffused by two gradients on top of
          it, which is what "the background is blurred" was: real detail on
          screen at a size the file could not carry. Geometry has no such
          problem — every edge here is a hairline at any size.

          The veil is now a light one, because the picture no longer depends on
          it: its left third is drawn deliberately quiet, which is where the
          headline and the buttons sit. Decorative, so it is hidden from
          assistive technology and carries no caption. */}
      {/* §X: the backdrop layers used to span the hero's full height
          (inset-0 over a section that runs copy + frame + marquee +
          banner ≈ 2,600px on a phone) — the largest raster on the
          page, sitting directly under the translucent frame, which is
          the one region whose tiles starved on the device witness.
          The artwork is now bounded to the first screens (the zone
          the headline and the frame's head actually read against);
          the veils inside already fade it into the page colour, and
          the fixed liquid field carries the room below. */}
      <div aria-hidden="true" className="hero-backdrop-parallax pointer-events-none absolute inset-x-0 top-0 h-full max-h-[180svh] overflow-hidden">
        <Image
          src={heroBackdrop.src}
          alt=""
          fill
          priority
          sizes="100vw"
          quality={92}
          className="object-cover object-right"
        />
        {/* Two veils rather than one. The horizontal veil keeps the headline
            column quiet over the artwork; the vertical one drops the page
            colour back in at the foot so the hero hands over to the band below
            it without a hard line across the picture. */}
        <div className="absolute inset-0 bg-gradient-to-r from-ink-950/92 via-ink-950/55 to-ink-950/10" />
        <div className="absolute inset-0 bg-gradient-to-t from-ink-950 via-ink-950/25 to-ink-950/60" />
      </div>

      {/* The aurora: three saturated washes crossing the opening screen,
          drifting on a 26 s cycle. It is what makes the top of the page read as
          a lit room rather than as a flat field, and it is drawn from the
          family's own pools so switching palette re-hues it. */}
      <div aria-hidden="true" className="hero-backdrop-parallax pointer-events-none absolute inset-x-0 top-0 h-full max-h-[180svh] overflow-hidden">
        <div className="aurora-field aurora-drift">
          {/* §C9/§R — the references' ornaments live behind the hero's glass:
              two rings and one sphere, crystal-painted only. */}
          <span className="decor decor-ring max-lg:hidden" style={{ right: "9%", top: "12%", width: 180, height: 180 }} />
          <span className="decor decor-ring max-lg:hidden" style={{ left: "6%", bottom: "18%", width: 120, height: 120 }} />
          <span className="glass-sphere max-lg:hidden" data-chill-target style={{ position: "absolute", right: "26%", top: "30%", width: 260, height: 260 }} />
        </div>
        <div className="absolute inset-x-0 top-0 h-px bg-gradient-to-r from-transparent via-brand-400/60 to-transparent" />
      </div>

      {/* ── The materials cloud ───────────────────────────────────────────
          The four build-ups the application prices — SGU, DGU, lam and the
          combined DGU+LAM — as free-floating renders at four depths of one
          scene: a sheet's `--d` is both its distance from the visitor and
          how pointer it rides, so the glass stack reads as real depth rather
          than as decoration glued to the text. Under reduced motion, on a
          coarse pointer, or before the field is live, they hold a fixed
          composition — the hero works from first paint either way.

          Decorative: every build-up is already named and priced where the
          visitor can read it, further down and on /screenshots. */}
      <div aria-hidden="true" className="pointer-events-none absolute inset-0 hidden lg:block">
        <div className="hero-shard hero-shard-1" style={{ "--d": 1.9 } as React.CSSProperties}>
          {/* eslint-disable-next-line @next/next/no-img-element */}
          <img src="/images/glass3d/dgu-3d-640.webp" alt="" width={118} height={118} loading="eager" />
        </div>
        <div className="hero-shard hero-shard-2" style={{ "--d": 1.1 } as React.CSSProperties}>
          {/* eslint-disable-next-line @next/next/no-img-element */}
          <img src="/images/glass3d/sgu-3d-640.webp" alt="" width={88} height={88} loading="lazy" />
        </div>
        <div className="hero-shard hero-shard-3" style={{ "--d": 2.6 } as React.CSSProperties}>
          {/* eslint-disable-next-line @next/next/no-img-element */}
          <img src="/images/glass3d/dgulam-3d-640.webp" alt="" width={104} height={104} loading="eager" />
        </div>
        <div className="hero-shard hero-shard-4" style={{ "--d": 0.7 } as React.CSSProperties}>
          {/* eslint-disable-next-line @next/next/no-img-element */}
          <img src="/images/glass3d/lam-3d-640.webp" alt="" width={64} height={64} loading="lazy" />
        </div>
      </div>

      {/* Aurora v2 — a second bank of light counter-drifting the page's own
          field: the violet pool at the upper right of the first screen, an
          ice pool low on the left, both drawn from the family's pools and
          breathing on a 32 s cycle so the ceiling of the page reads lit. */}
      <div aria-hidden="true" className="hero-aurora-strong pointer-events-none absolute inset-x-0 top-0 h-full max-h-[180svh]" />

      <div className="shell relative">
        <div className="grid items-center gap-12 lg:grid-cols-[minmax(0,1fr)_minmax(0,1.06fr)] lg:gap-14">
          <div className="max-lg:text-center">
            {/* ── The header block ──────────────────────────────────────
                Product mark, name, version, and the byline. A hero that opens
                with a bare eyebrow label says nothing about who is behind the
                claim; this one names the author in the first line a visitor
                reads, and links to the story that follows it. */}
            <MotionReveal immediate>
              <div className="mb-7 flex flex-wrap items-center gap-x-4 gap-y-3 max-lg:justify-center">
                <span className="flex items-center gap-2.5">
                  <GlassCubeEmblem size={26} className="h-6 w-6" />
                  <span className="text-sm font-semibold tracking-tight text-text-primary">
                    Pro<span className="text-brand-300">Glass</span>{" "}
                    <span className="font-mono text-2xs font-semibold uppercase tracking-[0.22em] text-text-faint">
                      Automation
                    </span>
                  </span>
                </span>
                <span aria-hidden="true" className="hidden h-4 w-px bg-line-strong sm:block" />
                <span className="font-mono text-2xs font-semibold uppercase tracking-[0.18em] text-text-faint">
                  v{site.version}
                </span>
              </div>
            </MotionReveal>

            <MotionReveal immediate delay={60}>
              <p className="mb-5 flex flex-wrap items-center gap-x-2.5 gap-y-2 text-xs leading-relaxed text-text-muted max-lg:justify-center">
                <span aria-hidden="true" className="h-1.5 w-1.5 rotate-45 bg-brand-400" />
                <span>
                  {t("Written and maintained by")}{" "}
                  <strong className="font-semibold text-text-primary">{site.developerName}</strong>
                  {t(", a glass estimator —")}
                </span>
                <Link
                  href="#developer-story"
                  className="link-grow font-semibold text-brand-300 hover:text-brand-200"
                >
                  {t("the developer story")}
                </Link>
                <ArrowRight className="h-3 w-3 text-brand-400" />
              </p>
            </MotionReveal>

            <MotionReveal immediate delay={110}>
              <h1 className="display-hero">
                {t("Glass business management,")}
                <span className="text-gradient-rich block">{t("built around your workflow.")}</span>
              </h1>
            </MotionReveal>

            {/* The application's own descriptor, from the keyline it prints
                under its wordmark on the splash screen. */}
            <MotionReveal immediate delay={160}>
              <BrandKeyline
                label={t("Glass cutting automation suite")}
                className="mt-7 justify-start max-lg:justify-center"
              />
            </MotionReveal>

            <MotionReveal immediate delay={200}>
              <p className="lede-lg mt-7 max-w-xl max-lg:mx-auto">
                {t("A Windows desktop application for glass fabrication: price the job, cut the stock, track what left the")}{" "}
                {t("yard, and invoice it — without re-keying the same glass twice.")}
              </p>
            </MotionReveal>

            <MotionReveal immediate delay={250}>
              <div className="mt-9 flex flex-wrap items-center gap-3 max-lg:justify-center">
                <ButtonLink href="/contact" size="lg" trailing={<ArrowRight />}>
                  {t("Request a Demo")}
                </ButtonLink>
                <ButtonLink href="/features" variant="secondary" size="lg">
                  {t("Explore ProGlass")}
                </ButtonLink>
              </div>
            </MotionReveal>
          </div>

          {/* ── Layered product presentation ───────────────────────── */}
          <MotionReveal immediate delay={120} className="relative">
            <div aria-hidden="true" className="pointer-events-none absolute -inset-8 -z-10">
              <span
                className="orb orb-wide left-1/2 top-1/2 h-[26rem] w-[34rem] -translate-x-1/2 -translate-y-1/2"
                style={{ "--orb": "rgb(var(--brand-600) / 0.32)" } as React.CSSProperties}
              />
            </div>

            <AppFrame
              t={t}
              title={t("Dashboard — Overview")}
              railActive="Dashboard"
              className="lg:translate-x-2"
              note={
                <>
                  <span className="font-mono text-2xs uppercase tracking-[0.16em] text-brand-300">
                    {t("Interface map")}
                  </span>{" "}
                  — the rail groups, module labels and top-bar elements are the application&apos;s own
                  (Views/Personal/GlassShell.xaml). The panel inside is a working preview you can click; it is not a
                  screen capture.
                </>
              }
            >
              <LiveSurface
                moduleKey="Dashboard"
                name="Overview dashboard"
                capabilities={["KPI cards", "Order-to-cash pipeline", "Activity feed"]}
              />
            </AppFrame>

            {/* Floating detail card — real fields from the proforma Step 9.
                Floats over the corner of the window from `sm` up. Below that it
                stacks underneath instead: a 224 px panel pinned over a preview
                that is itself only ~290 px wide on a 320 px phone covers the
                product shot it is supposed to be annotating, and the fields
                inside it end up sitting on a busy background. Same card, same
                fields — it just stops overlapping where there is no room to
                overlap. */}
            <Spotlight className="float-card edge-conic relative mt-4 w-full rounded-panel p-4 shadow-raised sm:absolute sm:-bottom-6 sm:-left-8 sm:mt-0 sm:w-64">
              <p className="font-mono text-2xs uppercase tracking-[0.16em] text-text-faint font-medium">{t("Proforma · Step 9")}</p>
              <p className="mt-2 text-sm font-semibold text-text-primary">Profit &amp; loss snapshot</p>
              <dl className="mt-3 space-y-1.5 text-xs">
                {[
                  ["Cost basis", "Sheet store buy price"],
                  ["Wastage", "Step 6 rows"],
                  ["VAT", "5% · round-safe"],
                ].map(([key, value]) => (
                  <div key={key} className="flex items-center justify-between gap-3">
                    <dt className="text-text-faint">{key}</dt>
                    <dd className="truncate font-medium text-text-secondary">{value}</dd>
                  </div>
                ))}
              </dl>
            </Spotlight>

            {/* Floating chain chip. */}
            <div className="float-card absolute -right-2 -top-5 hidden rounded-full px-3.5 py-2 shadow-raised sm:block">
              <span className="font-mono text-[0.75rem] uppercase tracking-[0.14em] text-brand-200">
                {chainVerbs.slice(0, 5).join(" → ")}
              </span>
            </div>
          </MotionReveal>
        </div>

        {/* ── The fact row ───────────────────────────────────────────────
            Four facts about what kind of software this is, moving slowly
            enough to be read while they move. They are the same four the strip
            under the hero states in full; the row is the version a visitor
            catches without stopping, and the strip is the version they read
            once they have. */}
        <Reveal delay={200} className="mt-14 sm:mt-16">
          <Marquee duration={52} className="border-y border-line/50 py-3.5">
            {[
              t("Windows desktop software — installed, not a browser tab"),
              t("Local SQLite: your data stays on your machine"),
              t("No account to create and nothing to log into"),
              `${site.locales} ${t("languages, translated rather than labelled")}`,
              t("2D nesting, sheet yield and row-level traceability"),
              `${site.testCount.toLocaleString("en-US")} ${t("tests in the build")}`,
            ].map((item) => (
              <MarqueeItem
                key={item}
                className="font-mono text-2xs font-semibold uppercase tracking-[0.16em] text-text-muted"
              >
                {item}
              </MarqueeItem>
            ))}
          </Marquee>
        </Reveal>
      </div>

      {/* ══ Brand banner ══════════════════════════════════════════════════
          The repository's own front-page artwork: `README_ASSETS/hero.png` is
          what the GitHub README shows and what a shared link unfurls as. It is
          drawn from the product by `scripts/render-hero-banner.mts` — the
          window on it is the Optimization module on its 2D View tab, with the
          plate nested at render time by the same packer the previews above use,
          and every figure on it read back out of `src/data`.

          Presented full-bleed rather than as a card inside the page column: it
          is the one 3:1 showcase on the site, and it reads as a product
          showcase at width, not as a figure inside an article. Frameless too —
          a border drawn around artwork that has its own edge and its own violet
          bed only adds a second, weaker frame around the first.

          It stops at 1920 px because that is what it is drawn at: past that the
          artwork would be enlarged rather than extended. `sizes="100vw"` is
          what lets the browser take the largest file the layout actually asks
          for — it used to claim 1200 px, so it picked a smaller one and
          stretched it, which is where "the banner looks blurred" came from. */}
      <Reveal delay={120} className="mt-16 sm:mt-20">
        <figure className="hero-banner-parallax">
          <div className="mx-auto w-full max-w-[1920px]">
            <div className="relative aspect-[3/1] w-full">
              <Image
                src={brandBanner.source}
                alt={t("ProGlass Automation brand banner: the application's Optimization module on its 2D View tab, showing one nested plate with every piece labelled by size and cut order, the plate's cut schedule, the run's utilisation, and the product mark beside the line 'Glass business management, built around your workflow.'")}
                fill
                sizes="(max-width: 1920px) 100vw, 1920px"
                quality={92}
                className="object-cover"
                priority
              />
            </div>

            {/* The caption sits below the artwork, not on it. The banner's own
                lower-left carries the headline, the figures and the workflow
                line, so anything placed over the picture covers something that
                was drawn deliberately. */}
            <figcaption className="flex flex-wrap items-baseline justify-between gap-x-6 gap-y-1 border-t border-line/60 px-5 py-3 sm:px-8">
              <span className="mono-label">{t("README_ASSETS/hero.png · drawn from the application’s own data")}</span>
              <span className="text-xs text-text-faint font-medium">
                {t("Optimization · 2D View · {version} · sample data, labelled as such", { version: site.version })}
              </span>
            </figcaption>
          </div>
        </figure>
      </Reveal>
    </HeroField>
  );
}

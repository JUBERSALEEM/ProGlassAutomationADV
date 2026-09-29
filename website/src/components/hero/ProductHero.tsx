import Image from "next/image";
import { ArrowRight, ButtonLink } from "@/components/ui/Button";
import { AppFrame } from "@/components/product/AppFrame";
import { LiveSurface } from "@/components/product/LiveSurface";
import { Reveal } from "@/components/ui/Reveal";
import { chainVerbs } from "@/data/workflows";
import { site } from "@/lib/site";

/**
 * Homepage hero.
 *
 * The headline is the product's own positioning: the README calls ProGlass an
 * "industrial glass-fabrication ERP", which is accurate but is jargon to the
 * person who actually runs a glass shop. The first line therefore says what it
 * is for, and the second line names the chain the application implements.
 */
export function ProductHero() {
  return (
    <section className="relative overflow-hidden pb-12 pt-14 sm:pb-16 sm:pt-20 lg:pb-24 lg:pt-24">
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
      <div aria-hidden="true" className="pointer-events-none absolute inset-0 overflow-hidden">
        <Image
          src="/images/brand/hero-backdrop-3840.png"
          alt=""
          fill
          priority
          sizes="100vw"
          quality={92}
          className="object-cover object-right"
        />
        <div className="absolute inset-0 bg-gradient-to-r from-ink-950/80 via-ink-950/35 to-transparent" />
        <div className="absolute inset-0 bg-gradient-to-t from-ink-950 via-transparent to-ink-950/45" />
      </div>

      {/* Ambient light. Four very wide pools, drifting slowly. */}
      <div aria-hidden="true" className="pointer-events-none absolute inset-0 overflow-hidden">
        <div className="animate-drift-slow absolute -top-40 left-[8%] h-[34rem] w-[34rem] rounded-full bg-brand-500/16 blur-[120px]" />
        <div className="absolute -top-24 right-[4%] h-[26rem] w-[26rem] rounded-full bg-glass-cyan/10 blur-[110px]" />
        <div className="absolute bottom-0 left-[38%] h-[20rem] w-[20rem] rounded-full bg-glass-rose/8 blur-[120px]" />
        <div className="absolute inset-x-0 top-0 h-px bg-gradient-to-r from-transparent via-brand-400/40 to-transparent" />
      </div>

      <div className="shell relative">
        <div className="grid items-center gap-12 lg:grid-cols-[minmax(0,1fr)_minmax(0,1.06fr)] lg:gap-14">
          <div>
            <Reveal>
              <p className="eyebrow mb-5 flex flex-wrap items-center gap-x-3 gap-y-2">
                <span aria-hidden="true" className="h-px w-8 bg-brand-400/60" />
                ProGlass Automation
                <span className="font-mono text-text-faint">v{site.version}</span>
              </p>
            </Reveal>

            <Reveal delay={70}>
              <h1 className="text-[2.1rem] font-semibold leading-[1.06] tracking-tight text-text-primary sm:text-[2.75rem] lg:text-[3.25rem]">
                Glass business management,
                <span className="text-gradient block">built around your workflow.</span>
              </h1>
            </Reveal>

            <Reveal delay={140}>
              <p className="lede mt-6 max-w-xl">
                A Windows desktop application for glass fabrication: price the job, cut the stock, track what left the
                yard, and invoice it — without re-keying the same glass twice.
              </p>
            </Reveal>

            <Reveal delay={200}>
              <div className="mt-8 flex flex-wrap items-center gap-3">
                <ButtonLink href="/contact" size="lg" trailing={<ArrowRight />}>
                  Request a Demo
                </ButtonLink>
                <ButtonLink href="/features" variant="secondary" size="lg">
                  Explore ProGlass
                </ButtonLink>
              </div>
            </Reveal>

            <Reveal delay={260}>
              <ul className="mt-9 flex flex-wrap items-center gap-x-5 gap-y-2.5">
                {[
                  "Windows desktop",
                  "Local SQLite data",
                  "No account to create",
                  `${site.locales} languages`,
                ].map((item) => (
                  <li key={item} className="flex items-center gap-2 text-xs text-text-muted">
                    <span aria-hidden="true" className="h-1 w-1 rounded-full bg-brand-400/70" />
                    {item}
                  </li>
                ))}
              </ul>
            </Reveal>
          </div>

          {/* ── Layered product presentation ───────────────────────── */}
          <Reveal delay={120} variant="scale" className="relative">
            <div aria-hidden="true" className="pointer-events-none absolute -inset-8 -z-10">
              <div className="absolute left-1/2 top-1/2 h-[26rem] w-[34rem] -translate-x-1/2 -translate-y-1/2 rounded-full bg-brand-500/12 blur-[100px]" />
            </div>

            <AppFrame
              title="Dashboard — Overview"
              railActive="Dashboard"
              className="lg:translate-x-2"
              note={
                <>
                  <span className="font-mono text-2xs uppercase tracking-[0.16em] text-brand-300">
                    Interface map
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

            {/* Floating detail card — real fields from the proforma Step 9. */}
            <div className="menu-card absolute -bottom-6 -left-3 w-56 rounded-panel p-4 shadow-raised sm:-left-8 sm:w-64">
              <p className="font-mono text-2xs uppercase tracking-[0.16em] text-text-faint font-medium">Proforma · Step 9</p>
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
            </div>

            {/* Floating chain chip. */}
            <div className="menu-card absolute -right-2 -top-5 hidden rounded-full px-3.5 py-2 shadow-raised sm:block">
              <span className="font-mono text-[0.75rem] uppercase tracking-[0.14em] text-brand-200">
                {chainVerbs.slice(0, 5).join(" → ")}
              </span>
            </div>
          </Reveal>
        </div>

        {/* ── Brand banner: the repository's own front-page artwork ──
            `README_ASSETS/hero.png` is what the GitHub README shows and what a
            shared link unfurls as. It is drawn from the product by
            `scripts/render-hero-banner.mts`: the window on it is this page's
            own hero preview — the live dashboard — and the figures on it come
            out of `src/data` rather than out of an illustrator's imagination. */}
        <Reveal delay={120} className="mt-16 sm:mt-20">
          <div className="plate overflow-hidden">
            <div className="relative aspect-[3/1] w-full">
              <Image
                /* The 3840 px master, not the 1920 px README file. This plate is
                   drawn edge to edge, so a retina display asks for it at roughly
                   3584 device pixels on a 1920 px screen; the 1920 px file was
                   being enlarged to meet that, which is what made it look soft.
                   `sizes="100vw"` is what lets the browser pick the right one —
                   it used to claim 1200 px, so it asked for too small a file. */
                src="/images/brand/proglass-hero-banner-3840.png"
                alt="ProGlass Automation brand banner: the application's Overview dashboard showing open inquiries, proformas raised, jobs in production and pieces ready to dispatch, beside the product mark, the proforma's profit and loss snapshot and the line 'Glass business management, built around your workflow.'"
                fill
                sizes="100vw"
                quality={92}
                className="object-cover"
                priority
              />
              {/*
                No veil over the artwork. It carries a cutting plan and a table
                of glass references down to its lower edge, and any gradient
                laid over it dims exactly the figures it was drawn to show — on
                a dark image a scrim reads as haze, which is half of what
                "the banner looks blurred" meant. The caption sits in its own
                opaque chip instead, so contrast never depended on the veil.
              */}
              {/* Bottom-right, in the one part of the composition that is empty:
                  the banner's own lower-left carries the headline, the figures
                  and the workflow line, and a chip there would cover them. */}
              <p className="absolute bottom-3 right-3 rounded-full border border-line-strong/60 bg-ink-950/80 px-3 py-1.5 font-mono text-2xs uppercase tracking-[0.18em] text-text-secondary sm:bottom-4 sm:right-4">
                The product&rsquo;s own screens · README_ASSETS/hero.png
              </p>
            </div>
          </div>
        </Reveal>
      </div>
    </section>
  );
}

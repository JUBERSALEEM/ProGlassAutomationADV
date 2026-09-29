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
          A photographic bed of stacked glass panes, heavily veiled so it reads
          as texture behind the headline rather than as a picture on its own.
          Decorative: it is not a photograph of any particular workshop, so it
          is hidden from assistive technology and carries no caption. */}
      <div aria-hidden="true" className="pointer-events-none absolute inset-0 overflow-hidden">
        <Image
          src="/images/brand/banner-glass-fabrication.jpg"
          alt=""
          fill
          priority
          sizes="100vw"
          className="object-cover object-right opacity-[0.34]"
        />
        <div className="absolute inset-0 bg-gradient-to-r from-ink-950 via-ink-950/85 to-ink-950/45" />
        <div className="absolute inset-0 bg-gradient-to-t from-ink-950 via-transparent to-ink-950/70" />
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
                  <span className="font-mono text-2xs uppercase tracking-[0.16em] text-brand-300/80">
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
              <p className="font-mono text-2xs uppercase tracking-[0.16em] text-text-faint">Proforma · Step 9</p>
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
              <span className="font-mono text-[0.625rem] uppercase tracking-[0.14em] text-brand-200">
                {chainVerbs.slice(0, 5).join(" → ")}
              </span>
            </div>
          </Reveal>
        </div>

        {/* ── Brand banner: a real asset from the repository ───────── */}
        <Reveal delay={120} className="mt-16 sm:mt-20">
          <div className="plate overflow-hidden">
            <div className="relative aspect-[3/1] w-full">
              <Image
                src="/images/brand/proglass-hero-banner-1920.png"
                alt="ProGlass Automation brand banner: a glass cutting table, a rack of sheets and the workflow steps from quotation to delivery"
                fill
                sizes="(max-width: 1024px) 100vw, 1200px"
                className="object-cover"
                priority
              />
              <div
                aria-hidden="true"
                className="absolute inset-0 bg-gradient-to-t from-ink-950 via-ink-950/25 to-transparent"
              />
              <p className="absolute bottom-4 left-4 right-4 font-mono text-2xs uppercase tracking-[0.18em] text-text-secondary sm:bottom-5 sm:left-6">
                Brand asset shipped inside the application · README_ASSETS/hero.png
              </p>
            </div>
          </div>
        </Reveal>
      </div>
    </section>
  );
}

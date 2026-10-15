"use client";

import { useCallback, useMemo, useRef, useState } from "react";
import Image from "next/image";
import { cn } from "@/lib/cn";
import { focusModules, railModules, type FocusModule } from "@/lib/featuresModules";
import { Tilt } from "@/lib/useTilt";
import { glass3d } from "@/data/assets";
import { modules } from "@/data/modules";
import { LiveSurface } from "@/components/product/LiveSurface";
import { LocaleLink as Link } from "@/components/ui/LocaleLink";

/**
 * The Features showcase: one stack, one rail.
 *
 * The homepage's biggest product moment used to be one huge grid of equal
 * cards. This replaces it with a single exhibit that behaves like the
 * product itself feels: dense, precise, and layered. Four focus modules
 * (Optimization, Proforma, Sheet Store, Delivery Note) form a *stacked*
 * exhibit — a composition of floating glass panes arranged in depth. The
 * lead pane carries the module's own interface (the same honest, lab-built
 * `AppFrame` + `LiveSurface` preview the hero and /developer ship — not a
 * screen capture), and the panes behind it carry the materials the module
 * prices, cuts, stores and ships: the four build-up renders the site
 * already shows on /screenshots (SGU, DGU, LAM and DGU+LAM,
 * `src/data/assets.ts`). The visitor walks the set with the seven-dot
 * index; clicking a dot crossfades the whole stage to the next module with
 * the panes handing their positions over in order.
 *
 * Everything is evidence-only — there is no invented interface and no
 * invented figure anywhere in this exhibit. And auto-advance is
 * deliberately absent: a stage that moves on its own rewards nobody, and
 * the prospective buyer of ERP software came to *look*, not to chase.
 *
 * Reduced motion: the crossfade shortens to a plain fade, the panes sit
 * still, and tilt never engages (the hook checks the same media queries
 * the CSS does).
 */


/** Build-up identity and its render, in the exhibit's stacking order. */
const MATERIALS = [
  { key: "SGU", render: glass3d.sgu },
  { key: "LAM", render: glass3d.lam },
  { key: "DGU", render: glass3d.dgu },
  { key: "DGU + LAM", render: glass3d.dguLam },
] as const;

function useActiveIndex(count: number) {
  const [active, setActive] = useState(0);
  const select = useCallback((i: number) => setActive(((i % count) + count) % count), [count]);
  return { active, select };
}

/** The rotating ring mark that sits at the top of the stage header. */
function Ring({ className }: { className?: string }) {
  return (
    <svg aria-hidden="true" viewBox="0 0 24 24" className={cn("mat-ring", className)}>
      <circle cx="12" cy="12" r="9" fill="none" stroke="currentColor" strokeWidth="1" strokeDasharray="4 3" />
      <circle cx="12" cy="12" r="4.5" fill="none" stroke="currentColor" strokeWidth="1" opacity="0.6" />
    </svg>
  );
}

/**
 * Lead pane: the module's own interface preview, exactly as the site
 * renders the module elsewhere — AppFrame chrome around LiveSurface, fed
 * by the module's declared capabilities. `compact` keeps the fake window
 * legible inside the exhibit.
 */
function ModuleLeadPane({ modId }: { modId: string }) {
  const mod = modules.find((m) => m.id === modId) ?? null;
  if (!mod) return null;
  return (
    <div className="mpane mpane--lead" aria-hidden="true">
      <div className="mpane__glass mpane__glass--lead">
        <LiveSurface
          moduleKey={mod.moduleKey}
          name={mod.name}
          capabilities={mod.capabilities}
          compact
        />
      </div>
    </div>
  );
}

/** Material pane: one of the four build-up renders, labelled by build-up. */
function MaterialPane({
  index,
  modId,
  eager,
}: {
  index: 0 | 1 | 2;
  modId: string;
  eager?: boolean;
}) {
  const rotation = useMemo(() => {
    /* Each module starts the deck at a different material, so walking the
       index shows all four build-ups around the stage rather than the same
       two behind every interface. */
    const start = focusModules.findIndex((f) => f.id === modId);
    return [0, 1, 2].map((i) => MATERIALS[(start + i) % MATERIALS.length]!);
  }, [modId]);
  const mat = rotation[index]!;
  return (
    <div className={cn("mpane", `mpane--${index + 1}`)} aria-hidden="true">
      <div className="mpane__glass mpane__glass--material">
        <Image
          src={mat.render.src}
          alt=""
          width={360}
          height={240}
          sizes="360px"
          loading={eager ? "eager" : "lazy"}
          className="mpane__material"
        />
      </div>
      <span className="mpane__badge font-mono" aria-hidden="true">
        {mat.key}
      </span>
    </div>
  );
}

export function FeaturesShowcase({
  names,
  captions,
  strings,
}: {
  /** Localized module names, focus-first — index i answers dot i. */
  names: string[];
  /** Localized one-line captions keyed by module id. */
  captions: Record<string, string>;
  /** Localized chrome: eyebrow, headline, aria labels, link text. */
  strings: {
    eyebrow: string;
    headlineFirst: string;
    indexLabel: string;
    railLabel: string;
    readModule: string;
  };
}) {
  const count = 7;
  const stageCount = focusModules.length;
  const { active, select } = useActiveIndex(count);
  const onStage = active < stageCount;

  const stageId = onStage ? focusModules[active]!.id : null;
  const railSpec = !onStage ? railModules[active - stageCount] : null;

  const railRef = useRef<HTMLDivElement | null>(null);

  const cap = (id: string) => captions[id] ?? "";
  const nameOf = (m: FocusModule) => {
    const idx = [...focusModules, ...railModules].findIndex((f) => f.id === m.id);
    return names[idx] ?? m.short;
  };
  const activeLabel = onStage ? nameOf(focusModules[active]!) : railSpec ? nameOf(railSpec) : null;
  const dots = useMemo(() => Array.from({ length: count }, (_, i) => i), [count]);

  const keyNav = (e: React.KeyboardEvent<HTMLDivElement>) => {
    if (e.key === "ArrowRight" || e.key === "ArrowDown") {
      e.preventDefault();
      select(active + 1);
    } else if (e.key === "ArrowLeft" || e.key === "ArrowUp") {
      e.preventDefault();
      select(active - 1);
    } else if (e.key === "Home") {
      e.preventDefault();
      select(0);
    } else if (e.key === "End") {
      e.preventDefault();
      select(count - 1);
    }
  };

  return (
    <div className="mat-stage-root">
      <div className="mat-stage grid gap-6 lg:grid-cols-[1.08fr_1fr] lg:items-stretch">
        {/* Left: the plate — heading, caption, and the seven-dot index. */}
        <div className="mat-stage__plates relative z-10 flex flex-col justify-between gap-5 card-pad">
          <div>
            <div className="flex items-center gap-3">
              <Ring className="h-6 w-6 text-brand-400" />
              <p className="mono-label">{strings.eyebrow}</p>
            </div>
            <h3 className="mt-3 text-balance text-2xl font-semibold tracking-tight text-text-primary sm:text-[1.7rem]">
              {strings.headlineFirst}{" "}
              <span className="text-gradient-rich">{activeLabel}</span>
            </h3>
            <p className="mt-3 max-w-md text-sm leading-relaxed text-text-muted">
              {stageId ? cap(stageId) : railSpec ? cap(railSpec.id) : ""}
            </p>
          </div>

          <nav
            role="tablist"
            aria-label={strings.indexLabel}
            className="mat-index flex select-none flex-wrap items-center gap-2"
            onKeyDown={keyNav}
          >
            {dots.map((i) => {
              const label =
                i < stageCount ? nameOf(focusModules[i]!) : nameOf(railModules[i - stageCount]!);
              return (
                <button
                  key={i}
                  type="button"
                  role="tab"
                  aria-selected={active === i}
                  aria-label={label}
                  title={label}
                  data-dot={i}
                  onClick={() => select(i)}
                  className={cn("mat-dot", active === i && "mat-dot--active")}
                >
                  <span className="mat-dot__pip" aria-hidden="true" />
                </button>
              );
            })}
            <span className="mat-index__progress" aria-hidden="true">
              <span style={{ width: `${((active + 1) / count) * 100}%` }} />
            </span>
            <span className="ml-2 font-mono text-2xs font-semibold tracking-[0.2em] text-text-faint">
              {String(active + 1).padStart(2, "0")} – {String(count).padStart(2, "0")}
            </span>
          </nav>
        </div>

        {/* Right: the visual field — the stack, then the rail beneath it. */}
        <div className="mat-stage__field relative min-h-[340px] sm:min-h-[420px] lg:min-h-[480px]">
          {onStage ? (
            <Tilt
              key={stageId}
              max={4.5}
              as="figure"
              className="mat-exhibit absolute inset-x-4 inset-y-0 bottom-16 sm:inset-x-8"
            >
              <div className="mat-exhibit__aura" aria-hidden="true" />
              <MaterialPane index={2} modId={stageId!} />
              <MaterialPane index={1} modId={stageId!} />
              <MaterialPane index={0} modId={stageId!} />
              <ModuleLeadPane modId={stageId!} />
              <figcaption className="mpane__stage-caption">
                <span className="font-mono text-2xs font-semibold uppercase tracking-[0.2em] text-brand-200">
                  {nameOf(focusModules[active]!)}
                </span>
                <span className="text-xs font-medium text-text-muted">{cap(stageId!)}</span>
              </figcaption>
            </Tilt>
          ) : railSpec ? (
            /* Rail selection: an honest preview card for the named module —
               it previews, it does not pretend to be the stage. */
            <figure key={railSpec.id} className="mat-railview absolute inset-x-4 inset-y-0 bottom-16 sm:inset-x-8">
              <div className="mat-railview__inner">
                <p className="font-mono text-2xs font-semibold uppercase tracking-[0.2em] text-brand-200">
                  {nameOf(railSpec)}
                </p>
                <p className="mt-2 max-w-sm text-sm leading-relaxed text-text-secondary">
                  {cap(railSpec.id)}
                </p>
                <Link
                  href={railSpec.pageHref}
                  className="link-grow mt-4 inline-flex items-center gap-1.5 text-sm font-semibold text-brand-300 hover:text-brand-200"
                >
                  {strings.readModule}
                  <svg viewBox="0 0 16 16" aria-hidden="true" className="h-3.5 w-3.5">
                    <path d="M3 8h10M9 4l4 4-4 4" fill="none" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" />
                  </svg>
                </Link>
              </div>
            </figure>
          ) : null}

          {/* The compact rail — the three modules off the stage. */}
          <div
            ref={railRef}
            role="tablist"
            aria-label={strings.railLabel}
            className="mat-rail absolute inset-x-2 bottom-0 flex gap-2 overflow-x-auto px-2 pb-1 pt-3 sm:justify-center"
          >
            {railModules.map((m, i) => (
              <button
                key={m.id}
                type="button"
                data-i={i}
                role="tab"
                aria-selected={active === stageCount + i}
                onClick={() => select(stageCount + i)}
                className={cn("mat-rail__chip", active === stageCount + i && "mat-rail__chip--active")}
              >
                {nameOf(m)}
              </button>
            ))}
          </div>
        </div>
      </div>
    </div>
  );
}

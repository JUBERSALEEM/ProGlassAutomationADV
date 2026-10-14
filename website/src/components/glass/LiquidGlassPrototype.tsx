"use client";

/**
 * Liquid Glass PROTOTYPE (Phase 1 of docs/LIQUID_GLASS_PROTOTYPE.md).
 *
 * One self-contained stage demonstrating the interaction grammar of the
 * two references — floating pill nav, pointer-gloss press/release
 * buttons, a panel that morphs open / selected / dismissed, layered
 * transparency tiers, edge light, an SVG-distortion lens, and a
 * coordinated light/dark scene. Everything lives under `.lgx`
 * (liquid-prototype.css, imported by the prototype route only); no
 * production component or engine rule is touched.
 *
 * Motion: the installed `motion` package (layoutId indicator,
 * AnimatePresence morphs, springs, drag). No WebGL — see the tech
 * evaluation in the checklist for why.
 */

import { useRef, useState } from "react";
import { AnimatePresence, motion, useReducedMotion } from "motion/react";
import { cn } from "@/lib/cn";

const NAV = [
  { id: "overview", label: "Overview" },
  { id: "orders", label: "Job orders" },
  { id: "production", label: "Production" },
  { id: "reports", label: "Reports" },
] as const;
type NavId = (typeof NAV)[number]["id"];

const MODULES = [
  { id: "quoting", name: "Quoting", metric: "128 quotes", detail: "Quoting is selected — estimates carry glass, labour and hardware in one pane." },
  { id: "cutting", name: "Cutting optimization", metric: "96.4% yield", detail: "Cutting optimization is selected — sheets nest tightest-first across the floor." },
  { id: "inventory", name: "Inventory", metric: "42 racks", detail: "Inventory is selected — every lite is traced from rack to remnant." },
] as const;

const ROWS: Record<NavId, { title: string; rows: { label: string; sub: string; value: string }[] }> = {
  overview: {
    title: "Today at the factory",
    rows: [
      { label: "Tempering queue", sub: "Furnace 2 · next batch", value: "14 lites" },
      { label: "Deliveries out", sub: "Two vans loaded", value: "6 drops" },
      { label: "Quotes awaiting reply", sub: "Oldest 2 days", value: "9" },
    ],
  },
  orders: {
    title: "Job orders in motion",
    rows: [
      { label: "JO-2481 · Marina Tower", sub: "Cutting → edging", value: "62%" },
      { label: "JO-2484 · Villa Amwaj", sub: "Tempering", value: "81%" },
      { label: "JO-2490 · Clinic fit-out", sub: "Awaiting glass", value: "12%" },
    ],
  },
  production: {
    title: "Production pulse",
    rows: [
      { label: "CNC 1 · bevel run", sub: "On schedule", value: "34 lites" },
      { label: "Lamination line", sub: "PVB interlayer", value: "18 lites" },
      { label: "Remnant reuse", sub: "Recovered this week", value: "11.2 m²" },
    ],
  },
  reports: {
    title: "Reports, distilled",
    rows: [
      { label: "Yield vs last month", sub: "All cutting plans", value: "+2.1%" },
      { label: "On-time delivery", sub: "Rolling 30 days", value: "97%" },
      { label: "Breakage cost", sub: "Down after nesting v3", value: "−18%" },
    ],
  },
};

/** Pointer gloss: write the light's position as CSS vars on the element
 * itself — style properties only, never React state (the pointer-guard
 * contract). One handler serves every gloss surface on the stage. */
const gloss = (e: React.PointerEvent<HTMLElement>) => {
  const el = e.currentTarget;
  const box = el.getBoundingClientRect();
  if (box.width === 0 || box.height === 0) return;
  // pointer-guard-ok: writes --lgx-px/--lgx-py style vars on the target only
  el.style.setProperty("--lgx-px", `${(((e.clientX - box.left) / box.width) * 100).toFixed(1)}%`);
  el.style.setProperty("--lgx-py", `${(((e.clientY - box.top) / box.height) * 100).toFixed(1)}%`);
};

const spring = { type: "spring", stiffness: 320, damping: 30, mass: 0.9 } as const;

export function LiquidGlassPrototype() {
  const reduce = useReducedMotion();
  const [mode, setMode] = useState<"dark" | "light">("dark");
  const [nav, setNav] = useState<NavId>("overview");
  const [moduleId, setModuleId] = useState<(typeof MODULES)[number]["id"]>("quoting");
  const [panelOpen, setPanelOpen] = useState(true);
  const [rowIdx, setRowIdx] = useState(0);
  const [frost, setFrost] = useState(16);
  const [clarity, setClarity] = useState(0.55);
  const [distortion, setDistortion] = useState(22);
  const [note, setNote] = useState("Prototype ready — pick a tab, a module, or drag the lens.");
  const lensZone = useRef<HTMLDivElement>(null);

  const data = ROWS[nav];
  const activeModule = MODULES.find((m) => m.id === moduleId) ?? MODULES[0];
  const dur = reduce ? 0 : undefined;

  const pickNav = (id: NavId, label: string) => {
    setNav(id);
    setRowIdx(0);
    setPanelOpen(true);
    setNote(`${label} selected — the panel re-filled and opened.`);
  };

  return (
    <div
      className="lgx p-4 sm:p-7"
      data-mode={mode}
      style={{ "--lgx-frost": `${frost}px`, "--lgx-clarity": clarity } as React.CSSProperties}
    >
      {/* SVG distortion rig: one filter, decorative layers only. */}
      <svg aria-hidden="true" className="absolute h-0 w-0">
        <defs>
          <filter id="lgx-distort" x="-20%" y="-20%" width="140%" height="140%">
            <feTurbulence type="fractalNoise" baseFrequency="0.012 0.02" numOctaves="2" seed="7" result="noise" />
            <feDisplacementMap in="SourceGraphic" in2="noise" scale={distortion} xChannelSelector="R" yChannelSelector="G" />
          </filter>
        </defs>
      </svg>
      <div className="lgx-folds" aria-hidden="true" />
      <div className="lgx-orb lgx-orb-a" aria-hidden="true" />
      <div className="lgx-orb lgx-orb-b" aria-hidden="true" />
      <div className="lgx-orb lgx-orb-c" aria-hidden="true" />
      <div className="lgx-ring" aria-hidden="true" />

      {/* ── Floating liquid navigation ─────────────────────────────── */}
      <div className="sticky top-3 z-20 flex justify-center">
        <nav aria-label="Prototype" className="lgx-nav lgx-glass lgx-gloss" onPointerMove={gloss}>
          <span className="flex items-center gap-2 ps-2 pe-1 text-sm font-bold tracking-tight">
            <span className="flex h-7 w-7 items-center justify-center rounded-full bg-gradient-to-br from-violet-400 to-fuchsia-500 text-[0.65rem] text-white shadow-inner">PG</span>
            <span className="hidden sm:inline">ProGlass</span>
          </span>
          {NAV.map((item) => (
            <button
              key={item.id}
              type="button"
              className="lgx-navlink"
              data-active={nav === item.id}
              aria-current={nav === item.id ? "page" : undefined}
              onClick={() => pickNav(item.id, item.label)}
            >
              {nav === item.id && <motion.span layoutId="lgx-nav-pill" className="lgx-indicator" transition={reduce ? { duration: 0 } : spring} />}
              <span className="relative z-10">{item.label}</span>
            </button>
          ))}
          <button
            type="button"
            className="lgx-iconbtn lgx-glass lgx-gloss ms-1"
            aria-pressed={mode === "light"}
            aria-label="Toggle light and dark scene"
            title="Toggle light / dark scene"
            onPointerMove={gloss}
            onClick={() => { setMode(mode === "dark" ? "light" : "dark"); setNote(`Scene switched to ${mode === "dark" ? "light" : "dark"} — one material, both themes.`); }}
          >
            {mode === "dark" ? "☾" : "☀"}
          </button>
        </nav>
      </div>

      <div className="relative z-10 mx-auto mt-8 grid max-w-6xl gap-6 lg:grid-cols-[1.05fr_1fr]">
        {/* ── Left: actions + module tiles + layered sheets ────────── */}
        <div>
          <p className="text-[0.7rem] font-bold uppercase tracking-[0.22em] opacity-70">Liquid Glass · interactive prototype</p>
          <h2 className="mt-2 text-3xl font-bold leading-tight tracking-tight sm:text-4xl">Glass that answers<br />the hand, not just the eye.</h2>
          <p className="mt-3 max-w-md text-sm leading-relaxed" style={{ color: "var(--lgx-ink-2)" }}>
            Every control below is live: tabs slide, buttons gloss and yield under the press,
            the panel morphs, and the lens bends light. Tune the material, then judge it.
          </p>

          <div className="mt-5 flex flex-wrap items-center gap-3">
            <motion.button
              type="button"
              className="lgx-btn lgx-btn-primary lgx-gloss"
              onPointerMove={gloss}
              whileTap={reduce ? undefined : { scale: 0.955 }}
              transition={spring}
              aria-expanded={panelOpen}
              aria-controls="lgx-panel-body"
              onClick={() => { setPanelOpen(!panelOpen); setNote(panelOpen ? "Panel dismissed — it folded back to a pill." : "Panel opened — it bloomed from the pill."); }}
            >
              {panelOpen ? "Dismiss the panel" : "Open the panel"}
            </motion.button>
            <motion.button
              type="button"
              className="lgx-btn lgx-glass lgx-gloss"
              onPointerMove={gloss}
              whileTap={reduce ? undefined : { scale: 0.955 }}
              transition={spring}
              onClick={() => pickNav(nav === "production" ? "overview" : "production", nav === "production" ? "Overview" : "Production")}
            >
              Jump to {nav === "production" ? "Overview" : "Production"}
            </motion.button>
            <span className="text-xs" style={{ color: "var(--lgx-ink-2)" }}>press <span className="lgx-kbd">Esc</span> inside the panel to fold it</span>
          </div>

          <div className="mt-7 grid gap-3 sm:grid-cols-3" role="group" aria-label="Modules">
            {MODULES.map((m) => (
              <motion.button
                key={m.id}
                type="button"
                className="lgx-tile lgx-glass lgx-gloss"
                data-selected={moduleId === m.id}
                aria-pressed={moduleId === m.id}
                onPointerMove={gloss}
                whileTap={reduce ? undefined : { scale: 0.965 }}
                transition={spring}
                onClick={() => { setModuleId(m.id); setPanelOpen(true); setNote(`${m.name} selected — the panel opened on it.`); }}
              >
                <span className="block text-sm font-bold">{m.name}</span>
                <span className="mt-1 block text-xs" style={{ color: "var(--lgx-ink-2)" }}>{m.metric}</span>
              </motion.button>
            ))}
          </div>

          {/* Layered sheets — the references' overlapping panes. */}
          <div className="relative mt-8 hidden h-40 sm:block" aria-hidden="true">
            <div className="lgx-glass lgx-sheet left-0 top-0 w-72 rounded-3xl p-4">
              <p className="text-xs font-bold">Sheet · tier 1</p>
              <p className="mt-1 text-[0.7rem] opacity-75">Resting pane, calmer fill.</p>
            </div>
            <div className="lgx-glass lgx-sheet left-44 top-12 w-72 rounded-3xl p-4" style={{ "--lgx-clarity": 0.9 } as React.CSSProperties}>
              <p className="text-xs font-bold">Sheet · tier 2 (overlapping)</p>
              <p className="mt-1 text-[0.7rem] opacity-75">Clearer, brighter edge — depth by layering.</p>
            </div>
          </div>
        </div>

        {/* ── Right: the morphing panel + material lab ─────────────── */}
        <div>
          <motion.section
            layout={!reduce}
            className={cn("lgx-panel lgx-glass lgx-gloss", !panelOpen && "lgx-panel-collapsed")}
            onPointerMove={gloss}
            aria-label="Morphing glass panel"
            onKeyDown={(e) => { if (e.key === "Escape" && panelOpen) { setPanelOpen(false); setNote("Panel dismissed with Escape."); } }}
          >
            <div className="flex items-center justify-between gap-3 p-4 sm:p-5">
              <div className="min-w-0">
                <p className="truncate text-sm font-bold">{panelOpen ? data.title : "Panel folded — tap Open to bloom it back"}</p>
                <p className="truncate text-xs" style={{ color: "var(--lgx-ink-2)" }}>{activeModule.detail}</p>
              </div>
              <button
                type="button"
                className="lgx-iconbtn lgx-glass lgx-gloss shrink-0"
                aria-expanded={panelOpen}
                aria-controls="lgx-panel-body"
                aria-label={panelOpen ? "Dismiss panel" : "Open panel"}
                onPointerMove={gloss}
                onClick={() => setPanelOpen(!panelOpen)}
              >
                {panelOpen ? "✕" : "+"}
              </button>
            </div>
            <AnimatePresence initial={false}>
              {panelOpen && (
                <motion.div
                  id="lgx-panel-body"
                  key="panel-body"
                  initial={{ height: 0, opacity: 0 }}
                  animate={{ height: "auto", opacity: 1 }}
                  exit={{ height: 0, opacity: 0 }}
                  transition={reduce ? { duration: 0 } : { ...spring, opacity: { duration: 0.22 } }}
                  className="overflow-hidden"
                >
                  <div className="px-4 pb-5 sm:px-5">
                    <div className="lgx-pillswitch lgx-glass" role="tablist" aria-label="Panel sections">
                      {NAV.slice(0, 3).map((item) => (
                        <button
                          key={item.id}
                          type="button"
                          role="tab"
                          aria-selected={nav === item.id}
                          className="lgx-navlink"
                          data-active={nav === item.id}
                          onClick={() => pickNav(item.id, item.label)}
                        >
                          {nav === item.id && <motion.span layoutId="lgx-panel-pill" className="lgx-indicator" transition={reduce ? { duration: 0 } : spring} />}
                          <span className="relative z-10">{item.label}</span>
                        </button>
                      ))}
                    </div>
                    <AnimatePresence mode="wait">
                      <motion.ul
                        key={nav}
                        initial={{ opacity: 0, y: reduce ? 0 : 8 }}
                        animate={{ opacity: 1, y: 0 }}
                        exit={{ opacity: 0, y: reduce ? 0 : -8 }}
                        transition={{ duration: dur ?? 0.24 }}
                        className="mt-4 space-y-2"
                      >
                        {data.rows.map((row, i) => (
                          <li key={row.label}>
                            <button
                              type="button"
                              className="lgx-row lgx-gloss"
                              data-selected={rowIdx === i}
                              aria-pressed={rowIdx === i}
                              onPointerMove={gloss}
                              onClick={() => { setRowIdx(i); setNote(`Row selected: ${row.label} — raised as a glass pill.`); }}
                            >
                              <span>
                                <span className="block text-sm font-semibold">{row.label}</span>
                                <span className="block text-xs" style={{ color: "var(--lgx-ink-2)" }}>{row.sub}</span>
                              </span>
                              <span className="text-sm font-bold">{row.value}</span>
                            </button>
                          </li>
                        ))}
                      </motion.ul>
                    </AnimatePresence>
                  </div>
                </motion.div>
              )}
            </AnimatePresence>
          </motion.section>

          {/* Material lab + distortion lens */}
          <div className="lgx-glass lgx-gloss mt-5 rounded-[1.75rem] p-5" onPointerMove={gloss}>
            <div className="flex items-center justify-between gap-3">
              <h3 className="text-sm font-bold">Material lab</h3>
              <span className="text-[0.7rem]" style={{ color: "var(--lgx-ink-2)" }}>drag the lens over the swatches</span>
            </div>
            <div ref={lensZone} className="relative mt-4 h-40 overflow-hidden rounded-2xl border border-white/20">
              <div className="lgx-lens-swatch" aria-hidden="true" />
              <motion.div
                className="lgx-lens"
                drag={!reduce}
                dragConstraints={lensZone}
                dragElastic={0.14}
                dragMomentum={false}
                initial={{ x: 24, y: 18 }}
                role="img"
                aria-label="Draggable distortion lens demonstrating simulated optical refraction"
              >
                <div className="lgx-lens-core">
                  <div className="lgx-lens-swatch" style={{ filter: "url(#lgx-distort)" }} aria-hidden="true" />
                </div>
              </motion.div>
            </div>
            <div className="mt-4 grid gap-3 sm:grid-cols-3">
              <label className="text-xs font-semibold">
                Frost · {frost}px
                <input className="lgx-input mt-1" type="range" min={4} max={32} step={1} value={frost} aria-label="Frost blur" onChange={(e) => setFrost(Number(e.target.value))} />
              </label>
              <label className="text-xs font-semibold">
                Clarity · {Math.round(clarity * 100)}%
                <input className="lgx-input mt-1" type="range" min={0} max={1} step={0.01} value={clarity} aria-label="Clarity" onChange={(e) => setClarity(Number(e.target.value))} />
              </label>
              <label className="text-xs font-semibold">
                Distortion · {distortion}
                <input className="lgx-input mt-1" type="range" min={0} max={36} step={1} value={distortion} aria-label="Distortion intensity" onChange={(e) => setDistortion(Number(e.target.value))} />
              </label>
            </div>
          </div>

          <p aria-live="polite" className="mt-4 text-xs leading-relaxed" style={{ color: "var(--lgx-ink-2)" }}>{note}</p>
        </div>
      </div>
    </div>
  );
}

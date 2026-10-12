import type { Metadata } from "next";
import { Container, Section } from "@/components/ui/Container";
import { translatorFor } from "@/i18n/server";

/*
 * The crystal catalog — every face the crystal engine owns, on a single
 * screen: the lift, the forms, the chips and seams, the light, and the
 * bench's traveled examples. This route is a review tool, not a page: it
 * is excluded from the sitemap and marked noindex, so a search engine
 * never suggests it and a visitor only reaches it by URL.
 *
 * Rule from the roadmap: selectors live in the engine file; this route
 * declares intent only. The one deliberate exception is the demo row —
 * the three inline --glass-*-mult adjustments, which exist precisely to
 * prove that the dial's reach is total. They carry no shade of their own;
 * they multiply the family's own recipe.
 */

type PageProps = { params: Promise<{ locale: string }> };

export async function generateMetadata({ params }: PageProps): Promise<Metadata> {
  await translatorFor(params);
  return {
    title: "Crystal engine catalog",
    description: "Internal review surface: the glass lift, forms, chips and the material bench on one page.",
    robots: { index: false, follow: false },
  };
}

const demoStyle = {
  frost06: { "--glass-frost-mult": 0.6 } as React.CSSProperties,
  body14: { "--glass-body-mult": 1.4 } as React.CSSProperties,
  glint00: { "--glass-glint-mult": 0 } as React.CSSProperties,
  glint20: { "--glass-glint-mult": 2 } as React.CSSProperties,
};

export default async function CrystalCatalogPage({ params }: PageProps) {
  const t = await translatorFor(params);
  return (
    <>
      <Section aria-labelledby="crystal-catalog" className="pt-10">
        <Container>
          <p className="mono-label">Internal · review surface</p>
          <h1 id="crystal-catalog" className="mt-2 text-3xl font-bold tracking-tight text-text-primary sm:text-4xl">
            {t("Crystal engine catalog")}
          </h1>
          <p className="mt-3 max-w-2xl text-sm leading-relaxed text-text-muted">
            {t("Every face the crystal engine owns: the lift on utility carriers, the forms, the chips and table seams, the corner light, and the bench's traveled examples. Toggle theme and family and the entire page must move as one — nothing below is a local override.")}
          </p>
        </Container>
      </Section>

      {/* ── 1 · Surfaces: the common card grammar under glass ──────── */}
      <Section aria-labelledby="cat-surfaces" className="!py-6">
        <Container>
          <h2 id="cat-surfaces" className="card-title mb-4">{t("Surfaces")}</h2>
          <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
            <div className="surface p-5">
              <p className="mono-label">.surface</p>
              <p className="mt-2 text-sm text-text-muted">{t("The resting card — the family's resting sheet, tri-token fill by mode.")}</p>
            </div>
            <div className="surface-raised p-5">
              <p className="mono-label">.surface-raised</p>
              <p className="mt-2 text-sm text-text-muted">{t("One tier closer — tier-2 fill, the same grammar at altitude.")}</p>
            </div>
            <div className="glass-primary p-5">
              <p className="mono-label">.glass-primary</p>
              <p className="mt-2 text-sm">{t("The accent sheet — the brand band carried by the material.")}</p>
              <div className="mt-4 flex gap-2">
                <button type="button" className="btn btn-secondary btn-sm">{t("Secondary")}</button>
              </div>
            </div>
            <div className="stat-tile p-5">
              <p className="mono-label">.stat-tile</p>
              <p className="mt-1 text-2xl font-bold text-text-primary">42</p>
              <p className="text-sm text-text-muted">{t("The assay tile")}</p>
            </div>
          </div>
        </Container>
      </Section>

      {/* ── 2 · The lift (§C1) ─────────────────────────────────────── */}
      <Section aria-labelledby="cat-lift" className="!py-6">
        <Container>
          <h2 id="cat-lift" className="card-title mb-4">{t("The lift — utility carriers in glass (§C1)")}</h2>
          <p className="mb-4 max-w-2xl text-xs leading-relaxed text-text-faint">
            {t("Ninety-four utility-carry sites the clay grammar never saw: chips, wells, table wells, inline code, info cards — lifted uniformly by shape, not by name.")}
          </p>
          <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
            <div className="bg-ink-900/60 rounded-panel p-5 ring-1 ring-line/50">
              <p className="mono-label !text-[0.6rem]">bg-ink-900/60</p>
              <p className="mt-2 text-sm text-text-secondary">{t("A well — the darkest utility tier.")}</p>
            </div>
            <div className="bg-ink-800/60 rounded-panel p-5 ring-1 ring-line/50">
              <p className="mono-label !text-[0.6rem]">bg-ink-800/60</p>
              <p className="mt-2 text-sm text-text-secondary">{t("A reading well — tier two.")}</p>
            </div>
            <div className="bg-white/60 rounded-panel p-5 ring-1 ring-line/50">
              <p className="mono-label !text-[0.6rem]">bg-white/60</p>
              <p className="mt-2 text-sm text-text-secondary">{t("A paper well — day-color.")}</p>
            </div>
            <div className="bg-glass/70 rounded-panel p-5 ring-1 ring-line/50">
              <p className="mono-label !text-[0.6rem]">bg-glass</p>
              <p className="mt-2 text-sm text-text-secondary">{t("The glass carrier itself.")}</p>
            </div>
          </div>
          <p className="mt-4 text-sm text-text-muted">
            {t("Inline code reads through the same lift:")}{" "}
            <code className="bg-ink-800/60 rounded px-1.5 py-0.5 text-[0.85em] text-text-primary">limit(90°, 0.35)</code>{" "}
            {t("— its utility backgrounds are the pedal of this engine.")}
          </p>
        </Container>
      </Section>

      {/* ── 3 · Forms (§C2) ────────────────────────────────────────── */}
      <Section aria-labelledby="cat-forms" className="!py-6">
        <Container>
          <h2 id="cat-forms" className="card-title mb-4">{t("Forms — field wells (§C2)")}</h2>
          <div className="grid gap-4 sm:grid-cols-3">
            <label className="block text-sm">
              <span className="mono-label !text-[0.6rem]">input</span>
              <input
                type="text"
                placeholder={t("A field carved into the glass…")}
                className="mt-1.5 w-full rounded-input bg-ink-800/50 px-3 py-2 text-sm text-text-primary ring-1 ring-line/60"
              />
            </label>
            <label className="block text-sm">
              <span className="mono-label !text-[0.6rem]">textarea</span>
              <textarea
                rows={1}
                placeholder={t("The well's own fill, the page behind it…")}
                className="mt-1.5 w-full rounded-input bg-ink-800/50 px-3 py-2 text-sm text-text-primary ring-1 ring-line/60"
              />
            </label>
            <label className="block text-sm">
              <span className="mono-label !text-[0.6rem]">select</span>
              <select className="mt-1.5 w-full rounded-input bg-ink-800/50 px-3 py-2 text-sm text-text-primary ring-1 ring-line/60">
                <option>{t("A closed field")}</option>
              </select>
            </label>
          </div>
        </Container>
      </Section>

      {/* ── 4 · Chips + table seams (§C3) ──────────────────────────── */}
      <Section aria-labelledby="cat-chips" className="!py-6">
        <Container>
          <h2 id="cat-chips" className="card-title mb-4">{t("Chips and table seams (§C3)")}</h2>
          <div className="flex flex-wrap items-center gap-2">
            <span className="chip">{t("chip")}</span>
            <span className="chip chip-active">{t("chip · active")}</span>
            <span className="eyebrow-chip">{t("eyebrow-chip")}</span>
            <span className="eyebrow-chip">{t("brand-side label")}</span>
          </div>
          <div className="mt-5 overflow-hidden rounded-panel ring-1 ring-line/50">
            <table className="w-full text-sm">
              <thead>
                <tr>
                  <th className="px-4 py-2 text-left font-semibold">{t("Face")}</th>
                  <th className="px-4 py-2 text-left font-semibold">{t("Section")}</th>
                  <th className="px-4 py-2 text-right font-semibold">{t("Tier")}</th>
                </tr>
              </thead>
              <tbody className="text-text-secondary">
                <tr><td className="px-4 py-2">.surface</td><td className="px-4 py-2">§C1 + grammar</td><td className="px-4 py-2 text-right">1</td></tr>
                <tr><td className="px-4 py-2">field wells</td><td className="px-4 py-2">§C2</td><td className="px-4 py-2 text-right">2</td></tr>
                <tr><td className="px-4 py-2">thead row</td><td className="px-4 py-2">§C3</td><td className="px-4 py-2 text-right">2</td></tr>
              </tbody>
            </table>
          </div>
        </Container>
      </Section>

      {/* ── 5 · The light (§C5): static corners + the pointer's catch ── */}
      <Section aria-labelledby="cat-light" className="!py-6">
        <Container>
          <h2 id="cat-light" className="card-title mb-4">{t("The light")}</h2>
          <div className="grid gap-4 sm:grid-cols-2">
            <div className="bg-ink-900/60 rounded-panel p-6 ring-1 ring-line/50">
              <p className="mono-label !text-[0.6rem]">{t("the corner catch — static")}</p>
              <p className="mt-2 text-sm text-text-secondary">
                {t("One wash per corner on the light's own diagonal, the family dial multiplied in. Run the cursor over any sheet on this page — a single 260px catch follows it (the G5 §C5 grammar: one listener, CSS vars only, off under reduced motion).")}
              </p>
            </div>
            <div className="bg-ink-800/60 rounded-panel p-6 ring-1 ring-line/50">
              <p className="mono-label !text-[0.6rem]">{t("the view-chill")}</p>
              <p className="mt-2 text-sm text-text-secondary">
                {t("Sections cool as they leave the viewport (view-linked frost behind @supports). Scroll a touch and watch this band's own section: it gathers a whisper of ink as it exits — the §11.13 scroll grammar carried into glass.")}
              </p>
            </div>
          </div>
        </Container>
      </Section>

      {/* ── 6 · The bench's traveled examples (G4) ─────────────────── */}
      <Section aria-labelledby="cat-bench" className="!py-6">
        <Container>
          <h2 id="cat-bench" className="card-title mb-4">{t("The bench — traveled examples (G4)")}</h2>
          <p className="mb-4 max-w-2xl text-xs leading-relaxed text-text-faint">
            {t("Three moved dials, so the bench's reach is visible without leaving the page. Each pane is the same sheet with one dial moved by hand (inline, the only override this catalog permits itself): the dial multiplies the recipe; the engine's floors and caps stay as the contract measured them.")}
          </p>
          <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
            <div className="bg-ink-900/60 rounded-panel p-5 ring-1 ring-line/50" style={demoStyle.frost06}>
              <p className="mono-label !text-[0.6rem]">frost 0.6×</p>
              <p className="mt-2 text-sm text-text-secondary">{t("The page becomes crisper behind the pane — the blur bows; the fill stays.")}</p>
            </div>
            <div className="bg-ink-900/60 rounded-panel p-5 ring-1 ring-line/50" style={demoStyle.body14}>
              <p className="mono-label !text-[0.6rem]">body 1.4×</p>
              <p className="mt-2 text-sm text-text-secondary">{t("The fill stands stiffer — more sheet, less print.")}</p>
            </div>
            <div className="bg-ink-900/60 rounded-panel p-5 ring-1 ring-line/50" style={demoStyle.glint00}>
              <p className="mono-label !text-[0.6rem]">glint 0×</p>
              <p className="mt-2 text-sm text-text-secondary">{t("The corners go quiet — frosted plastic.")}</p>
            </div>
            <div className="bg-ink-900/60 rounded-panel p-5 ring-1 ring-line/50" style={demoStyle.glint20}>
              <p className="mono-label !text-[0.6rem]">glint 2×</p>
              <p className="mt-2 text-sm text-text-secondary">{t("The corners burn, capped at 0.55 so the text field never lifts.")}</p>
            </div>
          </div>
        </Container>
      </Section>

      {/* ── 6.5 · The chrome (§C8): the accent disc + the glass bar ────── */}
      <Section aria-labelledby="cat-chrome" className="!py-6">
        <Container>
          <h2 id="cat-chrome" className="card-title mb-4">{t("The chrome — accent disc + glass bar (§C8)")}</h2>
          <div className="glass-bar inline-flex items-center gap-4 px-5 py-4">
            <button type="button" className="glass-fab grid h-14 w-14 place-items-center" aria-label={t("Demo accent action")}>
              <svg viewBox="0 0 20 20" className="h-5 w-5" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round">
                <path d="M10 3v14M3 10h14" />
              </svg>
            </button>
            <div>
              <p className="mono-label !text-[0.6rem]">.glass-fab in .glass-bar</p>
              <p className="mt-1 text-sm text-text-secondary">
                {t("The saturated disc sits in the frosted bar — the references' most-copied chrome. Gradient + specular lip + family halo; available to any round action under crystal.")}
              </p>
            </div>
          </div>
          <div className="mt-4 flex flex-wrap gap-3">
            <button type="button" className="btn btn-primary btn-sm">{t("Primary — faceted + halo")}</button>
            <button type="button" className="btn btn-secondary btn-sm">{t("Secondary — untouched")}</button>
          </div>
        </Container>
      </Section>

      {/* ── 6.7 · The ornaments (§C9): rings + volumetric orbs + spectrum ── */}
      <Section aria-labelledby="cat-ornaments" className="!py-6">
        <Container>
          <h2 id="cat-ornaments" className="card-title mb-4">{t("The ornaments — rings, orbs, spectrum (§C9/§C13)")}</h2>
          <div className="plate relative h-44 overflow-hidden">
            <div className="aurora-field">
              <span className="decor decor-ring" style={{ left: "6%", top: "18%", width: 92, height: 92 }} />
              <span className="decor decor-ring" style={{ left: "58%", top: "8%", width: 54, height: 54 }} />
              <span className="decor decor-orb" style={{ left: "30%", top: "26%", width: 116, height: 116 }} />
              <span className="glass-sphere" style={{ position: "absolute", left: "76%", top: "22%", width: 72, height: 72 }} />
            </div>
            <div className="relative z-10 flex h-full items-center justify-center gap-3">
              <div className="menu-card rounded-panel px-5 py-4">
                <p className="mono-label !text-[0.6rem]">frost over ornaments</p>
                <p className="mt-1 text-sm text-text-secondary">{t("Rings and spheres smear under 18px of frost — the screenshots' first grammar, now ours.")}</p>
              </div>
              <button type="button" className="btn btn-primary btn-sm">{t("Spectrum primary — §C13")}</button>
            </div>
          </div>
        </Container>
      </Section>

      {/* ── 6.8 · Tiles + selected glass (§C10) ─────────────────────────── */}
      <Section aria-labelledby="cat-tiles" className="!py-6">
        <Container>
          <h2 id="cat-tiles" className="card-title mb-4">{t("Tiles + selected glass (§C10)")}</h2>
          <div className="flex flex-wrap items-center gap-4">
            <span className="grid h-11 w-11 place-items-center rounded-card bg-brand-500/15 text-brand-300">
              <svg viewBox="0 0 20 20" className="h-5 w-5" fill="none" stroke="currentColor" strokeWidth="1.7" strokeLinecap="round"><path d="M4 10h12M10 4v12" /></svg>
            </span>
            <button type="button" className="chip" aria-pressed="false">$50</button>
            <button type="button" className="chip" aria-pressed="true">$100</button>
            <span className="chip glass-active">{t("Living room — picked")}</span>
            <p className="text-sm text-text-secondary">{t("The chosen thing saturates: spectrum fill, white ink, halo — the references' selection grammar.")}</p>
          </div>
        </Container>
      </Section>

      {/* ── 6.9 · The docked disc (§C11) ─────────────────────────────────── */}
      <Section aria-labelledby="cat-dock" className="!py-6">
        <Container>
          <h2 id="cat-dock" className="card-title mb-4">{t("The docked disc — the bar's seat (§C11)")}</h2>
          <div className="glass-bar dock mx-auto flex max-w-sm items-end justify-around px-8 py-3">
            <span className="pb-1 text-xs text-text-muted">⌂</span>
            <span className="pb-1 text-xs text-text-muted">◷</span>
            <button type="button" className="glass-fab is-docked grid h-14 w-14 place-items-center" aria-label={t("Demo docked action")}>
              <svg viewBox="0 0 20 20" className="h-5 w-5" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round"><path d="M10 4a2.5 2.5 0 0 1 2.5 2.5v3a2.5 2.5 0 0 1-5 0v-3A2.5 2.5 0 0 1 10 4Zm-5 5a5 5 0 0 0 10 0M10 14v3" /></svg>
            </button>
            <span className="pb-1 text-xs text-text-muted">⚡</span>
            <span className="pb-1 text-xs text-text-muted">☺</span>
          </div>
          <p className="mt-3 text-center text-sm text-text-secondary">{t("The disc breaks the bar's top edge and casts the family's halo upward — the mic-button grammar.")}</p>
        </Container>
      </Section>

      {/* ── 6.10 · Glass rows + accordion (§C12) ─────────────────────────── */}
      <Section aria-labelledby="cat-rows" className="!py-6">
        <Container>
          <h2 id="cat-rows" className="card-title mb-4">{t("Glass rows + the accordion (§C12)")}</h2>
          <div className="glass-rows max-w-md">
            <details className="glass-row group cursor-pointer" open>
              <summary className="flex items-center justify-between text-sm font-semibold text-text-primary [&::-webkit-details-marker]:hidden">
                TITLE #01 GOES HERE
                <svg viewBox="0 0 12 12" className="h-3 w-3 transition-transform group-open:rotate-180" fill="none" stroke="currentColor" strokeWidth="1.6"><path d="M2 4l4 4 4-4" /></svg>
              </summary>
              <p className="mt-2 text-xs text-text-secondary">{t("The content opens on lighter fill — the notes reference's accordion.")}</p>
            </details>
            <a className="glass-row flex items-center justify-between text-sm text-text-primary" href="#cat-rows">
              GHI · stock name <span className="font-semibold">1200 <span className="text-xs text-emerald-400">+50.00</span></span>
            </a>
            <a className="glass-row flex items-center justify-between text-sm text-text-primary" href="#cat-rows">
              JKL · stock name <span className="font-semibold">350 <span className="text-xs text-emerald-400">+25.00</span></span>
            </a>
          </div>
        </Container>
      </Section>

      {/* ── 7 · Notes for the reviewer ─────────────────────────────── */}
      <Section aria-labelledby="cat-notes" className="!py-6 pb-14">
        <Container>
          <h2 id="cat-notes" className="card-title mb-4">{t("Notes for the reviewer")}</h2>
          <ul className="grid gap-2 text-sm text-text-secondary sm:grid-cols-2">
            <li className="bg-ink-800/40 rounded-panel p-4 ring-1 ring-line/40">
              {t("Toggle the theme: every fill, every tier, every shadow turns over with it — the mode owns the ink; the family owns the material.")}
            </li>
            <li className="bg-ink-800/40 rounded-panel p-4 ring-1 ring-line/40">
              {t("Move the studio bench: the dials multiply the recipe; the pages of this catalog move in exact step, because the bench's reach is total.")}
            </li>
            <li className="bg-ink-800/40 rounded-panel p-4 ring-1 ring-line/40">
              {t("Reduce motion: every animated seam (corner runs, catch, chill) is dead — the static sheet alone remains, the §C5 reduced-motion covenant.")}
            </li>
            <li className="bg-ink-800/40 rounded-panel p-4 ring-1 ring-line/40">
              {t("A coarse pointer reads the census (G6): 522 blurred surfaces on /features become a 0.6-folded radius. Nothing else moves; the count is the proof.")}
            </li>
          </ul>
        </Container>
      </Section>
    </>
  );
}

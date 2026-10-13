import type { Metadata } from "next";
import { Container, Section } from "@/components/ui/Container";
import { translatorFor } from "@/i18n/server";

/*
 * The clay catalog — every plate, surface and carve the engine owns, on
 * one screen. This route is a review tool, not a page: it is excluded
 * from the sitemap and marked noindex, so a search engine never suggests
 * it and a visitor only reaches it by URL.
 *
 * Rule from the roadmap: selectors live in the engine file; this route
 * declares intent only. Nothing here uses inline hex or a bespoke
 * shadow — every face the catalog can show is one the bench can recolor.
 */

type PageProps = { params: Promise<{ locale: string }> };

export async function generateMetadata({ params }: PageProps): Promise<Metadata> {
  await translatorFor(params);
  return {
    title: "Clay engine catalog",
    description: "Internal review surface: every clay plate, surface and control on one page.",
    robots: { index: false, follow: false },
  };
}

const PLATES = ["plate-1", "plate-2", "plate-3", "plate-4", "plate-5", "plate-6"] as const;
const MEDIA_BODY =
  "The card answers its own width: compact under 340px, media in its own lane past 480px — the same markup in a sliver, a column, a full row.";

export default async function ClayCatalogPage({ params }: PageProps) {
  /* Validates the locale like every other route; the page itself is an
     internal English review surface (noindex), so it takes no copy key. */
  await translatorFor(params);
  return (
    <>
      <Section aria-labelledby="clay-catalog" className="pt-10">
        <Container>
          <p className="mono-label">Internal · review surface</p>
          <h1 id="clay-catalog" className="mt-2 text-3xl font-bold tracking-tight text-text-primary sm:text-4xl">
            Clay engine catalog
          </h1>
          <p className="mt-3 max-w-2xl text-sm leading-relaxed text-text-muted">
            Every named clay surface, plate and carve on one page, in the
            current family, theme and bench. Nothing below is a local
            override: recolor the bench in the theme studio and the whole
            page must move as one.
          </p>
        </Container>
      </Section>

      {/* ── 1 · Surface ladder ─────────────────────────────────────── */}
      <Section aria-labelledby="cat-surfaces" className="!py-6">
        <Container>
          <h2 id="cat-surfaces" className="card-title mb-4">Surfaces</h2>
          <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
            <div className="surface p-5">
              <p className="mono-label">.surface</p>
              <p className="mt-2 text-sm text-text-muted">The resting card — recipe sheet, sculpted both modes.</p>
            </div>
            <div className="surface-raised p-5">
              <p className="mono-label">.surface-raised</p>
              <p className="mt-2 text-sm text-text-muted">One tier closer to the light — hover altitude by default.</p>
            </div>
            <div className="card-accent p-5">
              <p className="mono-label">.card-accent</p>
              <p className="mt-2 text-sm">The 10% accent — lavender face, plum bench, porcelain buttons.</p>
              <div className="mt-4 flex flex-wrap gap-2">
                <button type="button" className="btn btn-secondary btn-sm">Secondary</button>
              </div>
            </div>
            <div className="panel-feature p-5">
              <p className="mono-label">.panel-feature</p>
              <p className="mt-2 text-sm">The feature plate — first plate of the bench, in both modes.</p>
            </div>
          </div>
        </Container>
      </Section>

      {/* ── 2 · The plate bench ────────────────────────────────────── */}
      <Section aria-labelledby="cat-plates" className="!py-6">
        <Container>
          <h2 id="cat-plates" className="card-title mb-4">The plate bench</h2>
          <div className="grid grid-cols-2 gap-4 sm:grid-cols-3 xl:grid-cols-7">
            {PLATES.map((plate, index) => (
              <div key={plate} className={`${plate} flex min-h-24 flex-col justify-between rounded-card p-4`}>
                <p className="mono-label !text-[0.6rem]">{plate}</p>
                <p className="text-xs font-semibold">Plate {index + 1}</p>
              </div>
            ))}
          </div>
        </Container>
      </Section>

      {/* ── 3 · Rotation + the noria ───────────────────────────────── */}
      <Section aria-labelledby="cat-noria" className="!py-6">
        <Container>
          <h2 id="cat-noria" className="card-title mb-4">Stat rotation + noria</h2>
          <div className="grid gap-6 xl:grid-cols-2">
            <div>
              <p className="mb-2 text-xs font-semibold text-text-faint">Default grid — house order (1 → 4)</p>
              <dl className="grid grid-cols-2 gap-3">
                {[
                  ["Tile 1", "Order one", "the default face"],
                  ["Tile 2", "Order two", "mint lane"],
                  ["Tile 3", "Order three", "peach lane"],
                  ["Tile 4", "Order four", "sky lane"],
                ].map(([label, value, note]) => (
                  <div key={value} className="stat-tile p-3.5">
                    <dt className="mono-label">{label}</dt>
                    <dd className="mt-1.5 text-base font-semibold text-text-primary">{value}</dd>
                    <dd className="mt-1 text-[0.75rem] leading-relaxed text-text-muted">{note}</dd>
                  </div>
                ))}
              </dl>
            </div>
            <div>
              <p className="mb-2 text-xs font-semibold text-text-faint">.plate-set-b — re-pointed rotation (5 / 2 / 6 / 4)</p>
              <dl className="plate-set-b grid grid-cols-2 gap-3">
                {[
                  ["Tile 1", "Rose face", "plate five"],
                  ["Tile 2", "Mint face", "plate two"],
                  ["Tile 3", "Lemon face", "plate six"],
                  ["Tile 4", "Sky face", "plate four"],
                ].map(([label, value, note]) => (
                  <div key={value} className="stat-tile p-3.5">
                    <dt className="mono-label">{label}</dt>
                    <dd className="mt-1.5 text-base font-semibold text-text-primary">{value}</dd>
                    <dd className="mt-1 text-[0.75rem] leading-relaxed text-text-muted">{note}</dd>
                  </div>
                ))}
              </dl>
            </div>
          </div>
        </Container>
      </Section>

      {/* ── 4 · Feature plates (row rotation, daylight only) ───────── */}
      <Section aria-labelledby="cat-feature" className="!py-6">
        <Container>
          <h2 id="cat-feature" className="card-title mb-4">Feature plates (daylight row rotation)</h2>
          <div className="feature-plates grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
            {["Panel face", "Card face", "Band face", "Card face"].map((label, index) => (
              <div key={`${label}-${index}`} className="surface-raised p-5">
                <p className="mono-label">Tile {index + 1}</p>
                <h3 className="mt-2 text-base font-semibold text-text-primary">{label}</h3>
                <p className="mt-2 text-sm leading-relaxed text-text-muted">
                  {index % 2 === 0
                    ? "A zone-plate face: the bench moves it, the ink stays plum."
                    : "The card face holds the recipe so the alternation reads."}
                </p>
              </div>
            ))}
          </div>
        </Container>
      </Section>

      {/* ── 5 · Wells (pressed controls) ───────────────────────────── */}
      <Section aria-labelledby="cat-wells" className="!py-6">
        <Container>
          <h2 id="cat-wells" className="card-title mb-4">Wells and controls</h2>
          <div className="surface max-w-2xl space-y-4 p-6">
            <label className="block">
              <span className="mono-label">Search (pressed well)</span>
              <input
                type="search"
                placeholder="Pressed well — inner shade, no drawn edge…"
                className="mt-2 w-full rounded-xl border border-line-strong/70 bg-ink-950/80 py-2.5 pl-4 pr-3 text-sm text-text-primary placeholder:text-text-faint/70 focus:outline-none"
                readOnly
              />
            </label>
            <label className="flex items-center gap-2 text-sm text-text-secondary">
              <input type="checkbox" defaultChecked className="accent-brand-400" />
              Family hue on the checker
            </label>
            <p className="text-xs text-text-faint">
              Focus any element above to meet the keyed lip (§19.4). Print
              this page: every plate collapses to an ink outline (§19.5).
            </p>
          </div>
        </Container>
      </Section>

      {/* ── 6 · Media card at 320px and 560px containers ───────────── */}
      <Section aria-labelledby="cat-media" className="!py-6">
        <Container>
          <h2 id="cat-media" className="card-title mb-4">Container-driven media card</h2>
          <div className="grid gap-6">
            <div style={{ width: 320 }}>
              <article className="clay-media-card">
                <div className="clay-media-card__media">
                  <div className="plate-2 absolute inset-0" aria-hidden="true" />
                  <span className="clay-media-card__tag">Feature</span>
                </div>
                <div className="clay-media-card__body">
                  <p className="clay-media-card__eyebrow">Container query · compact</p>
                  <h3 className="clay-media-card__title">320px lane</h3>
                  <p className="clay-media-card__description">{MEDIA_BODY}</p>
                </div>
              </article>
            </div>
            <div style={{ width: "min(560px, 100%)" }}>
              <article className="clay-media-card">
                <div className="clay-media-card__media">
                  <div className="plate-4 absolute inset-0" aria-hidden="true" />
                  <span className="clay-media-card__tag">Feature</span>
                </div>
                <div className="clay-media-card__body">
                  <p className="clay-media-card__eyebrow">Container query · wide vignette</p>
                  <h3 className="clay-media-card__title">560px lane</h3>
                  <p className="clay-media-card__description">{MEDIA_BODY}</p>
                  <div className="clay-media-card__meta">
                    <ul className="clay-media-card__avatars" aria-hidden="true">
                      <li className="clay-media-card__avatar plate-1" />
                      <li className="clay-media-card__avatar plate-5" />
                    </ul>
                    <span className="text-xs font-semibold text-text-faint">motion grammar 10/10</span>
                  </div>
                </div>
              </article>
            </div>
          </div>
        </Container>
      </Section>

      {/* ── 7 · Rule carves ────────────────────────────────────────── */}
      <Section aria-labelledby="cat-rules" className="!py-6">
        <Container>
          <h2 id="cat-rules" className="card-title mb-4">Rule carves</h2>
          <div className="surface max-w-2xl space-y-4 p-6">
            <p className="text-sm text-text-muted">A pressed .rule-plate between two paragraphs:</p>
            <span className="rule-plate" aria-hidden="true" />
            <p className="text-sm text-text-muted">The floor below the carve is lit; the room above is shaded.</p>
            <div className="flex items-stretch gap-4">
              <span className="rule-plate rule-plate--vertical min-h-10" aria-hidden="true" />
              <p className="text-sm text-text-muted">And the vertical form beside a text block.</p>
            </div>
          </div>
        </Container>
      </Section>

      {/* ── 8 · Bands ──────────────────────────────────────────────── */}
      <Section aria-labelledby="cat-bands" className="!py-6">
        <Container>
          <h2 id="cat-bands" className="card-title mb-4">Band zones</h2>
          <div className="band-solid rounded-card p-6">
            <p className="mono-label">.band-solid — the calm lit band</p>
            <p className="mt-2 max-w-xl text-sm">Reads the foil face in daylight, the deep plum at midnight.</p>
          </div>
          <div className="band-deep mt-4 overflow-hidden rounded-card p-6">
            <p className="mono-label">.band-deep — the deep plate zone</p>
            <p className="mt-2 max-w-xl text-sm">Lavender zone face, plum ink, porcelain buttons.</p>
            <div className="mt-4 flex flex-wrap gap-2">
              <button type="button" className="btn btn-primary btn-sm">Primary verb</button>
              <button type="button" className="btn btn-secondary btn-sm">Secondary</button>
            </div>
          </div>
        </Container>
      </Section>
    </>
  );
}

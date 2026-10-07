import { cn } from "@/lib/cn";
import type { PlateView, PackMargins } from "@/lib/guillotinePack";
import {
  areaText,
  breakoutZone,
  dimensionsText,
  labelsFor,
  pieceRef,
  reusableOffcut,
  shortDimensionsText,
  usableZone,
  type CadDensity,
} from "@/components/optimization/cad";
import type { Translator } from "@/i18n/core";

/**
 * One plate, drawn the way the application's 2D view draws it.
 *
 * This is the site's transcription of the Single Sheet CAD canvas
 * (`Views/Optimization/Tabs/TwoDViewTabView.xaml`), and it is a **light**
 * drawing in both readings of the site. A plate is a sheet of drawing paper
 * lying on the page — not a dark card with pale rectangles in it — because the
 * screen it reports is the application's light studio screen. The brushes are
 * the application's own, transcribed in `globals.css` under the studio-sheet
 * section: the stone bed `#D6D3D1`, near-white pieces outlined in the
 * saw-channel ink `#1F2937`, the trim boundary teal `#0F766E`, the breakout
 * band amber `#B45309`, a violet-tinted remnant and an indigo `Cut N` chip.
 *
 * What it carries, all of it from the application's canvas:
 *
 *   · the **usable rectangle**, dashed, inside the edge trims
 *   · the **breakout band**, dashed again and inset, which is the offcut inside
 *     the trims that is still breakout waste
 *   · the **plate callouts** — the stock width above the plate and its height
 *     down the left, like a workshop drawing
 *   · the **cut sequence tag** on each piece, in the order the packer placed it
 *   · each piece's **reference, its cut size in millimetres, and its area**,
 *     drawn only where the piece is big enough to carry the label
 *   · the **reusable remnant**, labelled with the size it would go back on the
 *     rack as
 *
 * Everything is derived from the packing result by `cad.ts`. The drawing has no
 * numbers of its own, so it cannot disagree with the schedule beside it.
 *
 * Sizes are millimetres throughout — the SVG's user units are millimetres and
 * stroke widths and type sizes are fractions of the stock width, so the whole
 * drawing scales as one object at any rendered size, and a piece too small for
 * its label stops carrying one instead of smudging.
 *
 * Renders on the server and inside client components alike, so it takes its
 * translator as a prop rather than reading one from a hook.
 */
export function PlateCad({
  t,
  plate,
  margins,
  minBreakoutMm = 15,
  density = "inspect",
  showCallouts = density === "inspect",
  /** Draw only the first N pieces by placement order. Undefined draws them all. */
  reveal,
  /** Milliseconds between pieces while a sequence is being revealed. */
  revealStepMs = 0,
  /** Milliseconds to fade a piece in — 0 when the reveal is instantaneous. */
  revealDurationMs = 500,
  className,
  title,
}: {
  t: Translator;
  plate: PlateView;
  margins: PackMargins;
  /** The breakout allowance for the plate's thickness bucket. */
  minBreakoutMm?: number;
  density?: CadDensity;
  showCallouts?: boolean;
  reveal?: number;
  revealStepMs?: number;
  revealDurationMs?: number;
  className?: string;
  title?: string;
}) {
  const width = plate.widthMm;
  const height = plate.heightMm;

  /* Every ounce of the drawing's scale comes from the stock width. */
  const font = {
    callout: width / 62,
    reference: width / 52,
    dimensions: width / 48,
    area: width / 66,
    tag: width / 74,
    offcut: width / 66,
    sequence: width / 30,
  };
  const stroke = {
    plate: width / 300,
    piece: width / 450,
    trim: width / 600,
    breakout: width / 700,
    callout: width / 900,
  };

  const usable = usableZone(plate, margins);
  const breakout = breakoutZone(usable, minBreakoutMm);
  const offcut = reusableOffcut(plate, margins);

  /* Room for the callouts, or a hairline of margin when there are none. */
  const gutter = showCallouts ? width * 0.088 : width * 0.012;
  const viewBox = `${-gutter} ${-gutter} ${width + gutter * 2} ${height + gutter * 2}`;

  /*
   * Placement order *on this plate*, numbered from 1 — which is what the
   * application's `Cut N` tag means and what its schedule's `Seq #` column
   * counts. The packer's own `sequence` runs across the whole run, so it is
   * re-based here rather than printed.
   */
  const ordered = [...plate.pieces].sort((a, b) => a.sequence - b.sequence);

  return (
    <svg
      viewBox={viewBox}
      className={cn("w-full", className)}
      role="img"
      aria-label={
        title ??
        t.template`Sheet ${plate.plate} of the sample job: ${plate.pieceCount} pieces nested on a ${plate.stockLabel} millimetre plate, ${plate.utilizationPct} percent used, ${plate.offcutSqm} square metres of it left over.`
      }
    >
      {/* ── The stock: a stone bed, the way the table's screen shows it ──── */}
      <rect
        x={0}
        y={0}
        width={width}
        height={height}
        rx={width * 0.004}
        className="cad-bed"
        strokeWidth={stroke.plate}
      />

      {/* ── The usable rectangle: stock inset by the four edge trims ─────── */}
      <rect
        x={usable.x}
        y={usable.y}
        width={usable.width}
        height={usable.height}
        fill="none"
        className="cad-trim"
        strokeWidth={stroke.trim}
        strokeDasharray={`${font.callout * 0.72} ${font.callout * 0.34}`}
      />

      {/* ── The breakout band, inset again ───────────────────────────────── */}
      <rect
        x={breakout.x}
        y={breakout.y}
        width={breakout.width}
        height={breakout.height}
        fill="none"
        className="cad-breakout"
        strokeOpacity={0.65}
        strokeWidth={stroke.breakout}
        strokeDasharray={`${font.callout * 0.2} ${font.callout * 0.2}`}
      />

      {/* ── The pieces, as placed ────────────────────────────────────────── */}
      {ordered.map((piece, index) => {
        const labels = labelsFor(piece, density);
        const sequence = index + 1;
        const fading = reveal !== undefined && sequence > reveal;
        const compact = density !== "inspect";

        return (
          <g
            key={piece.id}
            className="transition-opacity ease-reveal"
            style={{
              opacity: fading ? 0 : 1,
              transitionDuration: `${revealDurationMs}ms`,
              transitionDelay:
                reveal !== undefined && !fading && revealStepMs > 0
                  ? `${(sequence - 1) * revealStepMs}ms`
                  : undefined,
            }}
          >
            <rect
              x={piece.x}
              y={piece.y}
              width={piece.width}
              height={piece.height}
              rx={width * 0.0015}
              /*
                A rotated piece is filled in the light-sky tint and outlined in
                the accent, which is how the application marks one: the fit
                explains itself without a caption.
              */
              className={piece.rotated ? "cad-piece-rotated" : "cad-piece"}
              strokeWidth={piece.rotated ? stroke.piece * 0.7 : stroke.piece}
            />

            {compact ? (
              labels.cutTag ? (
                <text
                  x={piece.x + piece.width / 2}
                  y={piece.y + piece.height / 2}
                  textAnchor="middle"
                  dominantBaseline="central"
                  className="cad-ink"
                  style={{ fontSize: font.sequence, fontWeight: 600 }}
                >
                  {sequence}
                </text>
              ) : null
            ) : (
              <>
                {/* The rotated mark: a corner triangle, drawn at any size. */}
                {piece.rotated ? (
                  <polygon
                    points={`${piece.x + piece.width - width * 0.035},${piece.y} ${piece.x + piece.width},${piece.y} ${piece.x + piece.width},${piece.y + width * 0.035}`}
                    className="cad-accent"
                    fillOpacity={0.5}
                  />
                ) : null}

                {labels.dimensions ? (
                  <g>
                    <text
                      x={piece.x + piece.width / 2}
                      y={piece.y + piece.height / 2 - font.reference * 0.62}
                      textAnchor="middle"
                      className="cad-ink"
                      style={{ fontSize: font.reference, fontWeight: 600 }}
                    >
                      {pieceRef(piece.id)}
                    </text>
                    <text
                      x={piece.x + piece.width / 2}
                      y={piece.y + piece.height / 2 + font.dimensions * 0.28}
                      textAnchor="middle"
                      className="cad-ink"
                      style={{ fontSize: font.dimensions, fontWeight: 700 }}
                    >
                      {dimensionsText(piece)}
                    </text>
                    {labels.area ? (
                      <text
                        x={piece.x + piece.width / 2}
                        y={piece.y + piece.height / 2 + font.dimensions * 0.9}
                        textAnchor="middle"
                        className="cad-muted"
                        style={{ fontSize: font.area }}
                      >
                        {areaText(piece)}
                      </text>
                    ) : null}
                  </g>
                ) : (
                  <text
                    x={piece.x + piece.width / 2}
                    y={piece.y + piece.height / 2}
                    textAnchor="middle"
                    dominantBaseline="central"
                    className="cad-ink"
                    style={{ fontSize: font.tag, fontWeight: 700 }}
                  >
                    {shortDimensionsText(piece)}
                  </text>
                )}

                {/* Cut sequence tag, top-left, as the application prints it. */}
                {labels.cutTag ? (
                  <g>
                    <rect
                      x={piece.x + width * 0.006}
                      y={piece.y + width * 0.006}
                      width={font.tag * 3.15}
                      height={font.tag * 1.6}
                      rx={font.tag * 0.3}
                      className="cad-tag"
                    />
                    <text
                      x={piece.x + width * 0.006 + font.tag * 1.575}
                      y={piece.y + width * 0.006 + font.tag * 0.82}
                      textAnchor="middle"
                      dominantBaseline="central"
                      className="cad-tag-text"
                      style={{ fontSize: font.tag * 0.92, fontWeight: 700 }}
                    >
                      {t.template`Cut ${sequence}`}
                    </text>
                  </g>
                ) : null}
              </>
            )}
          </g>
        );
      })}

      {/* ── The reusable remnant, when there is one worth keeping ────────── */}
      {offcut ? (
        <g>
          <rect
            x={offcut.x}
            y={offcut.y}
            width={offcut.width}
            height={offcut.height}
            className="cad-offcut"
            strokeWidth={stroke.trim}
            strokeDasharray={`${font.offcut * 0.5} ${font.offcut * 0.34}`}
          />
          {offcut.width > width * 0.2 && offcut.height > font.offcut * 4 ? (
            <g>
              <text
                x={offcut.x + offcut.width / 2}
                y={offcut.y + offcut.height / 2 - font.offcut * 0.35}
                textAnchor="middle"
                className="cad-accent"
                style={{ fontSize: font.offcut, fontWeight: 600 }}
              >
                {t(offcut.label)}
              </text>
              <text
                x={offcut.x + offcut.width / 2}
                y={offcut.y + offcut.height / 2 + font.offcut * 0.75}
                textAnchor="middle"
                className="cad-accent"
                style={{ fontSize: font.offcut, fontWeight: 700 }}
              >
                {`${Math.round(offcut.width)} × ${Math.round(offcut.height)} mm`}
              </text>
            </g>
          ) : null}
        </g>
      ) : null}

      {/* ── Plate callouts: width above, height down the left ───────────── */}
      {showCallouts ? (
        <g className="cad-ink" strokeLinecap="square">
          <line x1={0} y1={-gutter * 0.42} x2={width} y2={-gutter * 0.42} strokeWidth={stroke.callout} />
          <line x1={0} y1={-gutter * 0.52} x2={0} y2={-gutter * 0.32} strokeWidth={stroke.callout} />
          <line
            x1={width}
            y1={-gutter * 0.52}
            x2={width}
            y2={-gutter * 0.32}
            strokeWidth={stroke.callout}
          />
          <text
            x={width / 2}
            y={-gutter * 0.62}
            textAnchor="middle"
            className="cad-ink"
            style={{ fontSize: font.callout, fontWeight: 600 }}
          >
            {t("Plate width {v1} mm", { v1: width })}
          </text>

          <line x1={-gutter * 0.42} y1={0} x2={-gutter * 0.42} y2={height} strokeWidth={stroke.callout} />
          <line x1={-gutter * 0.52} y1={0} x2={-gutter * 0.32} y2={0} strokeWidth={stroke.callout} />
          <line
            x1={-gutter * 0.52}
            y1={height}
            x2={-gutter * 0.32}
            y2={height}
            strokeWidth={stroke.callout}
          />
          <text
            x={-gutter * 0.62}
            y={height / 2}
            textAnchor="middle"
            transform={`rotate(-90 ${-gutter * 0.62} ${height / 2})`}
            className="cad-ink"
            style={{ fontSize: font.callout, fontWeight: 600 }}
          >
            {t("{v1} mm", { v1: height })}
          </text>
        </g>
      ) : null}
    </svg>
  );
}

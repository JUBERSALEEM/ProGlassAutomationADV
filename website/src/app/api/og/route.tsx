import { ImageResponse } from "next/og";
import { site } from "@/lib/site";

/**
 * Social share cards, generated at request time.
 *
 * One handler serves every page: the caller passes the page's own eyebrow,
 * title and a colour pair, and the card is composed from the same identity the
 * site uses — the mitred-sheet mark, the violet ramp, the same typography. A
 * shared link therefore arrives as a designed card rather than a bare URL.
 *
 * Deliberately no user-supplied HTML and no fonts fetched from the network:
 * passing an arbitrary string into a renderer that sits behind a public URL is
 * the obvious way to build an injection surface, so every parameter is length
 * capped and the only values that reach the layout are text runs.
 */

export const runtime = "nodejs";

/** Largest title we will render before truncating. */
const MAX_TITLE = 120;
const MAX_EYEBROW = 40;

function clean(value: string | null, limit: number): string {
  if (!value) return "";
  return value
    .replace(/[\u0000-\u001f\u007f]/g, " ") // control characters
    .replace(/\s+/g, " ")
    .trim()
    .slice(0, limit);
}

/** `#RRGGBB` or the house violet. Anything else is ignored rather than trusted. */
function colour(value: string | null, fallback: string): string {
  if (!value) return fallback;
  const trimmed = value.trim();
  return /^#[0-9a-fA-F]{6}$/.test(trimmed) ? trimmed : fallback;
}

export async function GET(request: Request) {
  const { searchParams } = new URL(request.url);

  const title = clean(searchParams.get("title"), MAX_TITLE) || site.name;
  const eyebrow = clean(searchParams.get("eyebrow"), MAX_EYEBROW) || "Glass fabrication software";
  const accent = colour(searchParams.get("accent"), "#8B5CF6");
  const accentSoft = colour(searchParams.get("accentSoft"), "#C4B5FD");

  // A longer title steps down a size rather than overflowing the card.
  const titleSize = title.length > 78 ? 54 : title.length > 52 ? 62 : 70;

  return new ImageResponse(
    (
      <div
        style={{
          width: "100%",
          height: "100%",
          display: "flex",
          flexDirection: "column",
          justifyContent: "space-between",
          background: "#0B0820",
          padding: "64px 72px",
          position: "relative",
          fontFamily: "sans-serif",
        }}
      >
        {/* Ambient light, same two pools the site opens on. */}
        <div
          style={{
            position: "absolute",
            top: -180,
            left: 40,
            width: 620,
            height: 620,
            borderRadius: 999,
            background: `radial-gradient(circle, ${accent}55, transparent 68%)`,
          }}
        />
        <div
          style={{
            position: "absolute",
            bottom: -220,
            right: -80,
            width: 560,
            height: 560,
            borderRadius: 999,
            background: "radial-gradient(circle, #22D3EE33, transparent 68%)",
          }}
        />

        {/* Header row: mark and lockup. */}
        <div style={{ display: "flex", alignItems: "center", gap: 18 }}>
          <svg width="52" height="52" viewBox="0 0 32 32">
            <path d="M8.5 3.5h20l-4 4h-16z" fill="#22D3EE" fillOpacity="0.85" />
            <path d="M4.5 12.5h20l-4 4h-16z" fill="#22D3EE" fillOpacity="0.6" />
            <path
              d="M3.5 3.5h17.5l7.5 7.5v17.5H3.5z"
              fill={accent}
              stroke={accentSoft}
              strokeWidth="0.8"
              strokeLinejoin="round"
            />
            <path d="M3.5 3.5 L21 3.5 L12 24 L3.5 24 z" fill="#FFFFFF" fillOpacity="0.28" />
            <path
              d="M21 3.5v7.5h7.5"
              fill="none"
              stroke="#FFFFFF"
              strokeOpacity="0.55"
              strokeWidth="0.9"
            />
          </svg>
          <div style={{ display: "flex", flexDirection: "column" }}>
            <div style={{ display: "flex", fontSize: 26, fontWeight: 700, color: "#FFFFFF", letterSpacing: -0.4 }}>
              Pro
              <span style={{ color: accentSoft }}>Glass</span>
            </div>
            <div style={{ fontSize: 13, letterSpacing: 4.5, color: "#B6B0D6", textTransform: "uppercase" }}>
              Automation
            </div>
          </div>
        </div>

        {/* The page's own framing. */}
        <div style={{ display: "flex", flexDirection: "column", maxWidth: 1000 }}>
          <div
            style={{
              display: "flex",
              fontSize: 17,
              fontWeight: 700,
              letterSpacing: 3.4,
              textTransform: "uppercase",
              color: accentSoft,
            }}
          >
            {eyebrow}
          </div>
          <div
            style={{
              display: "flex",
              marginTop: 20,
              fontSize: titleSize,
              fontWeight: 700,
              lineHeight: 1.1,
              letterSpacing: -1.6,
              color: "#FFFFFF",
            }}
          >
            {title}
          </div>
        </div>

        {/* Footer: the two facts that matter most in a thumbnail. */}
        <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between" }}>
          <div style={{ display: "flex", gap: 28, fontSize: 20, fontWeight: 600, color: "#D0CBE8" }}>
            <span>Windows desktop</span>
            <span style={{ color: "#6F6790" }}>·</span>
            <span>Local SQLite data</span>
            <span style={{ color: "#6F6790" }}>·</span>
            <span>{site.locales} languages</span>
          </div>
          <div style={{ display: "flex", fontSize: 20, fontWeight: 700, color: accentSoft }}>
            v{site.version}
          </div>
        </div>

        {/* The accent hairline, mirroring the site's card edge. */}
        <div
          style={{
            position: "absolute",
            left: 0,
            right: 0,
            bottom: 0,
            height: 6,
            background: `linear-gradient(90deg, ${accent}, #22D3EE)`,
          }}
        />
      </div>
    ),
    { width: 1200, height: 630 },
  );
}

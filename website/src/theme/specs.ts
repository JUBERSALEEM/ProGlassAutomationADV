import {
  THEME_SPEC_VERSION,
  type PortableColorTokens,
  type PortableMaterialTokens,
  type ThemeFamilyId,
  type ThemeSpec,
} from "./schema.ts";

const typography = {
  sans: "Inter Variable",
  display: "IBM Plex Sans Variable",
  mono: "JetBrains Mono Variable",
  basePx: 16,
  scale: [12, 14, 16, 20, 26, 38, 58],
  weights: { regular: 400, medium: 500, semibold: 600, bold: 700 },
  lineHeights: { compact: 1.1, heading: 1.18, body: 1.55, relaxed: 1.72 },
  letterSpacingEm: { tight: -0.025, normal: 0, label: 0.1 },
} as const;

const layout = {
  spacingPx: [4, 8, 12, 16, 24, 32, 48, 72],
  contentWidthsPx: { prose: 680, reading: 1184, shell: 1344 },
  breakpointsPx: { mobile: 360, tablet: 640, laptop: 1024, desktop: 1440, wide: 1920 },
  layers: { base: 0, sticky: 30, dropdown: 40, overlay: 50, modal: 60, toast: 70 },
} as const;

const shape = {
  radiusPx: { control: 10, card: 16, panel: 20, hero: 24, pill: 999 },
  nestedRadiusInsetPx: 4,
  borderWidthPx: { hairline: 1, control: 1.5, emphasis: 2 },
} as const;

const motion = {
  intensity: 1,
  speed: 1,
  durationMs: { press: 140, hover: 200, state: 220, reveal: 380, dialog: 280 },
  easing: {
    standard: [0.22, 0.61, 0.36, 1],
    out: [0.16, 1, 0.3, 1],
    emphasized: [0.3, 0.1, 0.4, 1],
  },
  distancePx: { subtle: 6, standard: 12 },
  staggerMs: 45,
  staggerCapMs: 200,
} as const;

const states = {
  disabledOpacity: 0.55,
  loadingOpacity: 0.72,
  focusWidthPx: 2,
  focusOffsetPx: 2,
  errorEmphasis: 1,
  successEmphasis: 1,
  warningEmphasis: 1,
} as const;

const dark: PortableColorTokens = {
  brandPrimary: "#6546B5", brandSecondary: "#1AC5E2", accent: "#9E79E5",
  canvas: "#080516", surface: "#110B2A", surfaceElevated: "#181038", surfaceOverlay: "#201648",
  textPrimary: "#FFFFFF", textSecondary: "#EEEAFC", textMuted: "#C2B9E2", textInverse: "#080516",
  border: "#4E3A9E", divider: "#362874", focus: "#BEA5F0", selection: "#493080",
  success: "#2DD4A8", warning: "#F8B02A", danger: "#FB6A82", info: "#9E79E5",
};

const light: PortableColorTokens = {
  brandPrimary: "#6546B5", brandSecondary: "#087F91", accent: "#6546B5",
  canvas: "#F8F5EE", surface: "#FFFFFF", surfaceElevated: "#F2EEE7", surfaceOverlay: "#E9E3DA",
  textPrimary: "#181126", textSecondary: "#352C45", textMuted: "#655C73", textInverse: "#FFFFFF",
  border: "#B8ACC8", divider: "#D8D0E1", focus: "#6546B5", selection: "#D8C9F4",
  success: "#137A5C", warning: "#935B00", danger: "#B4233B", info: "#57399A",
};

const elevation = {
  low: { x: 0, y: 8, blur: 24, spread: -16, opacity: 0.35 },
  medium: { x: 0, y: 20, blur: 50, spread: -28, opacity: 0.45 },
  high: { x: 0, y: 36, blur: 82, spread: -38, opacity: 0.55 },
} as const;

const material = (overrides: Partial<PortableMaterialTokens>): PortableMaterialTokens => ({
  opacity: { surface: 0.97, elevated: 0.95, overlay: 0.99 },
  blurPx: { surface: 0, elevated: 0 }, saturation: 1, edgeHighlight: 0.12,
  clayExtrusion: 0, neumorphicDepth: 0, realisticTexture: 0, reflection: 0,
  elevation, ...overrides,
});

const family = (
  id: ThemeFamilyId,
  name: string,
  description: string,
  darkMaterial: PortableMaterialTokens,
  lightMaterial: PortableMaterialTokens,
  composition: readonly ThemeFamilyId[] = [],
): ThemeSpec => ({
  schema: "proglass.theme", version: THEME_SPEC_VERSION, id, name, description,
  modes: { dark: { colors: dark, material: darkMaterial }, light: { colors: light, material: lightMaterial } },
  typography, layout, shape, motion, states, composition,
});

export const THEME_SPECS = {
  clay: family("clay", "Claymorphism", "Soft dimensional plates with controlled extrusion and readable flat content wells.", material({ clayExtrusion: 0.78, edgeHighlight: 0.28 }), material({ clayExtrusion: 0.68, edgeHighlight: 0.24 })),
  glass: family("glass", "Glassmorphism", "Stable translucent panes with clear borders, readable fills, and static frosting.", material({ opacity: { surface: 0.78, elevated: 0.84, overlay: 0.94 }, blurPx: { surface: 12, elevated: 18 }, saturation: 1.35, edgeHighlight: 0.32 }), material({ opacity: { surface: 0.88, elevated: 0.92, overlay: 0.97 }, blurPx: { surface: 14, elevated: 20 }, saturation: 1.2, edgeHighlight: 0.24 })),
  "liquid-glass": family("liquid-glass", "Liquid Glass", "Luminous glass with restrained reflection and transform-only environmental movement.", material({ opacity: { surface: 0.68, elevated: 0.76, overlay: 0.92 }, blurPx: { surface: 16, elevated: 22 }, saturation: 1.55, edgeHighlight: 0.46, reflection: 0.32 }), material({ opacity: { surface: 0.82, elevated: 0.88, overlay: 0.96 }, blurPx: { surface: 18, elevated: 24 }, saturation: 1.3, edgeHighlight: 0.36, reflection: 0.22 })),
  skeuomorphic: family("skeuomorphic", "Skeuomorphism", "Selective metal, glass, and tactile cues over a modern enterprise layout.", material({ realisticTexture: 0.42, edgeHighlight: 0.38 }), material({ realisticTexture: 0.34, edgeHighlight: 0.3 })),
  neumorphic: family("neumorphic", "Neumorphism", "Tonal raised and inset controls with explicit borders and focus rings.", material({ neumorphicDepth: 0.58, edgeHighlight: 0.2 }), material({ neumorphicDepth: 0.5, edgeHighlight: 0.18 })),
  material: family("material", "Flat / Material", "Clear typography, semantic color, restrained elevation, and explicit interaction states.", material({ edgeHighlight: 0.06 }), material({ edgeHighlight: 0.04 })),
  hybrid: family("hybrid", "Liquid Glass + Clay", "Approved hybrid: stable glass structure with clay accent controls, never stacked indiscriminately.", material({ opacity: { surface: 0.76, elevated: 0.82, overlay: 0.94 }, blurPx: { surface: 14, elevated: 18 }, saturation: 1.35, edgeHighlight: 0.34, clayExtrusion: 0.28, reflection: 0.18 }), material({ opacity: { surface: 0.86, elevated: 0.9, overlay: 0.97 }, blurPx: { surface: 14, elevated: 20 }, saturation: 1.2, edgeHighlight: 0.26, clayExtrusion: 0.24, reflection: 0.12 }), ["liquid-glass", "clay"]),
} satisfies Record<ThemeFamilyId, ThemeSpec>;

/** Existing saved website choices migrate to the closest explicit family. */
export const LEGACY_FAMILY_MIGRATION = {
  studio: "neumorphic",
  monet: "material",
  industrial: "material",
  crystal: "liquid-glass",
  clay: "clay",
} as const satisfies Record<string, ThemeFamilyId>;

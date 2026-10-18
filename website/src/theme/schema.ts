/**
 * Technology-neutral ProGlass theme contract.
 *
 * This module contains no CSS, React, browser, or WPF types. It is the portable
 * design authority that adapters translate into CSS custom properties today
 * and can translate into XAML resources later.
 */

export const THEME_SPEC_VERSION = 1 as const;

export type ThemeModeName = "light" | "dark";
export type ThemeFamilyId =
  | "clay"
  | "glass"
  | "liquid-glass"
  | "skeuomorphic"
  | "neumorphic"
  | "material"
  | "hybrid";

export type HexColor = `#${string}`;
export type CubicBezier = readonly [number, number, number, number];

export type PortableColorTokens = {
  brandPrimary: HexColor;
  brandSecondary: HexColor;
  accent: HexColor;
  canvas: HexColor;
  surface: HexColor;
  surfaceElevated: HexColor;
  surfaceOverlay: HexColor;
  textPrimary: HexColor;
  textSecondary: HexColor;
  textMuted: HexColor;
  textInverse: HexColor;
  border: HexColor;
  divider: HexColor;
  focus: HexColor;
  selection: HexColor;
  success: HexColor;
  warning: HexColor;
  danger: HexColor;
  info: HexColor;
};

export type PortableTypographyTokens = {
  sans: string;
  display: string;
  mono: string;
  basePx: number;
  scale: readonly [number, number, number, number, number, number, number];
  weights: { regular: number; medium: number; semibold: number; bold: number };
  lineHeights: { compact: number; heading: number; body: number; relaxed: number };
  letterSpacingEm: { tight: number; normal: number; label: number };
};

export type PortableLayoutTokens = {
  spacingPx: readonly [number, number, number, number, number, number, number, number];
  contentWidthsPx: { prose: number; reading: number; shell: number };
  breakpointsPx: { mobile: number; tablet: number; laptop: number; desktop: number; wide: number };
  layers: { base: number; sticky: number; dropdown: number; overlay: number; modal: number; toast: number };
};

export type PortableShapeTokens = {
  radiusPx: { control: number; card: number; panel: number; hero: number; pill: number };
  nestedRadiusInsetPx: number;
  borderWidthPx: { hairline: number; control: number; emphasis: number };
};

export type PortableElevation = {
  x: number;
  y: number;
  blur: number;
  spread: number;
  opacity: number;
};

export type PortableMaterialTokens = {
  opacity: { surface: number; elevated: number; overlay: number };
  blurPx: { surface: number; elevated: number };
  saturation: number;
  edgeHighlight: number;
  clayExtrusion: number;
  neumorphicDepth: number;
  realisticTexture: number;
  reflection: number;
  elevation: { low: PortableElevation; medium: PortableElevation; high: PortableElevation };
};

export type PortableMotionTokens = {
  intensity: number;
  speed: number;
  durationMs: { press: number; hover: number; state: number; reveal: number; dialog: number };
  easing: { standard: CubicBezier; out: CubicBezier; emphasized: CubicBezier };
  distancePx: { subtle: number; standard: number };
  staggerMs: number;
  staggerCapMs: number;
};

export type PortableStateTokens = {
  disabledOpacity: number;
  loadingOpacity: number;
  focusWidthPx: number;
  focusOffsetPx: number;
  errorEmphasis: number;
  successEmphasis: number;
  warningEmphasis: number;
};

export type ThemeModeTokens = {
  colors: PortableColorTokens;
  material: PortableMaterialTokens;
};

export type ThemeSpec = {
  schema: "proglass.theme";
  version: typeof THEME_SPEC_VERSION;
  id: ThemeFamilyId;
  name: string;
  description: string;
  modes: Record<ThemeModeName, ThemeModeTokens>;
  typography: PortableTypographyTokens;
  layout: PortableLayoutTokens;
  shape: PortableShapeTokens;
  motion: PortableMotionTokens;
  states: PortableStateTokens;
  /** Families combined by an approved hybrid. Empty for independent families. */
  composition: readonly ThemeFamilyId[];
};

export type ThemeConfiguration = {
  schema: "proglass.theme-config";
  version: typeof THEME_SPEC_VERSION;
  family: ThemeFamilyId;
  mode: ThemeModeName | "system";
  accent?: HexColor;
  typographyScale: number;
  spacingScale: number;
  radiusScale: number;
  elevationScale: number;
  transparencyScale: number;
  blurScale: number;
  motionIntensity: number;
  motionSpeed: number;
};

const HEX = /^#[0-9a-f]{6}$/i;
const finiteBetween = (value: unknown, min: number, max: number): value is number =>
  typeof value === "number" && Number.isFinite(value) && value >= min && value <= max;

export const THEME_FAMILY_IDS: readonly ThemeFamilyId[] = [
  "clay", "glass", "liquid-glass", "skeuomorphic", "neumorphic", "material", "hybrid",
];

export function isThemeFamilyId(value: unknown): value is ThemeFamilyId {
  return typeof value === "string" && THEME_FAMILY_IDS.includes(value as ThemeFamilyId);
}

export function isHexColor(value: unknown): value is HexColor {
  return typeof value === "string" && HEX.test(value);
}

/** Validate untrusted imported/persisted configuration before any adapter uses it. */
export function parseThemeConfiguration(value: unknown): ThemeConfiguration | null {
  if (!value || typeof value !== "object") return null;
  const candidate = value as Partial<ThemeConfiguration>;
  if (candidate.schema !== "proglass.theme-config" || candidate.version !== THEME_SPEC_VERSION) return null;
  if (!isThemeFamilyId(candidate.family)) return null;
  if (candidate.mode !== "light" && candidate.mode !== "dark" && candidate.mode !== "system") return null;
  if (candidate.accent !== undefined && !isHexColor(candidate.accent)) return null;
  const scales = [
    candidate.typographyScale,
    candidate.spacingScale,
    candidate.radiusScale,
    candidate.elevationScale,
    candidate.transparencyScale,
    candidate.blurScale,
    candidate.motionIntensity,
    candidate.motionSpeed,
  ];
  if (!scales.every((item) => finiteBetween(item, 0.5, 1.5))) return null;
  return candidate as ThemeConfiguration;
}

export function serializeThemeConfiguration(configuration: ThemeConfiguration): string {
  const valid = parseThemeConfiguration(configuration);
  if (!valid) throw new TypeError("Invalid ProGlass theme configuration");
  return `${JSON.stringify(valid, null, 2)}\n`;
}

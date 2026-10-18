import type { ThemeFamilyId } from "./schema.ts";

/** Approved public component vocabulary. New reusable UI extends this registry
 * instead of creating page-local visual primitives. */
export const COMPONENT_CONTRACTS = {
  button: {
    implementation: "src/components/ui/Button.tsx",
    variants: ["primary", "secondary", "ghost", "quiet"],
    sizes: ["sm", "md", "lg"],
    requiredStates: ["rest", "hover", "focus", "pressed", "disabled", "loading"],
  },
  card: {
    implementation: "src/components/ui/Glass.tsx",
    variants: ["primary", "raised", "card"],
    sizes: ["compact", "standard"],
    requiredStates: ["rest", "hover", "focus", "disabled"],
  },
  badge: {
    implementation: "src/components/ui/Badge.tsx",
    variants: ["neutral", "accent", "success", "warning", "danger"],
    sizes: ["sm"],
    requiredStates: ["rest"],
  },
  field: {
    implementation: "src/components/ui/Field.tsx",
    variants: ["default", "error", "success", "readonly"],
    sizes: ["sm", "md", "lg"],
    requiredStates: ["rest", "hover", "focus", "disabled", "loading", "error", "success"],
  },
  feedback: {
    implementation: "src/components/ui/Feedback.tsx",
    variants: ["empty", "error", "success", "unavailable", "info"],
    sizes: ["compact", "standard"],
    requiredStates: ["rest"],
  },
  loading: {
    implementation: "src/components/ui/Loading.tsx",
    variants: ["skeleton", "progress", "spinner"],
    sizes: ["sm", "md", "lg"],
    requiredStates: ["loading", "reduced-motion"],
  },
  accordion: {
    implementation: "src/components/ui/Accordion.tsx",
    variants: ["default"], sizes: ["standard"],
    requiredStates: ["closed", "open", "focus", "disabled"],
  },
  navigation: {
    implementation: "src/components/layout/Navbar.tsx",
    variants: ["desktop", "mobile"], sizes: ["compact"],
    requiredStates: ["rest", "current", "open", "focus"],
  },
  hero: {
    implementation: "src/components/layout/PageHero.tsx",
    variants: ["editorial", "document", "showcase"], sizes: ["responsive"],
    requiredStates: ["rest", "reduced-motion"],
  },
} as const;

export type ComponentContractName = keyof typeof COMPONENT_CONTRACTS;
export type ComponentThemeCoverage = Record<ComponentContractName, readonly ThemeFamilyId[]>;

/** Every primitive consumes semantic roles and is expected to render under all
 * approved families. Family-specific markup is not part of the contract. */
const ALL_FAMILIES = [
  "clay", "glass", "liquid-glass", "skeuomorphic", "neumorphic", "material", "hybrid",
] as const satisfies readonly ThemeFamilyId[];

export const REQUIRED_COMPONENT_THEME_COVERAGE: ComponentThemeCoverage = {
  button: ALL_FAMILIES,
  card: ALL_FAMILIES,
  badge: ALL_FAMILIES,
  field: ALL_FAMILIES,
  feedback: ALL_FAMILIES,
  loading: ALL_FAMILIES,
  accordion: ALL_FAMILIES,
  navigation: ALL_FAMILIES,
  hero: ALL_FAMILIES,
};

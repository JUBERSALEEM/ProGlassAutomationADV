# ProGlass Universal Theme Architecture

## Authority

The versioned portable authority is `src/theme/schema.ts`; approved complete family specifications are in `src/theme/specs.ts`. CSS variables, Tailwind utilities, React components, Framer Motion, and a future WPF adapter are consumers of that contract, not competing sources of design decisions.

Existing runtime engines remain active while adapters are introduced incrementally:

- `src/app/globals.css` — shared website grammar and mode tokens
- `src/app/clay-engine.css` — website Clay material adapter
- `src/app/crystal-engine.css` — website Crystal/Liquid Glass material adapter
- `src/data/themes.ts`, `src/lib/useTheme.ts`, `src/lib/useStudio.ts` — legacy runtime/persistence bridge

Do not bypass these with page-specific family selectors.

## Model

The system separates:

1. **Mode** — light, dark, or a runtime system preference.
2. **Family** — material and visual behavior.
3. **Accent** — bounded color override.
4. **Scales** — typography, spacing, radius, elevation, transparency, blur, motion intensity, and speed.
5. **Component variant** — a component contract selected independently of material family.

This prevents “theme” from becoming one untyped bag of CSS overrides.

## Approved families

- **Claymorphism** — solid soft-dimensional surfaces with controlled extrusion; content wells stay flatter.
- **Glassmorphism** — stable translucent panes, frosting, explicit borders, and readable fills. No moving reflection requirement.
- **Liquid Glass** — luminous edge/reflection treatment and restrained environmental movement. It is a visual approximation, not physical refraction.
- **Skeuomorphism** — selective tactile/metal/glass cues over modern layouts; texture is bounded.
- **Neumorphism** — tonal raised/inset controls with borders and focus rings; shadows are never the sole boundary.
- **Flat / Material** — typographic hierarchy, semantic color, clear states, and restrained elevation.
- **Hybrid** — only documented compositions. The first approved hybrid combines Liquid Glass structure with Clay accent controls.

Studio, Monet, and Industrial are legacy website palette identities that migrate to the Material family while retaining their accent/palette through the runtime adapter. Crystal migrates to Liquid Glass; Clay remains Clay.

## Portable versus technology-specific

| Concept | Portable specification | Website adapter | Future WPF translation |
|---|---|---|---|
| Semantic color | Hex color roles | CSS RGB custom properties/Tailwind names | `Color`/`SolidColorBrush` resources |
| Typography | family names, px scale, numeric weights/line heights | self-hosted web fonts and CSS | installed/packaged font families and text styles |
| Spacing/width | numeric pixel scale | rem/clamp/grid/container CSS | thickness, margin, grid sizing resources |
| Shape | radius/border numbers | `border-radius` and Tailwind utilities | `CornerRadius`, `BorderThickness` |
| Elevation | x/y/blur/spread/opacity intent | `box-shadow` recipes | supported shadow/effect/template translation |
| Material | opacity, blur, saturation, edge, extrusion, texture, reflection intent | backdrop filters, gradients, pseudo-elements | WPF brushes/templates/effects; capability-adjusted |
| Motion | duration, easing, distance, stagger and caps | CSS/Motion transitions | WPF storyboards/easing functions |
| State | disabled/loading/focus/emphasis values | native attributes, CSS, ARIA where needed | control templates, triggers, automation properties |

Backdrop filtering, CSS masks, blend modes, pseudo-elements, `content-visibility`, and browser pointer media queries are web-only. They must not enter exported portable configuration as implementation instructions.

## Configuration and safety

`ThemeConfiguration` is the bounded user/preset format. Imported or persisted data must pass `parseThemeConfiguration` before any CSS adapter sees it. Current scale limits are 0.5–1.5 and accents are six-digit hex colors. Schema/version/family/mode mismatches fail closed.

Future schema changes require:

1. incrementing the version;
2. a deterministic migration from every supported prior version;
3. fixtures and round-trip tests;
4. preserving the current no-flash pre-paint contract;
5. never writing an unvalidated value into a CSS custom property.

## Website adapter direction

Phase 1 defines authority but deliberately does not switch the live runtime. The next adapter must map each portable semantic role to existing CSS variables, preserving stored preferences and visual behavior. Required order:

1. read and migrate legacy family/configuration;
2. validate portable configuration;
3. resolve system mode;
4. map the selected mode/family to CSS variables;
5. apply bounded user scales;
6. publish only known attributes/variables;
7. persist the validated configuration;
8. render shared components with no family-specific page branches.

## Enforcement

Run:

```bash
npm run test:theme-system
```

The contract currently verifies family completeness, finite values, full light/dark mappings, Glass/Liquid distinction, Neumorphic boundary/focus requirements, legacy migration targets, and hostile configuration rejection. Later phases must add CSS-adapter parity, contrast matrices, and scoped hard-coded-value enforcement.

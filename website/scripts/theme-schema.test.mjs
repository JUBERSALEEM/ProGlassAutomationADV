import assert from "node:assert/strict";
import test from "node:test";
import { THEME_FAMILY_IDS, parseThemeConfiguration, serializeThemeConfiguration } from "../src/theme/schema.ts";
import { LEGACY_FAMILY_MIGRATION, THEME_SPECS } from "../src/theme/specs.ts";

const hex = /^#[0-9a-f]{6}$/i;
const walkNumbers = (value, path = "root") => {
  if (typeof value === "number") assert.ok(Number.isFinite(value), `${path} must be finite`);
  else if (Array.isArray(value)) value.forEach((item, index) => walkNumbers(item, `${path}[${index}]`));
  else if (value && typeof value === "object") Object.entries(value).forEach(([key, item]) => walkNumbers(item, `${path}.${key}`));
};

test("every approved family has a complete two-mode portable specification", () => {
  assert.deepEqual(Object.keys(THEME_SPECS).sort(), [...THEME_FAMILY_IDS].sort());
  for (const id of THEME_FAMILY_IDS) {
    const spec = THEME_SPECS[id];
    assert.equal(spec.schema, "proglass.theme");
    assert.equal(spec.id, id);
    for (const mode of ["light", "dark"]) {
      assert.ok(spec.modes[mode]);
      for (const [name, color] of Object.entries(spec.modes[mode].colors)) {
        assert.match(color, hex, `${id}.${mode}.${name}`);
      }
    }
    walkNumbers(spec, id);
  }
});

test("glass and liquid glass are distinct material recipes", () => {
  const glass = THEME_SPECS.glass.modes.dark.material;
  const liquid = THEME_SPECS["liquid-glass"].modes.dark.material;
  assert.notDeepEqual(glass, liquid);
  assert.equal(glass.reflection, 0);
  assert.ok(liquid.reflection > 0);
  assert.ok(liquid.blurPx.surface > glass.blurPx.surface);
});

test("neumorphism retains explicit portable border and focus contracts", () => {
  assert.ok(THEME_SPECS.neumorphic.modes.dark.material.neumorphicDepth > 0);
  assert.ok(THEME_SPECS.neumorphic.shape.borderWidthPx.control >= 1);
  assert.ok(THEME_SPECS.neumorphic.states.focusWidthPx >= 2);
});

test("legacy families have an explicit migration target", () => {
  assert.deepEqual(Object.keys(LEGACY_FAMILY_MIGRATION).sort(), ["clay", "crystal", "industrial", "monet", "studio"]);
  for (const target of Object.values(LEGACY_FAMILY_MIGRATION)) assert.ok(THEME_FAMILY_IDS.includes(target));
});

test("untrusted configuration validates and round-trips", () => {
  const config = {
    schema: "proglass.theme-config", version: 1, family: "liquid-glass", mode: "system",
    accent: "#6546B5", typographyScale: 1, spacingScale: 1, radiusScale: 1,
    elevationScale: 1, transparencyScale: 1, blurScale: 1, motionIntensity: 1, motionSpeed: 1,
  };
  const parsed = parseThemeConfiguration(config);
  assert.deepEqual(parsed, config);
  assert.deepEqual(JSON.parse(serializeThemeConfiguration(config)), config);
  assert.equal(parseThemeConfiguration({ ...config, family: "invented" }), null);
  assert.equal(parseThemeConfiguration({ ...config, blurScale: Infinity }), null);
  assert.equal(parseThemeConfiguration({ ...config, accent: "javascript:red" }), null);
});

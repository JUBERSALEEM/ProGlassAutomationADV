import assert from "node:assert/strict";
import { readFileSync, readdirSync, statSync } from "node:fs";
import { join, relative } from "node:path";
import test from "node:test";

const root = new URL("..", import.meta.url).pathname;
const globals = readFileSync(join(root, "src/app/globals.css"), "utf8");
const tailwind = readFileSync(join(root, "tailwind.config.ts"), "utf8");

const walk = (directory) => readdirSync(directory).flatMap((name) => {
  const path = join(directory, name);
  return statSync(path).isDirectory() ? walk(path) : [path];
});

const UI_FILES = [...walk(join(root, "src/app/[locale]")), ...walk(join(root, "src/components"))]
  .filter((path) => /\.(?:ts|tsx)$/.test(path));

/* Literal colour is legitimate only where the value is itself data/artwork or
 * where the browser metadata API requires a resolved color. Pages and ordinary
 * components must request semantic roles instead. */
const LITERAL_ALLOWLIST = [
  /^src\/components\/brand\//,
  /^src\/components\/optimization\/PlateCad\.tsx$/,
  /^src\/components\/layout\/(?:ThemeStudio|AppearancePreview)\.tsx$/,
  /^src\/app\/\[locale\]\/layout\.tsx$/,
];

const stripComments = (source) => source
  .replace(/\/\*[\s\S]*?\*\//g, "")
  .replace(/(^|[^:])\/\/.*$/gm, "$1");

test("page and component markup does not invent unapproved literal colors", () => {
  const violations = [];
  for (const path of UI_FILES) {
    const name = relative(root, path).replaceAll("\\", "/");
    if (LITERAL_ALLOWLIST.some((rule) => rule.test(name))) continue;
    const source = stripComments(readFileSync(path, "utf8"));
    const matches = source.match(/#[0-9a-f]{3,8}\b|(?:bg|text|border)-\[(?:#|rgb\(|hsl\()/gi);
    if (matches?.length) violations.push(`${name}: ${[...new Set(matches)].join(", ")}`);
  }
  assert.deepEqual(violations, [], `Move literal colors to semantic tokens or document a narrow allowlist:\n${violations.join("\n")}`);
});

test("the runtime publishes every core semantic role", () => {
  const roles = [
    "--ink-950", "--ink-900", "--ink-850", "--ink-800", "--ink-700",
    "--line", "--line-soft", "--line-strong",
    "--brand-50", "--brand-100", "--brand-200", "--brand-300", "--brand-400", "--brand-500", "--brand-600", "--brand-700",
    "--text-1", "--text-2", "--text-3", "--text-4",
    "--state-success", "--state-warning", "--state-danger", "--state-info",
    "--surface-alpha", "--surface-alpha-2", "--surface-alpha-3", "--surface-blur", "--surface-sat",
    "--radius-scale", "--shadow-panel", "--shadow-raised",
    "--dur-press", "--dur-hover", "--dur-card", "--dur-lift", "--dur-reveal",
    "--ease-standard", "--ease-out",
  ];
  for (const role of roles) assert.match(globals, new RegExp(`${role.replaceAll("-", "\\-")}\\s*:`), `Missing ${role}`);
});

test("Tailwind maps component colors to semantic CSS variables", () => {
  for (const token of ["--ink-950", "--line", "--brand-500", "--text-1", "--state-success", "--money-positive"])
    assert.ok(tailwind.includes(`var(${token})`), `Tailwind must expose ${token}`);
});

test("material engines remain isolated from page templates", () => {
  for (const path of UI_FILES) {
    const name = relative(root, path).replaceAll("\\", "/");
    const source = stripComments(readFileSync(path, "utf8"));
    /* Root layout publishes the server default; system catalogues deliberately
     * inspect engines. Neither is page-level family-specific composition. */
    if (name === "src/app/[locale]/layout.tsx" || name.includes("/sys/")) continue;
    assert.doesNotMatch(source, /data-appearance\s*=|\[data-appearance=/, `${name} branches on a material family`);
  }
});

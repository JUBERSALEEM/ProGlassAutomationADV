import { fileURLToPath } from "node:url";
import { defineConfig } from "vitest/config";

/*
 * Interaction tests live in `tests/` (DOM-level, jsdom per file via the
 * `@vitest-environment` docblock). The `@` alias mirrors tsconfig paths,
 * and esbuild's automatic JSX runtime stands in for the Next compiler so
 * the production build pipeline stays untouched.
 */
export default defineConfig({
  resolve: {
    alias: {
      "@": fileURLToPath(new URL("./src", import.meta.url)),
    },
  },
  esbuild: {
    jsx: "automatic",
  },
  test: {
    include: ["tests/**/*.test.tsx"],
    environment: "node",
  },
});

"use client";

import { themeBootScript } from "@/data/themes";

/**
 * Emits the synchronous theme boot `<script>` during server HTML rendering
 * only. The inline script removes its own DOM node via `document.currentScript`
 * as soon as it executes in `<head>`, and this component returns `null` on the
 * client so React 19 never reconciles or recreates a `<script>` tag inside a
 * React component tree (avoiding React 19's client `<script>` dev warning on
 * hydration recovery or `[locale]` layout transitions).
 */
export function ThemeBootScript() {
  if (typeof window !== "undefined") return null;
  return <script dangerouslySetInnerHTML={{ __html: themeBootScript }} />;
}

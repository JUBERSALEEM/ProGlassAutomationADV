import type { ReactNode } from "react";

/**
 * Locale route shell.
 *
 * Route entry is CSS-enhanced with `@starting-style` in globals.css. The
 * element's authored state is fully visible, so server HTML, a busy hydration
 * thread, unsupported starting-style, reduced motion, and no JavaScript can
 * never strand a whole page at opacity zero. Supporting browsers interpolate
 * from the short starting style without delaying navigation.
 */
export default function LocaleTemplate({ children }: { children: ReactNode }) {
  return <div className="route-shell">{children}</div>;
}

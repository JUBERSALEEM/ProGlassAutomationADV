import { APP_STRINGS } from "./app-strings";
import { pageOverlayFor } from "./server";
import { I18nProvider } from "./provider";
import type { Locale } from "./locales";
import type { ReactNode } from "react";

/**
 * The page-level overlay provider.
 *
 * Nests under the layout's I18nProvider — which carries only the chrome
 * sentences — and hands the page's client islands the per-route sentence set
 * the overlay build mined for exactly this route (see
 * `scripts/build-route-overlays.mjs`), unioned with the chrome set. The
 * nearest provider wins, so below this component the whole locale dictionary
 * is no longer part of the flight payload; it is replaced by the sentences
 * this route's client code can actually render.
 *
 * Server translator behaviour is unchanged: the page body is still rendered
 * off the full dictionary on the server — the subset is about what is
 * serialized to the browser, never about what the server can say.
 */
export function PageOverlay({
  locale,
  route,
  children,
}: {
  locale: Locale;
  route: string;
  children: ReactNode;
}) {
  const overlay = { ...APP_STRINGS[locale], ...pageOverlayFor(route, locale) };
  const inquiryWorkflow = route === "inquiries" || route.startsWith("inquiries/");
  return (
    <I18nProvider locale={locale} overlay={overlay}>
      {inquiryWorkflow ? <div className="inquiry-route">{children}</div> : children}
    </I18nProvider>
  );
}

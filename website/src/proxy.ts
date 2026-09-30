import { NextResponse, type NextRequest } from "next/server";
import { DEFAULT_LOCALE, isLocale, localeForAcceptLanguage, localeForCountry } from "@/i18n/locales";
import { LOCALE_COOKIE } from "@/i18n/core";

/**
 * The country rule.
 *
 * A visitor from Germany should land in German without being asked and without
 * a language gate in front of the site — a splash page that demands a choice
 * before showing anything is a page most people leave. So the first request is
 * resolved here, in three steps, in this order:
 *
 *   1. **The cookie.** If this browser has chosen a language before, that choice
 *      wins outright. A person's own answer is better evidence than their IP
 *      address, and it is the only one that survives being wrong.
 *   2. **The country.** Vercel puts the request's country in
 *      `x-vercel-ip-country`, and `COUNTRY_LOCALE` maps it to one of the
 *      fifteen languages. This is the step the request asked for: the site
 *      opens in the visitor's language of business, not in English.
 *   3. **The browser's own preference.** `Accept-Language`, for the requests
 *      that arrive without geo data — local development, a self-hosted
 *      deployment, a proxy in front of the site.
 *
 * Failing all three, English. Every step is a preference, never a lock: the
 * language menu sits in the header of every page and its answer is written back
 * into the cookie, so one click outranks the country forever.
 *
 * **The redirect is a redirect, not a rewrite.** A visitor sent to `/de-DE/…`
 * sees that address in the bar, can copy it, and can send it to a colleague who
 * gets the same page. A rewrite would leave everyone on `/` while showing
 * German, and no link would be shareable.
 *
 * **English is the exception, and it has to be handled here.** English
 * addresses are unprefixed, so there is no `/en-US` for a visitor to be sent
 * to and nothing for `/` to resolve against: the pages live under
 * `app/[locale]/`, and a request for `/` finds no page at all. The rewrite
 * below hands the request to the English route while the address bar keeps the
 * bare one — which is what makes `https://site/` work as well as
 * `https://site/en-US`.
 */
export function proxy(request: NextRequest) {
  const { pathname, search } = request.nextUrl;

  const segments = pathname.split("/").filter(Boolean);
  const first = segments[0];

  /* Already in a language — including English, whose addresses are unprefixed.
     Anything under a shipped code is left alone. */
  if (first && isLocale(first)) return NextResponse.next();

  /* Prefix-free by design: these are files and endpoints, not pages — there is
     no German version of a stylesheet, and a static file has no page to be
     re-addressed into a language. `html` is on the list for the same reason
     even though it looks like a page: `public/supabase-check.html` is a
     standalone file, not a route, and without this it would be rewritten to
     `/en-US/supabase-check.html` and 404. */
  if (pathname.startsWith("/api/") || pathname.startsWith("/_next/")) return NextResponse.next();
  if (/\.(?:ico|png|jpg|jpeg|webp|avif|svg|woff2?|txt|xml|json|webmanifest|html?)$/i.test(pathname)) {
    return NextResponse.next();
  }

  const remembered = request.cookies.get(LOCALE_COOKIE)?.value;
  const country = request.headers.get("x-vercel-ip-country") ?? request.headers.get("cf-ipcountry");
  const accept = request.headers.get("accept-language");

  const locale =
    (remembered && isLocale(remembered) ? remembered : null) ??
    localeForCountry(country) ??
    localeForAcceptLanguage(accept) ??
    DEFAULT_LOCALE;

  if (locale === DEFAULT_LOCALE) {
    /* `/` and `/features` are the English addresses; the page itself is built
       by the `[locale]` route. */
    const url = request.nextUrl.clone();
    url.pathname = pathname === "/" ? `/${DEFAULT_LOCALE}` : `/${DEFAULT_LOCALE}${pathname}`;
    url.search = search;
    return NextResponse.rewrite(url);
  }

  const url = request.nextUrl.clone();
  url.pathname = pathname === "/" ? `/${locale}` : `/${locale}${pathname}`;
  url.search = search;

  const response = NextResponse.redirect(url);
  /* Cached for the life of the visit: the same visitor must not be re-resolved
     by country on every navigation, or a stale geo header could bounce them
     back out of the language they picked. */
  response.headers.set("Cache-Control", "private, max-age=600");
  response.headers.set("Vary", "Accept-Language, Cookie");
  return response;
}

export const config = {
  /*
   * Everything except Next's own build output and the metadata files that live
   * at the root by design. Fonts, images and the API routes are handled above
   * as well; the matcher keeps the function from running at all for the bulk of
   * the traffic, which is static assets.
   */
  matcher: ["/((?!_next/static|_next/image|favicon.ico|sitemap.xml|robots.txt|icon.svg).*)"],
};

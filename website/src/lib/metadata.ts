import type { Metadata } from "next";
import { site } from "@/lib/site";
import type { Locale } from "@/i18n/locales";
import { DEFAULT_LOCALE, LOCALE_CODES } from "@/i18n/locales";
import { hrefIn, ogLocale } from "@/i18n/core";

const baseUrl = site.url;

type PageMetadataInput = {
  title: string;
  description: string;
  path: string;
  /** The language this page is written in. English has no address prefix. */
  locale?: Locale;
  keywords?: string[];
  /** Short label rendered above the title on the generated share card. */
  ogEyebrow?: string;
  /** Accent used on the share card, when the page has one of its own. */
  ogAccent?: string;
  type?: "website" | "article";
  publishedTime?: string;
};

/**
 * One helper so every page emits the same, complete metadata envelope:
 * title, description, canonical, language alternates, Open Graph and Twitter.
 *
 * The share image is generated per page by `/api/og` rather than being one
 * static banner reused everywhere, so a link to the optimizer page arrives
 * looking like the optimizer page. The parameters are plain text and a hex
 * colour, and the handler validates both.
 *
 * **The canonical and the alternates are the multilingual part, and getting
 * them wrong is the classic way to lose the other fourteen pages.** Each page
 * declares itself canonical in its own language and lists every language it
 * exists in, so a search engine serves the German page to a German searcher
 * rather than treating it as a duplicate of the English one. `x-default`
 * points at English, which is the right answer for a searcher whose language
 * the site does not ship.
 */
export function pageMetadata({
  title,
  description,
  path,
  locale = DEFAULT_LOCALE,
  keywords,
  ogEyebrow,
  ogAccent,
  type = "website",
  publishedTime,
}: PageMetadataInput): Metadata {
  const href = hrefIn(locale, path);
  const url = `${baseUrl}${href === "/" ? "" : href}`;

  const languages: Record<string, string> = {};
  for (const code of LOCALE_CODES) {
    const localized = hrefIn(code, path);
    languages[code] = `${baseUrl}${localized === "/" ? "" : localized}`;
  }
  languages["x-default"] = languages[DEFAULT_LOCALE] ?? baseUrl;

  const card = new URL("/api/og", baseUrl);
  card.searchParams.set("title", title);
  if (ogEyebrow) card.searchParams.set("eyebrow", ogEyebrow);
  if (ogAccent) card.searchParams.set("accent", ogAccent);
  card.searchParams.set("locale", locale);
  const ogImage = card.toString();

  return {
    title,
    description,
    keywords,
    alternates: { canonical: url, languages },
    openGraph: {
      title,
      description,
      url,
      siteName: site.name,
      type,
      locale: ogLocale(locale),
      images: [{ url: ogImage, width: 1200, height: 630, alt: `${site.name} — ${title}` }],
      ...(publishedTime ? { publishedTime } : {}),
    },
    twitter: {
      card: "summary_large_image",
      title,
      description,
      images: [ogImage],
    },
  };
}

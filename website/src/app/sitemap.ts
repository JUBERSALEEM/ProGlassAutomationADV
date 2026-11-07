import type { MetadataRoute } from "next";
import { site } from "@/lib/site";
import { DEFAULT_LOCALE, LOCALE_CODES } from "@/i18n/locales";
import { hrefIn } from "@/i18n/core";

// The redesigned site has a small public surface: the welcome page and the
// two customer request forms. Private account and inquiry workspaces are not
// included in search indexes.
const publicPages = ["/", "/optimization", "/inquiries/submit", "/inquiries/submit/proforma"];

function alternatesFor(path: string): Record<string, string> {
  const languages: Record<string, string> = {};
  for (const code of LOCALE_CODES) {
    const href = hrefIn(code, path);
    languages[code] = `${site.url}${href === "/" ? "" : href}`;
  }
  languages["x-default"] = languages[DEFAULT_LOCALE] ?? site.url;
  return languages;
}

export default function sitemap(): MetadataRoute.Sitemap {
  const now = new Date();
  return publicPages.flatMap((path, index) =>
    LOCALE_CODES.map((locale) => {
      const href = hrefIn(locale, path);
      return {
        url: `${site.url}${href === "/" ? "" : href}`,
        lastModified: now,
        changeFrequency: path === "/" ? "monthly" : "yearly",
        priority: locale === DEFAULT_LOCALE ? (index === 0 ? 1 : 0.6) : (index === 0 ? 0.9 : 0.5),
        alternates: { languages: alternatesFor(path) },
      };
    }),
  );
}

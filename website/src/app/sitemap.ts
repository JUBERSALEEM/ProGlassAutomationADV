import type { MetadataRoute } from "next";
import { allPosts } from "@/lib/posts";
import { site } from "@/lib/site";
import { DEFAULT_LOCALE, LOCALE_CODES } from "@/i18n/locales";
import { hrefIn } from "@/i18n/core";

/**
 * The sitemap, in every language.
 *
 * A multilingual site whose sitemap lists the English pages only is telling a
 * crawler that the other fourteen do not exist, and the alternate links that
 * would have joined them are never read. So there is one entry per page per
 * language, and each entry carries the full set of `alternates` — the same
 * fifteen links on all fifteen rows, which is what the specification asks for:
 * the alternates must be reciprocal, not one-way.
 *
 * English has no prefix (`/features`), every other language does
 * (`/de-DE/features`). That rule lives in `hrefIn` and is not repeated here.
 */
type ChangeFrequency = MetadataRoute.Sitemap[number]["changeFrequency"];

const pages: { path: string; priority: number; changeFrequency: ChangeFrequency }[] = [
  { path: "/", priority: 1, changeFrequency: "monthly" },
  { path: "/features", priority: 0.9, changeFrequency: "monthly" },
  { path: "/optimization", priority: 0.9, changeFrequency: "monthly" },
  { path: "/how-it-works", priority: 0.8, changeFrequency: "monthly" },
  { path: "/worked-example", priority: 0.9, changeFrequency: "monthly" },
  { path: "/screenshots", priority: 0.8, changeFrequency: "monthly" },
  { path: "/pricing", priority: 0.8, changeFrequency: "monthly" },
  { path: "/reports", priority: 0.8, changeFrequency: "daily" },
  { path: "/records", priority: 0.8, changeFrequency: "daily" },
  { path: "/deliveries", priority: 0.8, changeFrequency: "daily" },
  { path: "/about", priority: 0.7, changeFrequency: "yearly" },
  { path: "/developer", priority: 0.7, changeFrequency: "monthly" },
  { path: "/sponsors", priority: 0.7, changeFrequency: "monthly" },
  { path: "/collaborate", priority: 0.8, changeFrequency: "monthly" },
  { path: "/contact", priority: 0.7, changeFrequency: "yearly" },
  { path: "/resources", priority: 0.6, changeFrequency: "monthly" },
  { path: "/blog", priority: 0.6, changeFrequency: "weekly" },
  { path: "/legal/privacy", priority: 0.3, changeFrequency: "yearly" },
  { path: "/legal/terms", priority: 0.3, changeFrequency: "yearly" },
  { path: "/legal/license", priority: 0.3, changeFrequency: "yearly" },
];

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

  const staticPages = pages.flatMap((page) =>
    LOCALE_CODES.map((locale) => {
      const href = hrefIn(locale, page.path);
      return {
        url: `${site.url}${href === "/" ? "" : href}`,
        lastModified: now,
        changeFrequency: page.changeFrequency,
        priority: locale === DEFAULT_LOCALE ? page.priority : Math.max(0.1, page.priority - 0.1),
        alternates: { languages: alternatesFor(page.path) },
      };
    }),
  );

  const posts = allPosts().flatMap((post) =>
    LOCALE_CODES.map((locale) => {
      const href = hrefIn(locale, `/blog/${post.slug}`);
      return {
        url: `${site.url}${href}`,
        lastModified: new Date(post.date),
        changeFrequency: "yearly" as ChangeFrequency,
        priority: locale === DEFAULT_LOCALE ? 0.6 : 0.5,
        alternates: { languages: alternatesFor(`/blog/${post.slug}`) },
      };
    }),
  );

  return [...staticPages, ...posts];
}

import { nav, site } from "@/lib/site";
import type { Locale } from "@/i18n/locales";
import { DEFAULT_LOCALE } from "@/i18n/locales";
import { hrefIn } from "@/i18n/core";

/** A translator, as this file needs it: sentence in, sentence out. */
type Translate = (key: string) => string;

/**
 * A page's address in one language.
 *
 * Structured data has to point at the page the visitor is actually reading:
 * the English graph belongs at `https://site/features` and the Japanese one at
 * `https://site/ja-JP/features`. `hrefIn` is the only function on the site that
 * knows that rule, so every URL in this file goes through it.
 */
function urlFor(locale: Locale, path: string): string {
  return `${site.url}${hrefIn(locale, path)}`;
}

/**
 * Structured data, in one connected graph.
 *
 * The site used to emit three unrelated JSON-LD blocks: Organization +
 * SoftwareApplication from the layout, a FAQPage from the homepage and an
 * Article from each blog post. Every one of them was schema-valid on its own
 * and none of them referred to any other, which means a crawler had to guess
 * that the article's publisher and the site's organization were the same thing.
 *
 * Here the nodes carry stable `@id`s and reference each other: the
 * `SoftwareApplication` is published by the `Organization`, every `WebPage` is
 * part of the `WebSite` and about the application, and an `Article` is authored
 * by the same `Person` named in the product's About screen. One page, one graph.
 */

const ORG_ID = `${site.url}/#organization`;
const SITE_ID = `${site.url}/#website`;
const SOFTWARE_ID = `${site.url}/#software`;
const DEVELOPER_ID = `${site.url}/#developer`;

export const schemaIds = {
  organization: ORG_ID,
  website: SITE_ID,
  software: SOFTWARE_ID,
  developer: DEVELOPER_ID,
} as const;

/**
 * The nodes every route shares. Rendered once by the root layout.
 *
 * Nothing here is invented: the publisher is the developer named in the
 * application's own About screen, the version is the one the build ships, and
 * the feature list is drawn from the module data rather than written fresh.
 */
export function globalGraph(locale: Locale = DEFAULT_LOCALE, languageName = "English") {
  return {
  "@context": "https://schema.org",
  "@graph": [
    {
      "@type": "Organization",
      "@id": ORG_ID,
      name: site.legalName,
      url: site.url,
      email: site.developerEmail,
      description:
        "ProGlass Automation builds desktop software for glass fabrication businesses: estimation, calculation, cutting optimization, stock, production, delivery, invoicing and reporting.",
      knowsLanguage: [languageName],
      founder: { "@id": DEVELOPER_ID },
    },
    {
      "@type": "WebSite",
      "@id": SITE_ID,
      url: site.url,
      name: site.name,
      description: "Desktop ERP for glass fabrication: estimation, cutting optimization, production, delivery and invoicing.",
      inLanguage: locale,
      publisher: { "@id": ORG_ID },
    },
    {
      "@type": "Person",
      "@id": DEVELOPER_ID,
      name: site.developerName,
      description:
        "Sole developer of ProGlass Automation: estimation, optimization, production and finance workflows for glass fabrication.",
      knowsAbout: [
        "Glass fabrication software",
        "Cutting optimization",
        "Double-glazed unit estimation",
        "Desktop ERP systems",
      ],
      url: urlFor(locale, "/developer"),
    },
    {
      "@type": "SoftwareApplication",
      "@id": SOFTWARE_ID,
      name: site.name,
      applicationCategory: "BusinessApplication",
      applicationSubCategory: "ERP",
      operatingSystem: "Windows 10, Windows 11",
      softwareVersion: site.version,
      softwareRequirements: ".NET 8 desktop runtime",
      description:
        "Windows desktop ERP for glass fabrication businesses: inquiry-to-quotation estimation with SGU, DGU, lamination and DGU+lamination pricing, a guillotine cutting-optimization engine, sheet-store inventory, production stages, multi-movement delivery, VAT invoicing, statements and reporting.",
      publisher: { "@id": ORG_ID },
      author: { "@id": DEVELOPER_ID },
      isAccessibleForFree: false,
      featureList: [
        "Proforma invoice 9-step estimation wizard",
        "SGU, DGU, lamination and DGU+lamination build-up pricing",
        "Guillotine cutting optimization with CNC export",
        "Sheet store inventory with buy/use movements",
        "Production stage workstations and control centre",
        "Multi-movement delivery notes with balance tracking",
        "VAT invoicing with one centralised calculator",
        "Customer statements and reporting",
      ],
    },
  ],
  };
}

/** The label a page is known by in the navigation — used for breadcrumbs. */
function labelFor(path: string, t: Translate = (key) => key): string {
  const groups = [
    ...nav.product,
    ...nav.primary,
    ...nav.footer.product,
    ...nav.footer.company,
    ...nav.footer.resources,
    ...nav.footer.legal,
  ];
  const match = groups.find((entry) => entry.href === path);
  if (match) return t(match.label);
  if (path === "/blog") return t("Blog");
  return path
    .split("/")
    .filter(Boolean)
    .map((part) => part.replace(/-/g, " ").replace(/\b\w/g, (letter) => letter.toUpperCase()))
    .join(" / ");
}

/** A breadcrumb trail, built from the path itself so it cannot drift. */
export function breadcrumbNode(path: string, locale: Locale = DEFAULT_LOCALE, t: Translate = (key) => key) {
  const segments = path.split("/").filter(Boolean);
  const items = [{ name: t("Home"), item: urlFor(locale, "/") }];

  segments.forEach((segment, index) => {
    const href = `/${segments.slice(0, index + 1).join("/")}`;
    items.push({ name: labelFor(href, t), item: urlFor(locale, href) });
  });

  return {
    "@type": "BreadcrumbList",
    "@id": `${urlFor(locale, path)}#breadcrumb`,
    itemListElement: items.map((entry, index) => ({
      "@type": "ListItem",
      position: index + 1,
      name: entry.name,
      item: entry.item,
    })),
  };
}

/**
 * One route's graph: the page itself, its breadcrumb trail, and whatever the
 * page carries — questions it actually renders, or an article it actually is.
 * Every node points back at the shared `@id`s above rather than restating them.
 */
export function routeGraph({
  path,
  title,
  description,
  locale = DEFAULT_LOCALE,
  t = (key: string) => key,
  type = "WebPage",
  faq,
  article,
  items,
}: {
  path: string;
  title: string;
  description: string;
  locale?: Locale;
  t?: Translate;
  type?: "WebPage" | "CollectionPage" | "AboutPage" | "ContactPage";
  faq?: { question: string; answer: string }[];
  article?: { slug: string; title: string; summary: string; date: string; category: string };
  items?: { name: string; url: string }[];
}) {
  const url = urlFor(locale, path);

  const nodes: Record<string, unknown>[] = [
    {
      "@type": type,
      "@id": `${url}#webpage`,
      url,
      name: title,
      description,
      isPartOf: { "@id": SITE_ID },
      about: { "@id": SOFTWARE_ID },
      publisher: { "@id": ORG_ID },
      inLanguage: locale,
      breadcrumb: { "@id": `${site.url}${path}#breadcrumb` },
    },
    breadcrumbNode(path, locale, t),
  ];

  if (items && items.length > 0) {
    nodes.push(itemListNode(path, items, locale));
  }

  if (faq && faq.length > 0) {
    nodes.push({
      "@type": "FAQPage",
      "@id": `${url}#faq`,
      isPartOf: { "@id": `${url}#webpage` },
      mainEntity: faq.map((item) => ({
        "@type": "Question",
        name: item.question,
        acceptedAnswer: { "@type": "Answer", text: item.answer },
      })),
    });
  }

  if (article) {
    const postUrl = urlFor(locale, `/blog/${article.slug}`);
    nodes.push({
      "@type": "Article",
      "@id": `${postUrl}#article`,
      headline: article.title,
      description: article.summary,
      articleSection: article.category,
      datePublished: article.date,
      dateModified: article.date,
      author: { "@id": DEVELOPER_ID },
      publisher: { "@id": ORG_ID },
      isPartOf: { "@id": `${urlFor(locale, "/blog")}#webpage` },
      mainEntityOfPage: { "@id": `${postUrl}#webpage` },
    });
  }

  return { "@context": "https://schema.org", "@graph": nodes };
}

/**
 * A listing page names the things it links to, in order. This is an `ItemList`
 * rather than a second copy of every `Article` node: the articles already
 * describe themselves on their own pages, and restating them here would mean
 * two nodes claiming the same thing.
 */
export function itemListNode(
  path: string,
  items: { name: string; url: string }[],
  locale: Locale = DEFAULT_LOCALE,
) {
  return {
    "@type": "ItemList",
    "@id": `${urlFor(locale, path)}#items`,
    isPartOf: { "@id": `${urlFor(locale, path)}#webpage` },
    numberOfItems: items.length,
    itemListElement: items.map((entry, index) => ({
      "@type": "ListItem",
      position: index + 1,
      name: entry.name,
      url: entry.url.startsWith("/") ? urlFor(locale, entry.url) : entry.url,
    })),
  };
}

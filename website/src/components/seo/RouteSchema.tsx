import { routeGraph } from "@/lib/schema";
import type { Locale } from "@/i18n/locales";

/**
 * One route's structured-data graph.
 *
 * Rendered once per page, next to the page's own content, so the `WebPage`
 * node can carry the same title and description the page actually shows and
 * the breadcrumb trail is derived from the real path. The shared nodes
 * (Organization, WebSite, SoftwareApplication, Person) live in the root layout
 * and are referenced by `@id` rather than repeated.
 *
 * `faq` must be the questions the page really renders — marking up a question
 * a visitor cannot read is the kind of thing that gets structured data ignored.
 */
export function RouteSchema({
  path,
  title,
  description,
  locale,
  type,
  faq,
  article,
  items,
}: {
  path: string;
  title: string;
  description: string;
  /** The language this page is written in; the graph's URLs follow it. */
  locale: Locale;
  type?: "WebPage" | "CollectionPage" | "AboutPage" | "ContactPage";
  faq?: { question: string; answer: string }[];
  article?: { slug: string; title: string; summary: string; date: string; category: string };
  items?: { name: string; url: string }[];
}) {
  const graph = routeGraph({ path, title, description, locale, type, faq, article, items });

  return (
    <script
      type="application/ld+json"
      // Static, developer-authored data: titles, descriptions and data-layer
      // content only. Nothing a visitor typed reaches this string.
      dangerouslySetInnerHTML={{ __html: JSON.stringify(graph) }}
    />
  );
}

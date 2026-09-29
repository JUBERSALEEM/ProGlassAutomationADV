import type { Metadata } from "next";
import { site } from "@/lib/site";

const baseUrl = site.url;

type PageMetadataInput = {
  title: string;
  description: string;
  path: string;
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
 * title, description, canonical, Open Graph and Twitter card.
 *
 * The share image is generated per page by `/api/og` rather than being one
 * static banner reused everywhere, so a link to the optimizer page arrives
 * looking like the optimizer page. The parameters are plain text and a hex
 * colour, and the handler validates both.
 */
export function pageMetadata({
  title,
  description,
  path,
  keywords,
  ogEyebrow,
  ogAccent,
  type = "website",
  publishedTime,
}: PageMetadataInput): Metadata {
  const url = `${baseUrl}${path === "/" ? "" : path}`;

  const card = new URL("/api/og", baseUrl);
  card.searchParams.set("title", title);
  if (ogEyebrow) card.searchParams.set("eyebrow", ogEyebrow);
  if (ogAccent) card.searchParams.set("accent", ogAccent);
  const ogImage = card.toString();

  return {
    title,
    description,
    keywords,
    alternates: { canonical: url },
    openGraph: {
      title,
      description,
      url,
      siteName: site.name,
      type,
      locale: "en_US",
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

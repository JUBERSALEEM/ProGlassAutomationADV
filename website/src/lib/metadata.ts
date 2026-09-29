import type { Metadata } from "next";
import { site } from "@/lib/site";

const baseUrl = site.url;

type PageMetadataInput = {
  title: string;
  description: string;
  path: string;
  keywords?: string[];
  /** Absolute path under /images used for Open Graph. */
  ogImage?: string;
  type?: "website" | "article";
  publishedTime?: string;
};

/**
 * One helper so every page emits the same, complete metadata envelope:
 * title, description, canonical, Open Graph and Twitter card.
 */
export function pageMetadata({
  title,
  description,
  path,
  keywords,
  ogImage = "/images/brand/proglass-hero-banner-1920.png",
  type = "website",
  publishedTime,
}: PageMetadataInput): Metadata {
  const url = `${baseUrl}${path === "/" ? "" : path}`;
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
      images: [{ url: ogImage, width: 1920, height: 640, alt: `${site.name} — ${title}` }],
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

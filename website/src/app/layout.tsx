import type { Metadata, Viewport } from "next";
import localFont from "next/font/local";
import "./globals.css";
import { SvgDefs } from "@/components/brand/SvgDefs";
import { Navbar } from "@/components/layout/Navbar";
import { globalGraph } from "@/lib/schema";
import { Footer } from "@/components/layout/Footer";
import { ThemeStudio } from "@/components/layout/ThemeStudio";
import { site } from "@/lib/site";
import { themeBootScript } from "@/data/themes";

/**
 * Inter for the interface, JetBrains Mono for the technical labels.
 *
 * Both files are self-hosted from `src/fonts` and served from this origin, so
 * opening a page never calls a third-party font host. `display: swap` means
 * text is readable on the first paint rather than waiting on the download.
 * The woff2 files come from the upstream font packages under the SIL Open
 * Font License.
 */
const inter = localFont({
  src: "../fonts/Inter-Variable-latin.woff2",
  display: "swap",
  weight: "100 900",
  variable: "--font-inter",
  fallback: ["Segoe UI", "system-ui", "sans-serif"],
});

const mono = localFont({
  src: "../fonts/JetBrainsMono-Variable-latin.woff2",
  display: "swap",
  weight: "100 800",
  variable: "--font-mono",
  fallback: ["Consolas", "ui-monospace", "monospace"],
});

export const metadata: Metadata = {
  metadataBase: new URL(site.url),
  title: {
    default: `${site.name} — glass business management software for Windows`,
    template: `%s · ProGlass`,
  },
  description:
    "ProGlass Automation is Windows desktop software for glass fabrication businesses: estimation, SGU/DGU/lamination pricing, cutting optimization, sheet-store inventory, production stages, delivery, VAT invoicing and reporting.",
  applicationName: site.name,
  authors: [{ name: site.developerName }],
  creator: site.developerName,
  publisher: site.legalName,
  keywords: [
    "glass estimation software",
    "glass ERP software",
    "glass calculation software",
    "glass cutting optimization",
    "DGU calculator",
    "SGU calculator",
    "glass stock management",
    "glass production management",
    "glass business software",
    "glass optimization software",
  ],
  category: "Business software",
  robots: {
    index: true,
    follow: true,
    googleBot: { index: true, follow: true, "max-image-preview": "large", "max-snippet": -1 },
  },
  openGraph: {
    type: "website",
    siteName: site.name,
    locale: "en_US",
    url: site.url,
    title: `${site.name} — glass business management software for Windows`,
    description:
      "Estimation, SGU/DGU/lamination pricing, cutting optimization, stock, production stages, delivery, VAT invoicing and reporting in one Windows application.",
    images: [{ url: "/api/og?title=Glass%20business%20management%2C%20built%20around%20your%20workflow&eyebrow=Glass%20fabrication%20software", width: 1200, height: 630, alt: site.name }],
  },
  twitter: {
    card: "summary_large_image",
    title: `${site.name} — glass business management software`,
    description:
      "Desktop software for glass fabrication: estimation, calculation, optimization, stock, production, delivery and reporting.",
    images: ["/api/og?title=Glass%20business%20management%2C%20built%20around%20your%20workflow&eyebrow=Glass%20fabrication%20software"],
  },
  icons: {
    icon: [{ url: "/favicon.ico" }, { url: "/images/brand/proglass-icon-256-192.png", type: "image/png", sizes: "192x192" }],
    apple: [{ url: "/images/brand/proglass-icon-256-256.png", sizes: "256x256" }],
  },
};

export const viewport: Viewport = {
  /* The address bar follows the active theme. */
  themeColor: [
    { media: "(prefers-color-scheme: dark)", color: "#0B0820" },
    { media: "(prefers-color-scheme: light)", color: "#F6F5FC" },
  ],
  colorScheme: "dark light",
  width: "device-width",
  initialScale: 1,
};

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html
      lang="en"
      data-theme="midnight"
      data-theme-mode="system"
      data-appearance="studio"
      /* The boot script sets these before paint; the server renders the default. */
      suppressHydrationWarning
      className={`${inter.variable} ${mono.variable}`}
    >
      <head>
        <script dangerouslySetInnerHTML={{ __html: themeBootScript }} />
      </head>
      <body>
        {/*
          One shared gradient sheet for every SVG on the page — see SvgDefs.
          It has to be inside <body>: it is a rendered element, and a
          `display:none` sheet would stop resolving in some engines.
        */}
        <SvgDefs />
        <script
          type="application/ld+json"
          // Static, developer-authored structured data. No user input reaches this string.
          // Every route adds its own graph that references these @ids — see lib/schema.ts.
          dangerouslySetInnerHTML={{ __html: JSON.stringify(globalGraph) }}
        />
        <Navbar />
        <main id="main" className="pt-[var(--nav-height)]">
          {children}
        </main>
        <Footer />
        <ThemeStudio />
      </body>
    </html>
  );
}

import type { Metadata, Viewport } from "next";
import localFont from "next/font/local";
import "./globals.css";
import { Navbar } from "@/components/layout/Navbar";
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
    images: [{ url: "/images/brand/proglass-hero-banner-1920.png", width: 1920, height: 640, alt: site.name }],
  },
  twitter: {
    card: "summary_large_image",
    title: `${site.name} — glass business management software`,
    description:
      "Desktop software for glass fabrication: estimation, calculation, optimization, stock, production, delivery and reporting.",
    images: ["/images/brand/proglass-hero-banner-1920.png"],
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

/** Organization + software-application structured data. */
const structuredData = {
  "@context": "https://schema.org",
  "@graph": [
    {
      "@type": "Organization",
      "@id": `${site.url}/#organization`,
      name: site.legalName,
      url: site.url,
      email: site.developerEmail,
      description:
        "ProGlass Automation builds desktop software for glass fabrication businesses: estimation, calculation, cutting optimization, stock, production, delivery, invoicing and reporting.",
    },
    {
      "@type": "SoftwareApplication",
      "@id": `${site.url}/#software`,
      name: site.name,
      applicationCategory: "BusinessApplication",
      applicationSubCategory: "ERP",
      operatingSystem: "Windows 10, Windows 11",
      softwareVersion: site.version,
      description:
        "Windows desktop ERP for glass fabrication businesses: inquiry-to-quotation estimation with SGU, DGU, lamination and DGU+lamination pricing, a guillotine cutting-optimization engine, sheet-store inventory, production stages, multi-movement delivery, VAT invoicing, statements and reporting.",
      publisher: { "@id": `${site.url}/#organization` },
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
        <script
          type="application/ld+json"
          // Static, developer-authored structured data. No user input reaches this string.
          dangerouslySetInnerHTML={{ __html: JSON.stringify(structuredData) }}
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

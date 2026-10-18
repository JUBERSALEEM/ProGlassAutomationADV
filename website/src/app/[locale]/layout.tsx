import type { Metadata, Viewport } from "next";
import localFont from "next/font/local";
import { notFound } from "next/navigation";
import "../globals.css";
import "../clay-engine.css";
import "../crystal-engine.css";
import "../top-panel-compact.css";
import "../responsive-card-grid.css";
import "../component-system.css";
import { SvgDefs } from "@/components/brand/SvgDefs";
import { Navbar } from "@/components/layout/Navbar";
import { globalGraph } from "@/lib/schema";
import { Footer } from "@/components/layout/Footer";
import { ThemeStudioLazy } from "@/components/layout/ThemeStudioLazy";
import { brandIcon } from "@/data/assets";
import { site } from "@/lib/site";
import { buildStamp } from "@/lib/buildStamp";
import { ThemeBootScript } from "@/components/layout/ThemeBootScript";
import { I18nProvider } from "@/i18n/provider";
import { LOCALE_CODES, dirOf, isLocale, localeInfo, type Locale } from "@/i18n/locales";
import { ogLocale } from "@/i18n/core";
import { SpeedInsights } from "@vercel/speed-insights/next";
import { GlassLife } from "@/components/glass/GlassLife";
import { SupportBubble } from "@/components/support/SupportBubble";
import { chromeOverlayFor, translatorForLocale } from "@/i18n/server";
import { APP_STRINGS } from "@/i18n/app-strings";

/**
 * Inter for the interface, JetBrains Mono for the technical labels.
 *
 * Both files are self-hosted from `src/fonts` and served from this origin, so
 * opening a page never calls a third-party font host. `display: swap` means
 * text is readable on the first paint rather than waiting on the download.
 * The woff2 files come from the upstream font packages under the SIL Open
 * Font License.
 *
 * Neither carries `preload: true` — and that is measured, not an oversight.
 * The studio's default typeface is Segoe UI, a pure system-font stack with
 * zero downloads, so a launch that preloads every shipped face would spend
 * 280 KB of bandwidth before a visitor has read a word: on faces only the
 * four people who open the appearance studio ever see. Without a preload the
 * browser fetches a face the moment a stylesheet actually names it — for the
 * studio dials that instant is the dial itself, and `display: swap` keeps the
 * page entirely readable while the woff2 arrives.
 *
 * The path is `../../fonts/` rather than `../fonts/` because this layout moved
 * one segment deeper when the site gained languages: it now lives at
 * `src/app/[locale]/layout.tsx`, and every relative path in it had to move with
 * it. A wrong path here does not fail the build — it fails it four times, once
 * per font, with "Font file not found".
 */
const inter = localFont({
  src: "../../fonts/Inter-Variable-latin.woff2",
  display: "swap",
  weight: "100 900",
  variable: "--font-inter",
  fallback: ["Segoe UI", "system-ui", "sans-serif"],
  preload: false,
});

const mono = localFont({
  src: "../../fonts/JetBrainsMono-Variable-latin.woff2",
  display: "swap",
  weight: "100 800",
  variable: "--font-mono",
  fallback: ["Consolas", "ui-monospace", "monospace"],
  preload: false,
});

/*
 * The two optional faces offered by the appearance studio's typeface control.
 *
 * Both are loaded here rather than swapped in from a network host, for the same
 * reason as the two above: the privacy page states plainly that opening a page
 * never calls a third-party font service, and a font switcher that fetched from
 * a CDN would make that sentence false. Each carries the upright and the italic
 * of its variable weight axis, so bold, bold italic and underline all have a
 * real face to draw with instead of a synthesised slant.
 *
 * `preload: false` for the same measured reason as Inter and JetBrains Mono —
 * see the comment block above them.
 */
const serif = localFont({
  src: [
    { path: "../../fonts/SourceSerif4-Variable-latin.woff2", style: "normal" },
    { path: "../../fonts/SourceSerif4-Variable-latin-italic.woff2", style: "italic" },
  ],
  display: "swap",
  weight: "200 900",
  variable: "--font-serif",
  fallback: ["Georgia", "Times New Roman", "serif"],
  preload: false,
});

const plex = localFont({
  src: [
    { path: "../../fonts/IBMPlexSans-Variable-latin.woff2", style: "normal" },
    { path: "../../fonts/IBMPlexSans-Variable-latin-italic.woff2", style: "italic" },
  ],
  display: "swap",
  weight: "100 700",
  variable: "--font-plex",
  fallback: ["Segoe UI", "system-ui", "sans-serif"],
  preload: false,
});

/**
 * Nunito — the round voice the Clay appearance asks for. Claymorphism pairs
 * its chunky radii with chunky round type (the spec's own shortlist:
 * Quicksand, Nunito, Outfit, Fredoka); Inter's grotesk terminals fight that
 * mood, so the family re-roots `--font-sans` to this file and everything
 * inherits. Self-hosted like the rest (SIL OFL via @fontsource-variable/
 * nunito), latin + latin-ext subsets, no preload — the face downloads at the
 * moment of choosing Clay and not before.
 */
const nunito = localFont({
  src: "../../fonts/Nunito-Variable-latin.woff2",
  display: "swap",
  weight: "200 1000",
  variable: "--font-nunito",
  fallback: ["Segoe UI", "system-ui", "sans-serif"],
  preload: false,
});

/**
 * Every language, prerendered.
 *
 * `dynamicParams = false` is the other half of that: a language tag the site
 * does not ship does not render English with a foreign-looking URL, it 404s.
 * A request for `/xx/features` should be told there is nothing there, not
 * quietly handed the English page.
 */
export function generateStaticParams() {
  return LOCALE_CODES.map((locale) => ({ locale }));
}

export const dynamicParams = false;

type LayoutProps = { params: Promise<{ locale: string }> };

/**
 * Metadata is built from the translator, not from constants.
 *
 * The title, the description, the keywords and the share card's own title are
 * all sentences like any other, so switching language switches them — which is
 * what a search engine and a chat client both need in order to show the right
 * language to the person following the link.
 */
export async function generateMetadata({ params }: LayoutProps): Promise<Metadata> {
  const { locale } = await params;
  if (!isLocale(locale)) notFound();
  const t = translatorForLocale(locale);

  const title = t("Glass business management software for Windows");
  const description = t(
    "ProGlass Automation is Windows desktop software for glass fabrication businesses: estimation, SGU/DGU/lamination pricing, cutting optimization, sheet-store inventory, production stages, delivery, VAT invoicing and reporting.",
  );
  const ogTitle = `${site.name} — ${title}`;

  const share = new URL("/api/og", site.url);
  share.searchParams.set("title", ogTitle);
  share.searchParams.set("eyebrow", t("Glass fabrication software"));
  share.searchParams.set("locale", locale);

  return {
    metadataBase: new URL(site.url),
    title: { default: ogTitle, template: `%s · ${site.name}` },
    description,
    applicationName: site.name,
    authors: [{ name: site.developerName }],
    creator: site.developerName,
    publisher: site.legalName,
    keywords: [
      t("glass estimation software"),
      t("glass ERP software"),
      t("glass calculation software"),
      t("glass cutting optimization"),
      "DGU calculator",
      "SGU calculator",
      t("glass stock management"),
      t("glass production management"),
      t("glass business software"),
      t("glass optimization software"),
    ],
    category: "Business software",
    /* The pages each declare their own canonical and language alternates; this
       is the site-wide floor for anything that forgets. */
    alternates: {
      canonical: locale === "en-US" ? site.url : `${site.url}/${locale}`,
      languages: Object.fromEntries(
        LOCALE_CODES.map((code) => [code, code === "en-US" ? site.url : `${site.url}/${code}`]),
      ),
    },
    robots: {
      index: true,
      follow: true,
      googleBot: { index: true, follow: true, "max-image-preview": "large", "max-snippet": -1 },
    },
    openGraph: {
      type: "website",
      siteName: site.name,
      locale: ogLocale(locale),
      alternateLocale: LOCALE_CODES.filter((code) => code !== locale).map(ogLocale),
      url: locale === "en-US" ? site.url : `${site.url}/${locale}`,
      title: ogTitle,
      description,
      images: [{ url: share.toString(), width: 1200, height: 630, alt: site.name }],
    },
    twitter: {
      card: "summary_large_image",
      title: ogTitle,
      description,
      images: [share.toString()],
    },
    manifest: "/manifest.webmanifest",
    icons: {
      // The application's own icon, through the manifest, so the file that is
      // advertised here and the file that is optimised below it cannot drift.
      icon: [{ url: "/favicon.ico" }, { url: brandIcon.small, type: "image/png", sizes: "192x192" }],
      apple: [{ url: brandIcon.large, sizes: "256x256" }],
    },
  };
}

export const viewport: Viewport = {
  /* The address bar follows the active theme. */
  themeColor: [
    { media: "(prefers-color-scheme: dark)", color: "#080516" },
    { media: "(prefers-color-scheme: light)", color: "#F8F5EE" },
  ],
  colorScheme: "dark light",
  width: "device-width",
  initialScale: 1,
};

/**
 * The document element carries the language and the direction.
 *
 * `lang` is the language of the text in the page, which is what a screen reader
 * uses to choose a voice and what the browser uses to hyphenate — an Arabic
 * page marked `lang="en"` is read aloud as English letters. `dir="rtl"` on
 * `<html>` is what makes Arabic and Urdu lay out from the right; every rule
 * that cares is written with logical properties so it flips with it.
 */
export default async function LocaleLayout({
  children,
  params,
}: {
  children: React.ReactNode;
  params: Promise<{ locale: string }>;
}) {
  const { locale: tag } = await params;
  if (!isLocale(tag)) notFound();
  const locale: Locale = tag;
  /* The layout's provider ships the chrome overlay only — see server.ts. The
     page's own sentences arrive at a deeper provider: PageOverlay, wrapped
     by each page around its own content. */
  const overlay = { ...APP_STRINGS[locale], ...chromeOverlayFor(locale) };
  /* The footer renders on the server and takes its translator as a prop, so
     the layout builds one for the chrome around the page body. */
  const t = translatorForLocale(locale);

  return (
    <html
      lang={locale}
      dir={dirOf(locale)}
      data-locale={locale}
      data-theme="midnight"
      data-theme-mode="system"
      data-appearance="crystal"
      data-scroll-behavior="smooth"
      data-build-time={buildStamp.builtAt}
      data-build-rev={buildStamp.revision ?? ""}
      /* The boot script sets these before paint; the server renders the default. */
      suppressHydrationWarning
      className={`${inter.variable} ${mono.variable} ${serif.variable} ${plex.variable} ${nunito.variable}`}
    >
      <head>
        <ThemeBootScript />
        {/*
          No-JS resilience (Phase 8). Entrance states start hidden by design —
          `.reveal` at opacity 0, motion writing its hidden variant inline,
          accordion panels at 0fr — and only script reveals them. Without
          script the whole page would sit invisible, so this sheet (which only
          ever applies when scripting is off) puts every gated element back to
          its resting state. `!important` is required: it has to beat motion's
          inline styles, and nothing with script enabled ever sees this block.
        */}
        <noscript>
          <style>{`.reveal,.reveal-scale,.motion-reveal,.motion-stagger,.motion-stagger>div,.route-shell{opacity:1!important;transform:none!important}.accordion-panel{grid-template-rows:1fr!important}`}</style>
        </noscript>
      </head>
      <body suppressHydrationWarning>
        {/*
          The living-liquid scene (crystal-engine §L): three accent-hued
          blobs that drift forever behind the wallpaper, so Crystal panes
          refract moving colour like liquid in a clear bottle. Pure DOM —
          globals.css keeps it display:none and the Crystal engine alone
          switches it on, so every other family pays exactly nothing.
        */}
        <div className="liquid-scene" aria-hidden="true">
          <span />
          <span />
          <span />
        </div>
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
          dangerouslySetInnerHTML={{ __html: JSON.stringify(globalGraph(locale, localeInfo(locale).english)) }}
        />
        {/*
          One dictionary per page. The provider hands the language and its
          overlay to every client component below it, so a button in the footer
          and a chart label in a module preview read the same translator as the
          heading above them.
        */}
        <I18nProvider locale={locale} overlay={overlay}>
          {/* The first keyboard stop bypasses the persistent navigation. It is
              visually hidden until focused and uses the page language. */}
          <a href="#main" className="skip-link">
            {t("Skip to content")}
          </a>
          {/* §C5 living glass — one listener for every lifted surface,
              gated twice (JS: hover+fine+no-reduce; CSS: reduce unpaints).
              Renders nothing. */}
          <GlassLife />
          <Navbar />
          <div className="site-stage">
            <main id="main" className="site-main pt-[var(--nav-height)]" tabIndex={-1}>
              {children}
            </main>
            <Footer t={t} />
          </div>
          <ThemeStudioLazy />
          {/* Inside the provider, not beside it: outside the context the
              support bubble's labels and links silently resolved through the
              English fallback on every non-English page. */}
          <SupportBubble />
        </I18nProvider>
        <SpeedInsights />
      </body>
    </html>
  );
}

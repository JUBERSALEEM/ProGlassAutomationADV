"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { useEffect, useRef, useState } from "react";
import { cn } from "@/lib/cn";
import { navIcons } from "@/data/navIcons";
import { site } from "@/lib/site";
import { ArrowRight, ButtonLink } from "@/components/ui/Button";
import { ThemeToggle } from "@/components/layout/ThemeToggle";
import { LanguageMenu, LanguageMenuBlock } from "@/components/layout/LanguageMenu";
import { Logo } from "@/components/brand/Logo";
import { useT } from "@/i18n/provider";

type NavChild = { label: string; href: string; description?: string };
type NavEntry = { label: string; href?: string; children?: NavChild[] };

const entries: NavEntry[] = [
  /* Home comes first and is a plain link: the way back to the front page should
     never be behind a menu. */
  { label: "Home", href: "/" },
  {
    label: "Product",
    children: [
      { label: "Features", href: "/features", description: "Every module, in the app's own vocabulary" },
      { label: "How It Works", href: "/how-it-works", description: "Inquiry to statement, step by step" },
      { label: "Worked Example", href: "/worked-example", description: "One real job, every figure" },
      { label: "Screenshots", href: "/screenshots", description: "The interface, screen by screen" },
      { label: "Pricing", href: "/pricing", description: "Editions and what each one unlocks" },
      { label: "Reports", href: "/reports?status=Confirmed", description: "Confirmed proformas from Excel Sync" },
      { label: "Job Order", href: "/job-orders", description: "Release Confirmed PIs and track production progress" },
      {
        label: "Deliveries",
        href: "/deliveries",
        description: "Delivery Notes, part loads and outstanding balance",
      },
    ],
  },
  { label: "Optimization", href: "/optimization" },
  {
    label: "Resources",
    children: [
      { label: "Resources", href: "/resources", description: "Guides to the glass workflows" },
      { label: "Blog", href: "/blog", description: "Notes on the build and the industry" },
      { label: "Sponsors", href: "/sponsors", description: "Back the build, or join it" },
    ],
  },
  /* About and Developer are separate pages with separate jobs, and both get a
     top-level slot rather than being buried in a menu. */
  { label: "About", href: "/about" },
  { label: "Developer", href: "/developer" },
  /*
   * Collaboration takes the top-level slot, and Sponsors keeps the one it
   * already had inside this same nav's Resources menu (and in the footer, and
   * on the collaboration page itself). Adding an eighth top-level item instead
   * would push the pill past what `lg` has room for, and a navigation that
   * wraps is worse than one page reached by two routes.
   */
  { label: "Collaborate", href: "/collaborate" },
];

/*
 * The top panel is a card holding cards.
 *
 * It used to be bare words on the page background that only lit up under the
 * pointer, which read as an unstyled menu bar bolted onto a product whose whole
 * interface is cards. Now the group of items sits inside one raised card — the
 * card edge, the 16 px panel corner, the same violet wash and hairline the
 * rest of the site uses — and each item inside it is a card of its own, with a
 * transparent edge that only resolves on hover. The entry you are on, or have
 * open, keeps its edge and its wash, so the panel always says where you are.
 */
const NAV_BAR = "nav-pill hidden items-center gap-0.5 rounded-panel p-1 lg:flex";
const NAV_CARD =
  "relative flex items-center gap-1.5 rounded-card border-2 px-2.5 py-1.5 text-sm font-semibold transition-colors duration-200";
const NAV_CARD_OFF =
  "border-transparent text-text-muted hover:border-line-strong/80 hover:bg-ink-800/70 hover:text-text-primary";
const NAV_CARD_ON = "border-brand-400/60 bg-brand-500/14 text-text-primary";

function NavGlyph({ path }: { path?: string }) {
  if (!path) return null;
  return (
    <svg
      aria-hidden="true"
      viewBox="0 0 16 16"
      className="h-3.5 w-3.5 shrink-0 opacity-80"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.6"
      strokeLinecap="round"
      strokeLinejoin="round"
    >
      <path d={path} />
    </svg>
  );
}

export function Navbar() {
  const t = useT();
  const pathname = usePathname();
  const [scrolled, setScrolled] = useState(false);
  const [openMenu, setOpenMenu] = useState<string | null>(null);
  const [mobileOpen, setMobileOpen] = useState(false);
  const closeTimer = useRef<ReturnType<typeof setTimeout> | null>(null);

  useEffect(() => {
    const onScroll = () => setScrolled(window.scrollY > 12);
    onScroll();
    window.addEventListener("scroll", onScroll, { passive: true });
    return () => window.removeEventListener("scroll", onScroll);
  }, []);

  // Route change closes everything. Adjusting state during render (rather than
  // in an effect) is the documented React pattern for "reset when a prop
  // changes", and it avoids a second render pass with the menu still open.
  const [lastPath, setLastPath] = useState(pathname);
  if (lastPath !== pathname) {
    setLastPath(pathname);
    setMobileOpen(false);
    setOpenMenu(null);
  }

  // Escape closes the open surface.
  useEffect(() => {
    const onKey = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        setOpenMenu(null);
        setMobileOpen(false);
      }
    };
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, []);

  // Lock scroll behind the mobile sheet.
  useEffect(() => {
    document.body.style.overflow = mobileOpen ? "hidden" : "";
    return () => {
      document.body.style.overflow = "";
    };
  }, [mobileOpen]);

  const isActive = (entry: NavEntry) => {
    if (entry.href) return pathname === entry.href || pathname.startsWith(`${entry.href}/`);
    return entry.children?.some((c) => pathname === c.href || pathname.startsWith(`${c.href}/`)) ?? false;
  };

  const scheduleClose = () => {
    if (closeTimer.current) clearTimeout(closeTimer.current);
    closeTimer.current = setTimeout(() => setOpenMenu(null), 140);
  };
  const cancelClose = () => {
    if (closeTimer.current) clearTimeout(closeTimer.current);
  };

  return (
    <header
      className={cn(
        // `no-print`: a fixed bar that repeats on every printed sheet is waste.
        "no-print fixed inset-x-0 top-0 z-50 transition-all duration-300 ease-industrial",
        scrolled
          /*
           * The bar is opaque in the three opaque families — a blurred,
           * 65%-opaque bar puts the navigation, the most-read text on the site,
           * on a composited layer, and composited text loses subpixel
           * antialiasing, which is a real cost for no visible gain when the
           * surface behind it is a solid colour anyway.
           *
           * Crystal is the exception, and it is decided in CSS (`.nav-glass`)
           * rather than here: the family whose whole language is a floating
           * sheet of glass cannot have a solid slab of ink across the top of
           * it. The bar itself still ships the same two states.
           */
          ? "nav-glass"
          : "border-b border-transparent bg-transparent",
      )}
    >
      <a
        href="#main"
        className="sr-only focus:not-sr-only focus:absolute focus:left-4 focus:top-3 focus:z-50 focus:rounded-lg focus:bg-brand-400 focus:px-3 focus:py-2 focus:text-sm focus:font-semibold focus:text-accent-ink"
      >
        {t("Skip to content")}
      </a>

      <div className="shell flex h-[var(--nav-height)] items-center justify-between gap-4">
        <Link href="/" className="group flex items-center gap-3" aria-label={`${site.name} — home`}>
          <Logo size={34} />
        </Link>

        {/* ── Desktop navigation ─────────────────────────────────── */}
        <nav aria-label={t("Primary")} className={NAV_BAR}>
          {entries.map((entry) => {
            const active = isActive(entry);
            if (!entry.children) {
              return (
                <Link
                  key={t(entry.label)}
                  href={entry.href ?? "#"}
                  aria-current={active ? "page" : undefined}
                  className={cn(NAV_CARD, active ? NAV_CARD_ON : NAV_CARD_OFF)}
                >
                  <NavGlyph path={navIcons[entry.label]} />
                  {t(entry.label)}
                </Link>
              );
            }

            const open = openMenu === entry.label;
            return (
              <div
                key={t(entry.label)}
                className="relative"
                onMouseEnter={() => {
                  cancelClose();
                  setOpenMenu(entry.label);
                }}
                onMouseLeave={scheduleClose}
              >
                <button
                  type="button"
                  aria-expanded={open}
                  aria-haspopup="true"
                  onClick={() => setOpenMenu(open ? null : entry.label)}
                  className={cn(NAV_CARD, active || open ? NAV_CARD_ON : NAV_CARD_OFF)}
                >
                  <NavGlyph path={navIcons[entry.label]} />
                  {t(entry.label)}
                  <svg
                    aria-hidden="true"
                    viewBox="0 0 12 12"
                    className={cn("h-2.5 w-2.5 transition-transform duration-200", open && "rotate-180")}
                    fill="none"
                    stroke="currentColor"
                    strokeWidth="1.6"
                    strokeLinecap="round"
                  >
                    <path d="M2.5 4.5 6 8l3.5-3.5" />
                  </svg>
                </button>

                {open ? (
                  /*
                   * The menu is a stack of cards, not a list of links.
                   *
                   * It used to be plain rows with a hover fill — the panel had
                   * the card edge, but nothing inside it did, so it read as a
                   * dropdown bolted onto a product whose whole interface is
                   * cards. Each destination is now a card in its own right:
                   * numbered, titled, described, with the same edge, radius and
                   * accent-on-hover treatment as every other card on the site.
                   */
                  <div className="absolute left-1/2 top-full max-h-[calc(100vh-var(--nav-height))] w-[22rem] -translate-x-1/2 overflow-y-auto overscroll-contain pt-3">
                    <div className="menu-card animate-scale-in rounded-panel p-2 shadow-raised">
                      <p className="mono-label px-2.5 pb-2 pt-1">{t(entry.label)}</p>
                      <ul className="space-y-1.5">
                        {entry.children.map((child, index) => (
                          <li key={child.href}>
                            <Link
                              href={child.href}
                              className="card-interactive group flex items-start gap-3 rounded-card border-2 border-line-strong/60 bg-ink-900/70 px-3 py-2.5"
                            >
                              <span
                                aria-hidden="true"
                                className="mt-0.5 grid h-6 w-6 shrink-0 place-items-center rounded-[0.4rem] border border-brand-400/35 bg-brand-500/12 font-mono text-[0.625rem] font-bold text-brand-200"
                              >
                                {String(index + 1).padStart(2, "0")}
                              </span>
                              <span className="min-w-0 flex-1">
                                <span className="flex items-center gap-1.5 text-sm font-semibold text-text-primary">
                                  {t(child.label)}
                                  <ArrowRight className="opacity-0 transition-opacity duration-200 group-hover:opacity-100" />
                                </span>
                                {child.description ? (
                                  <span className="mt-0.5 block text-xs font-medium leading-relaxed text-text-muted">
                                    {t(child.description)}
                                  </span>
                                ) : null}
                              </span>
                            </Link>
                          </li>
                        ))}
                      </ul>
                    </div>
                  </div>
                ) : null}
              </div>
            );
          })}
        </nav>

        <div className="hidden items-center gap-2 lg:flex">
          {/* The language menu sits with the theme control: both are settings
              about how the site presents itself, not navigation through it. */}
          <LanguageMenu />
          <ThemeToggle />
          <ButtonLink href="/screenshots" variant="ghost" size="sm">
            {t("See the app")}
          </ButtonLink>
          <ButtonLink href="/contact" size="sm" trailing={<ArrowRight />}>
            {t("Request a Demo")}
          </ButtonLink>
        </div>

        {/* ── Mobile trigger ─────────────────────────────────────── */}
        <div className="flex items-center gap-2 lg:hidden">
          <LanguageMenu compact />
          <ThemeToggle />
          <button
            type="button"
            onClick={() => setMobileOpen((v) => !v)}
            aria-expanded={mobileOpen}
            aria-controls="mobile-menu"
            className="flex h-11 w-11 items-center justify-center rounded-xl border border-line-strong/70 bg-ink-900/70 text-text-primary transition-colors duration-200 hover:border-brand-400/50"
          >
            <span className="sr-only">{mobileOpen ? "Close menu" : "Open menu"}</span>
            <svg viewBox="0 0 20 20" className="h-4.5 w-4.5" fill="none" stroke="currentColor" strokeWidth="1.7" strokeLinecap="round">
              {mobileOpen ? <path d="M5 5l10 10M15 5 5 15" /> : <path d="M3 6h14M3 10h14M3 14h14" />}
            </svg>
          </button>
        </div>
      </div>

      {/* ── Mobile sheet ─────────────────────────────────────────── */}
      <div
        id="mobile-menu"
        hidden={!mobileOpen}
        className={cn(
          "lg:hidden",
          mobileOpen && "nav-sheet animate-fade-in",
        )}
      >
        <div className="shell max-h-[calc(100vh-var(--nav-height))] overflow-y-auto py-5">
          <nav aria-label={t("Mobile")} className="space-y-6">
            {entries.map((entry) => (
              <div key={t(entry.label)}>
                {entry.href ? (
                  <Link
                    href={entry.href}
                    className="flex items-center justify-between border-b border-line/50 pb-2 text-base font-semibold text-text-primary"
                  >
                    {t(entry.label)}
                    <ArrowRight />
                  </Link>
                ) : (
                  <>
                    <p className="eyebrow mb-2">{t(entry.label)}</p>
                    {/* The same card treatment as the desktop menu: a sheet
                        of cards, numbered, each with its own edge. */}
                    <ul className="space-y-1.5">
                      {entry.children?.map((child, index) => (
                        <li key={child.href}>
                          <Link
                            href={child.href}
                            className="card-interactive group flex items-start gap-3 rounded-card border-2 border-line-strong/60 bg-ink-900/70 px-3 py-2.5"
                          >
                            <span
                              aria-hidden="true"
                              className="mt-0.5 grid h-6 w-6 shrink-0 place-items-center rounded-[0.4rem] border border-brand-400/35 bg-brand-500/12 font-mono text-[0.625rem] font-bold text-brand-200"
                            >
                              {String(index + 1).padStart(2, "0")}
                            </span>
                            <span className="min-w-0 flex-1">
                              <span className="text-sm font-semibold text-text-primary">{t(child.label)}</span>
                              {child.description ? (
                                <span className="mt-0.5 block text-xs font-medium leading-snug text-text-muted">
                                  {t(child.description)}
                                </span>
                              ) : null}
                            </span>
                          </Link>
                        </li>
                      ))}
                    </ul>
                  </>
                )}
              </div>
            ))}
          </nav>

          {/* The full list, spelled out on its own row — a dropdown inside a
              sheet is a second thing to open. */}
          <div className="mt-6">
            <LanguageMenuBlock />
          </div>

          <div className="mt-7 flex flex-col gap-2.5">
            <ButtonLink href="/contact" size="lg" trailing={<ArrowRight />}>
              {t("Request a Demo")}
            </ButtonLink>
            <ButtonLink href="/optimization" variant="secondary" size="lg">
              {t("See the optimization engine")}
            </ButtonLink>
          </div>

          <p className="mt-6 font-mono text-2xs uppercase tracking-[0.18em] text-text-faint font-medium">
            {t("Desktop software · v{version} · No account required", { version: site.version })}
          </p>
        </div>
      </div>
    </header>
  );
}

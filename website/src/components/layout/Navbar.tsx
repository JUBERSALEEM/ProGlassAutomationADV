"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { useEffect, useRef, useState } from "react";
import { cn } from "@/lib/cn";
import { navIcons } from "@/data/navIcons";
import { site } from "@/lib/site";
import { ArrowRight, ButtonLink } from "@/components/ui/Button";
import { ThemeModeSwitch } from "@/components/layout/ThemeModeSwitch";
import { ThemeToggle } from "@/components/layout/ThemeToggle";
import { LanguageMenu, LanguageMenuBlock } from "@/components/layout/LanguageMenu";
import { BuildStampChip, BuildStampPill, BuildStampRow } from "@/components/layout/BuildStamp";
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
   * on the collaboration page itself). An eighth top-level item would push the
   * last card from `2xl` to `wide` in the ladder below, which is the honest
   * price of one — but a page reached by two routes is still better than a
   * navigation that wraps, and this one is reachable from the footer, the
   * Resources menu and its own page.
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
const NAV_BAR = "nav-pill hidden items-center gap-0.5 rounded-panel p-1 xl:flex";
/*
 * The card ladder.
 *
 * Seven cards, the settings, the call to action and a stamp that carries the
 * site's name, the date and the time in Gulf Standard Time do not fit in
 * 1280px — measured, they need about 1450. So the cards arrive in three
 * steps, widest last: the first three at `xl`, the next two at `2xl`, and the
 * last two at `wide`, the one tier where the panel fits everything it owns.
 * The Menu button holds the cards that are not on screen yet, and steps aside
 * at `wide`. Below `xl` the pill is not rendered at all and the Menu button
 * holds all seven.
 *
 * `scripts/measure-panel.py` measures each tier and checks these lines
 * against its own model, so the tool cannot go on reporting room that the
 * markup does not have — which is exactly what it did when the seventh entry
 * was added and it was still counting six; and for one pass the roles
 * flipped, with the markup showing all seven at `2xl` while the model counted
 * five — the two now agree on this ladder, checked at edit time.
 */
const NAV_CARD_FIRST_TIER = 3;
const NAV_CARD_SECOND_TIER = 5;
const NAV_CARD_TIERS = [
  "hidden xl:flex",
  "hidden 2xl:flex",
  "hidden wide:flex",
] as const;
const NAV_CARD =
  "relative flex items-center gap-1.5 rounded-card border-2 px-2.5 py-1.5 text-sm font-semibold transition-all duration-200 wide:px-3";
const NAV_CARD_OFF =
  "border-transparent text-text-muted hover:border-line-strong/80 hover:bg-ink-800/70 hover:text-text-primary";
/*
 * The active card.
 *
 * It keeps its edge and takes a real fill rather than a 14%-alpha wash: the
 * navigation is the one element a visitor reads on every page, and "where am I"
 * has to survive being glanced at while scrolling. The accent bar on the
 * leading edge is the only ornament, and it is the same mark the application
 * puts on a selected rail row.
 */
const NAV_CARD_ON =
  "border-brand-500/60 bg-brand-600/30 text-text-primary shadow-[inset_0_1px_0_0_rgb(255_255_255/0.07)] before:absolute before:inset-y-1.5 before:start-0 before:w-[3px] before:rounded-full before:bg-brand-300";

function NavGlyph({ path }: { path?: string }) {
  if (!path) return null;
  return (
    <svg
      aria-hidden="true"
      viewBox="0 0 16 16"
      className="hidden h-3.5 w-3.5 shrink-0 opacity-80 wide:block"
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
        className="sr-only focus:not-sr-only focus:absolute focus:left-4 focus:top-3 focus:z-50 focus:rounded-lg focus:bg-brand-600 focus:px-3 focus:py-2 focus:text-sm focus:font-semibold focus:text-accent-ink"
      >
        {t("Skip to content")}
      </a>

      <div className="shell flex h-[var(--nav-height)] items-center justify-between gap-4">
        <Link href="/" className="group flex items-center gap-3" aria-label={`${site.name} — home`}>
          <Logo size={34} wordmarkClassName="hidden lg:flex" />
        </Link>

        {/*
         * The build's sync time, after the logo, on the widths the pill
         * cannot afford.
         *
         * Below `lg` the three-line pill has no room — it waits on the right
         * for 1024 and up — and the only stamp a phone reader got was the
         * footer, two scrolls away from the question "which build is this".
         * The chip prints the one fact in the one place that is always on
         * screen: the time the build shipped, in Gulf Standard Time, with
         * the full two-zone tooltip the pill carries. It steps aside at `lg`
         * precisely when the pill takes over, so the bar never pays for both
         * at once — the same shed-ladder rule as the rest of this row, and
         * measured into the phone and sm tiers like everything else in it.
         */}
        <BuildStampChip t={t} className="lg:hidden" />

        {/* ── Desktop navigation ─────────────────────────────────── */}
        <nav aria-label={t("Primary")} className={NAV_BAR}>
          {entries.map((entry, index) => {
            const active = isActive(entry);
            const tier =
              index < NAV_CARD_FIRST_TIER
                ? NAV_CARD_TIERS[0]
                : index < NAV_CARD_SECOND_TIER
                  ? NAV_CARD_TIERS[1]
                  : NAV_CARD_TIERS[2];
            if (!entry.children) {
              return (
                <Link
                  key={t(entry.label)}
                  href={entry.href ?? "#"}
                  aria-current={active ? "page" : undefined}
                  className={cn(NAV_CARD, tier, active ? NAV_CARD_ON : NAV_CARD_OFF)}
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
                className={cn(tier, "relative")}
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
          {/*
            The panel's own settings, in two controls rather than one: the
            reading (Light · System · Dark) and the palette. They answer
            different questions, so they are different controls — see
            `ThemeModeSwitch` and `ThemeToggle`.

            The language menu sits with them: all three are settings about how
            the site presents itself, not navigation through it.
          */}
          {/*
            The way to the pages that are not cards yet.

            The settings cluster arrives at `lg` and the nav cards arrive in
            three steps — three at `xl`, five at `2xl`, all seven at `wide` —
            so the Menu button holds whatever is missing above 1024: all seven
            at `lg`, the last four at `xl`, the last two at `2xl`. It is the
            same sheet the phone menu opens, from the same component, so
            nothing is ever unreachable. It goes at `wide`, the one tier where
            all seven cards are on screen.

            Measured, the full row needs about 1660px of content; the ladder is
            in `scripts/measure-panel.py` and the numbers are in the README.
          */}
          <button
            type="button"
            onClick={() => setMobileOpen((value) => !value)}
            aria-expanded={mobileOpen}
            aria-controls="mobile-menu"
            className={cn(
              "hidden items-center gap-1.5 rounded-card border-2 px-2.5 py-1.5 text-sm font-semibold transition-all duration-200 lg:flex wide:hidden",
              mobileOpen
                ? "border-brand-500/60 bg-brand-600/30 text-text-primary"
                : "border-line-strong/60 text-text-muted hover:border-line-strong/80 hover:bg-ink-800/70 hover:text-text-primary",
            )}
          >
            <svg
              viewBox="0 0 20 20"
              aria-hidden="true"
              className="h-4 w-4 shrink-0 opacity-80"
              fill="none"
              stroke="currentColor"
              strokeWidth="1.7"
              strokeLinecap="round"
            >
              <path d="M3 6h14M3 10h14M3 14h14" />
            </svg>
            {t("Menu")}
          </button>
          <LanguageMenu />
          <ThemeModeSwitch />
          <ThemeToggle />
          {/*
            There used to be a ghost "See the app" button here, shown from
            1920px. It is gone, and the page it pointed at is not: Screenshots
            is the first child of the Product menu, one hover away, and at 1920
            the row has better uses for 126px — the stamp card now carries the
            site's name, the date and the time in Gulf Standard Time, and it is
            the sight of that card that answers "which build am I looking at".
            A shortcut to a page that is also a menu child is the cheapest thing
            in this row to lose; the primary call to action is not.
          */}
          <ButtonLink href="/contact" size="sm" trailing={<ArrowRight />}>
            {t("Request a Demo")}
          </ButtonLink>

          {/*
            The build stamp, at the end of the panel where chrome belongs.

            It appears at `lg` and not before: the stamp carries the site's own
            name, the date and the time in Gulf Standard Time, so a reader on a
            1024px window gets the answer to "which build is this, and when was
            it made" without opening anything. It is measured rather than
            guessed — `scripts/measure-panel.py` holds the arithmetic and says
            the row still fits with slack at every tier — and below `lg` it is
            still on every page, in the mobile panel and in the footer, where
            there is room to print the revision and both clocks. The divider at
            `2xl` is what tells the eye this is a fact about the build rather
            than one more thing to click.
          */}
          <span aria-hidden="true" className="mx-1 hidden h-6 w-px bg-line/70 2xl:block" />
          <BuildStampPill t={t} className="hidden lg:block" />
        </div>

        {/* ── Mobile trigger ─────────────────────────────────────── */}
        <div className="flex items-center gap-2 lg:hidden">
          {/*
            The language button waits for `sm`. At 360px the row measures 297px
            in the 305px a phone leaves it once the shell's own padding is
            counted — which fits, and fits by 8px, on the one width where a
            device is smallest and a scrollbar is not the only thing that can
            change. The language list is one tap away in the sheet below, where
            all fifteen languages are spelled out in their own names, so nothing
            is lost by waiting. The palette stays: the sheet has no other way to
            reach the palette families.
          */}
          <span className="hidden sm:flex">
            <LanguageMenu compact />
          </span>
          <ThemeModeSwitch className="hidden sm:flex" />
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
        aria-hidden={!mobileOpen}
        className={cn(
          "nav-sheet transition-[opacity,transform,visibility] duration-300 ease-industrial xl:hidden",
          mobileOpen
            ? "pointer-events-auto visible translate-y-0 opacity-100 will-change-transform"
            : "pointer-events-none invisible -translate-y-2 opacity-0",
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

          {/* The same stamp as the desktop panel, with room for the revision. */}
          <BuildStampRow t={t} className="mt-3" />
        </div>
      </div>
    </header>
  );
}

"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { useEffect, useRef, useState } from "react";
import { cn } from "@/lib/cn";
import { navIcons } from "@/data/navIcons";
import { footerIconFor } from "@/data/footerIcons";
import { site } from "@/lib/site";
import { ArrowRight, ButtonLink } from "@/components/ui/Button";
import { Icon } from "@/components/ui/Icon";
import { ThemeModeSwitch } from "@/components/layout/ThemeModeSwitch";
import { ThemeToggle } from "@/components/layout/ThemeToggle";
import { LanguageMenu, LanguageMenuBlock } from "@/components/layout/LanguageMenu";
import { BuildStampChip, BuildStampPill, BuildStampRow } from "@/components/layout/BuildStamp";
import { Logo } from "@/components/brand/Logo";
import { useT } from "@/i18n/provider";
import { useAppearance } from "@/lib/useTheme";
import { slidePillHandlers, slidePillTo, restSlidePill } from "@/components/layout/slidePill";
import { hrefIn, localeOfPath, pathWithoutLocale } from "@/i18n/core";

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
  /* The family decides one thing here: whether the bar's material is
     continuous (Crystal, §U) or two-stated (the opaque families). It
     comes from the DOM-attribute store, so server and client agree
     from the first render — no state-in-effect, no flip. */
  const family = useAppearance();
  const pathname = usePathname();
  /*
   * `usePathname` returns the URL as the browser speaks it, and every route
   * in this app lives under /[locale]/ — so the first segment is the
   * language, not a page. Comparing raw, `isActive` matched nothing beyond
   * "/", which meant: no `aria-current` on the bar, and the clay bar's
   * indicator (§15) had no current page to rest on — it only appeared
   * under the pointer. The LanguageMenu already strips with
   * `pathWithoutLocale`; the bar must do the same.
   */
  const barePath = pathWithoutLocale(pathname ?? "");
  const activeLocale = localeOfPath(pathname ?? "");
  const toHref = (path: string) => hrefIn(activeLocale, path);
  const [scrolled, setScrolled] = useState(false);
  const [openMenu, setOpenMenu] = useState<string | null>(null);
  const [mobileOpen, setMobileOpen] = useState(false);
  /*
   * Whether the sheet's CONTENT exists at all — three lives it leads:
   *
   *   1. Server render and first client render: empty. The sheet used to be
   *      a full menu painted `invisible` beneath the bar, and that hidden
   *      full menu cost a real launch debt: Next's prefetch watcher
   *      (use-intersection.js) reads geometry through an
   *      IntersectionObserver, and `visibility: hidden` is not geometry —
   *      every card link in the sheet intersected the viewport as far as the
   *      watcher could tell, so the phone that had just opened a page quietly
   *      prefetched the RSC payloads of a dozen more. Mounting the content
   *      only on demand removes the observers, which removes the traffic.
   *
   *   2. Open and closing: mounted. Unmounting the instant the visitor asks
   *      to close would cut the 300 ms fade to a hard cut, so the content
   *      outlives the open state by exactly one transition.
   *
   *   3. Hydration safety: the gate below is a state change inside an effect,
   *      so the server's output and the first client render agree on the
   *      empty sheet — no mismatch, no recover-and-redraw.
   */
  const [sheetMounted, setSheetMounted] = useState(false);
  const closeTimer = useRef<ReturnType<typeof setTimeout> | null>(null);
  /*
   * Hover opens; a click PINS. Without this the two gestures fight: the
   * wrapper opens the menu on mouse-enter, so the very first click arrives
   * at an already-open menu and would toggle it shut — from the visitor's
   * side the button "needs a second click" to do anything.
   *
   * The pin carries one invariant, and every path below keeps it:
   * `pinnedMenu` is only ever the label of the menu that is OPEN right now.
   * The pin sets it (click), the deliberate re-click on the same trigger
   * clears it (the one toggle-off), a hover switch to a sibling reverts to
   * hover mode, and the outside-pointer-down / Escape / route-change paths
   * clear it with the rest. If a pin could outlive its menu — which the
   * first version of this allowed, closing a pinned menu on hover-out but
   * keeping the pin — the next click landed on the stale pin and CLOSED
   * instead of opening: the "double click" bug, baked into the state
   * machine rather than the hand. `tests/navbar.test.tsx` walks these
   * sequences against a real DOM so they cannot silently regress again.
   */
  const pinnedMenu = useRef<string | null>(null);
  const navListRef = useRef<HTMLDivElement | null>(null);
  const headerRef = useRef<HTMLElement | null>(null);

  /* ── The clay bar's sliding pill ────────────────────────────────────
   * Under `[data-appearance="clay"]` the bar paints a raised pill that
   * slides to the item under the pointer and rests on the current page
   * (§15 of the engine; the pack's demo cycle is switched off). All this
   * component owns is the geometry: measure, translate, hand back. In
   * other families the pill stays invisible — nothing here is theirs.
   *
   * Third sighting: this used to live in React state, and `onPointerOver`
   * BUBBLES — moving the pointer across a link's glyph, text span and
   * inner nodes fires it for every boundary it crosses, each event
   * forcing two layout reads (`getBoundingClientRect`) plus a setState
   * that re-rendered the whole header. On a slow phone that reads as a
   * menu that freezes while the hand moves across it. The pill is now
   * written imperatively (style writes only, no re-render), and the
   * hover handler dedupes: the same target and the same geometry touch
   * the DOM at most once. */
  const indicatorRef = useRef<HTMLSpanElement | null>(null);
  const menuListRef = useRef<HTMLUListElement | null>(null);
  const sheetNavRef = useRef<HTMLElement | null>(null);
  const hoverTargetRef = useRef<HTMLElement | null>(null);
  const moveIndicator = (target: HTMLElement | null) => {
    const list = navListRef.current;
    const pill = indicatorRef.current;
    if (!list || !pill) return;
    if (!target) {
      hoverTargetRef.current = null;
      if (pill.dataset.geo) {
        delete pill.dataset.geo;
        pill.style.opacity = "0";
      }
      return;
    }
    hoverTargetRef.current = target;
    const listRect = list.getBoundingClientRect();
    const rect = target.getBoundingClientRect();
    if (rect.width === 0) {
      if (pill.dataset.geo) {
        delete pill.dataset.geo;
        pill.style.opacity = "0";
      }
      return;
    }
    const x = Math.max(0, rect.left - listRect.left - 4);
    const geo = `${Math.round(x)}x${Math.round(rect.width)}`;
    if (pill.dataset.geo === geo) return; /* same item, same box: no work */
    pill.dataset.geo = geo;
    /* The pill is parked at the list's left padding, so the slide is the
       item's left edge measured from that same parking spot. */
    pill.style.opacity = "1";
    pill.style.width = `${rect.width}px`;
    pill.style.transform = `translateX(${x}px)`;
  };
  const settleIndicator = () => {
    const list = navListRef.current;
    if (!list) return;
    /* Resting place, in order: the open menu's trigger, the current
       page's own link, then a menu trigger whose child is current
       (Product while on /features). Plain links carry aria-current
       and menu triggers carry data-nav-active, so the pill rests
       under every kind of entry the same way (§AE — one pattern
       for Home, About, Product, Resources alike). */
    moveIndicator(
      list.querySelector<HTMLElement>('[aria-expanded="true"]') ??
        list.querySelector<HTMLElement>('[aria-current="page"]') ??
        list.querySelector<HTMLElement>('[data-nav-active="true"]'),
    );
  };
  useEffect(() => {
    /* Route changes re-seat the pill; fonts and the tier ladder can move
       items too, so measure again after paint and on resize. The late pass
       is the one the reference bar (the NavClay pack: a pill pinned to the
       active index, its demo cycle off) gets for free with widths baked at
       load: our labels are re-typeset when the overlay dictionary lands,
       so the first measurement can stale before fonts/overlay finish —
       settle once more on the next microtask wave and once when the
       document's fonts report ready. */
    const settle = () => settleIndicator();
    const frame = requestAnimationFrame(settle);
    const late = window.setTimeout(settle, 420);
    document.fonts?.ready.then(settle).catch(() => undefined);
    window.addEventListener("resize", settle);
    return () => {
      cancelAnimationFrame(frame);
      clearTimeout(late);
      window.removeEventListener("resize", settle);
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [pathname]);

  /* The open menu's sliding pill (§AF) rests on its current
     destination when the menu opens — the same resting rule as the
     bar, one level down. Only one menu is open at a time, so one
     list ref serves every dropdown. */
  useEffect(() => {
    if (!openMenu) return;
    const rest = () => {
      if (menuListRef.current) restSlidePill(menuListRef.current);
    };
    const frame = requestAnimationFrame(rest);
    const late = window.setTimeout(rest, 120);
    return () => {
      cancelAnimationFrame(frame);
      clearTimeout(late);
    };
  }, [openMenu, pathname]);

  /* The sheet's destination lists get the same resting rule when
     the sheet opens (§AF): each group's pill sits on the page the
     visitor is already on, where that group holds it. */
  useEffect(() => {
    if (!mobileOpen || !sheetMounted) return;
    const rest = () => {
      sheetNavRef.current
        ?.querySelectorAll<HTMLElement>("ul")
        .forEach((ul) => restSlidePill(ul));
    };
    const frame = requestAnimationFrame(rest);
    const late = window.setTimeout(rest, 140);
    return () => {
      cancelAnimationFrame(frame);
      clearTimeout(late);
    };
  }, [mobileOpen, sheetMounted, pathname]);

  useEffect(() => {
    if (mobileOpen) return; /* Mounting happens in the toggle handler itself. */
    /* One transition length past the 300 ms ease — see the comment above. */
    const done = setTimeout(() => setSheetMounted(false), 320);
    return () => clearTimeout(done);
  }, [mobileOpen]);

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
  /* Ref writes don't belong to render (react-hooks/refs), so the pin clears
     in an effect that follows the same route change — the menu itself has
     already closed in the render pass above, and a pin cleared a frame
     later can only make the next close MORE permissive, never less. */
  useEffect(() => {
    pinnedMenu.current = null;
  }, [pathname]);

  // Escape closes the open surface.
  // A pointer-down outside the bar closes whatever the bar opened.
  useEffect(() => {
    if (!openMenu) return;
    const onPointer = (event: PointerEvent) => {
      const list = navListRef.current;
      if (list && !list.contains(event.target as Node)) {
        pinnedMenu.current = null;
        setOpenMenu(null);
      }
    };
    document.addEventListener("pointerdown", onPointer);
    return () => document.removeEventListener("pointerdown", onPointer);
  }, [openMenu]);

  useEffect(() => {
    if (!mobileOpen) return;
    const onPointer = (event: PointerEvent) => {
      const header = headerRef.current;
      if (header && !header.contains(event.target as Node)) {
        setMobileOpen(false);
      }
    };
    document.addEventListener("pointerdown", onPointer);
    return () => document.removeEventListener("pointerdown", onPointer);
  }, [mobileOpen]);

  useEffect(() => {
    const onKey = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        pinnedMenu.current = null;
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
    if (entry.href) return barePath === entry.href || barePath.startsWith(`${entry.href}/`);
    return (
      entry.children?.some((c) => barePath === c.href || barePath.startsWith(`${c.href}/`)) ?? false
    );
  };

  const scheduleClose = () => {
    if (closeTimer.current) clearTimeout(closeTimer.current);
    closeTimer.current = setTimeout(() => {
      closeTimer.current = null;
      /* The grace timer closes hover-opens only. A pinned menu — one the
         visitor clicked to hold — survives the hand leaving; it closes on
         the re-click on its trigger, on a pointer-down outside the bar, on
         Escape, or on a route change, each of which also clears the pin. */
      if (pinnedMenu.current) return;
      setOpenMenu(null);
    }, 140);
  };
  const cancelClose = () => {
    if (closeTimer.current) clearTimeout(closeTimer.current);
  };

  return (
    <header
      ref={headerRef}
      className={cn(
        // `no-print`: a fixed bar that repeats on every printed sheet is waste.
        "no-print fixed inset-x-0 top-0 z-50 transition-all duration-300 ease-industrial",
        scrolled || family === "crystal"
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
           * it.
           *
           * §U: in Crystal the bar wears that sheet in BOTH states. The
           * two-state swap — transparent ↔ film under this header's
           * `transition-all` — crossfaded the bar's entire material at
           * the 12px boundary: a visible flash on every arrival at the
           * top and every departure from it, worst on touch, where the
           * scrolled state is a near-opaque film (§S). A floating sheet
           * of glass does not pop into existence twelve pixels into the
           * page, so in Crystal the scroll flip is a no-op for the bar.
           * The opaque families keep their two states untouched.
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
        <Link href={toHref("/")} className="group flex items-center gap-3" aria-label={`${site.name} — home`}>
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
        <nav
          ref={navListRef}
          aria-label={t("Primary")}
          className={NAV_BAR}
          onPointerOver={(event) => {
            const target = (event.target as HTMLElement).closest("a, button");
            /* pointerover fires for every inner-node boundary; only a new
               target is new information — the dedupe is what keeps the
               menu from re-measuring (and before, re-rendering) per
               crossing. The open menu's own cards are inside this nav too:
               without the menu-card guard the pill teleported 22rem across
               to whatever dropdown item the hand entered — it read as the
               bar glitching on every menu visit. */
            if (
              target &&
              target !== hoverTargetRef.current &&
              !target.closest(".menu-card") &&
              navListRef.current?.contains(target)
            ) {
              moveIndicator(target as HTMLElement);
            }
          }}
          onPointerLeave={() => settleIndicator()}
          onFocusCapture={(event) => {
            const target = (event.target as HTMLElement).closest("a, button");
            if (
              target &&
              target !== hoverTargetRef.current &&
              !target.closest(".menu-card") &&
              navListRef.current?.contains(target)
            ) {
              moveIndicator(target as HTMLElement);
            }
          }}
          onBlurCapture={(event) => {
            if (!navListRef.current?.contains(event.relatedTarget as Node)) settleIndicator();
          }}
        >
          <span aria-hidden="true" className="navclay__indicator" ref={indicatorRef} style={{ opacity: 0 }} />
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
                  href={entry.href ? toHref(entry.href) : "#"}
                  aria-current={active ? "page" : undefined}
                  onClick={() => {
                    pinnedMenu.current = null;
                    setOpenMenu(null);
                  }}
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
                  /* Moving to a sibling hands the bar back to hover: the pin
                     belongs to the menu that is open, and a hover-opened
                     sibling must never inherit it (a stale pin is what made
                     the next click close instead of open). */
                  if (openMenu !== entry.label) pinnedMenu.current = null;
                  /* pointer-guard-ok: onMouseEnter fires once per entry
                     boundary (it does not bubble from children), and React
                     bails on an identical state value — this is the menu
                     open call itself, not per-frame pointer work. */
                  setOpenMenu(entry.label);
                }}
                onMouseLeave={scheduleClose}
              >
                <button
                  type="button"
                  aria-expanded={open}
                  aria-haspopup="true"
                  data-nav-active={active ? "true" : undefined}
                  onClick={() => {
                    cancelClose();
                    if (open && pinnedMenu.current === entry.label) {
                      /* The one toggle-off: a re-click on the trigger the
                         visitor pinned. Reachable ONLY while that menu is
                         visibly open — the pin invariant guarantees it. */
                      pinnedMenu.current = null;
                      setOpenMenu(null);
                      return;
                    }
                    /* Closed menu, or one open by hover that the click now
                       commits to: pin it open. The click never slams the
                       menu it lands on — that was the "needs a double
                       click" defect. */
                    pinnedMenu.current = entry.label;
                    setOpenMenu(entry.label);
                  }}
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
                   * glyph, titled, described, with the same edge, radius and
                   * accent-on-hover treatment as every other card on the site —
                   * the mark is the one the footer and the banner already use.
                   *
                   * The panel is sized to its contents (§AC): one fixed
                   * 22rem used to serve both the eight-destination Product
                   * menu and the three-destination Resources menu, so the
                   * short menu read as an oversized empty slab and the long
                   * one as a cramped column. Short menus take 19rem, long
                   * ones 22rem, and neither may run past the viewport edge
                   * when its trigger sits near one.
                   */
                  <div
                    className={cn(
                      "absolute left-1/2 top-full max-h-[calc(100vh-var(--nav-height))] max-w-[calc(100vw-1.5rem)] -translate-x-1/2 overflow-y-auto overscroll-contain pt-3",
                      entry.children.length > 4 ? "w-[22rem]" : "w-[19rem]",
                    )}
                  >
                    <div className="menu-card animate-scale-in rounded-panel p-2 shadow-raised">
                      <p className="mono-label px-2.5 pb-2 pt-1">{t(entry.label)}</p>
                      <ul
                        ref={menuListRef}
                        className="relative space-y-1.5"
                        {...slidePillHandlers(() => menuListRef.current)}
                      >
                        <span aria-hidden="true" className="menu-slide-pill" />
                        {entry.children.map((child) => {
                          const childPath = child.href.split(/[?#]/)[0];
                          return (
                          <li key={child.href}>
                            <Link
                              href={toHref(child.href)}
                              aria-current={
                                barePath === childPath || barePath.startsWith(`${childPath}/`)
                                  ? "page"
                                  : undefined
                              }
                              onClick={() => {
                                pinnedMenu.current = null;
                                setOpenMenu(null);
                              }}
                              className="card-interactive group flex items-start gap-3 rounded-card border-2 border-line-strong/60 bg-ink-900/70 px-3 py-2.5"
                            >
                              <span
                                aria-hidden="true"
                                className="mt-0.5 grid h-6 w-6 shrink-0 place-items-center rounded-[0.4rem] border border-brand-400/35 bg-brand-500/12 text-brand-200"
                              >
                                <Icon path={footerIconFor(child.href)} className="h-3.5 w-3.5" />
                              </span>
                              <span className="min-w-0 flex-1">
                                <span className="flex items-center gap-1.5 text-sm font-semibold text-text-primary">
                                  {t(child.label)}
                                  <ArrowRight className="-translate-x-1 opacity-0 transition-all duration-300 ease-industrial group-hover:translate-x-0 group-hover:opacity-100" />
                                </span>
                                {child.description ? (
                                  <span className="mt-0.5 block text-xs font-medium leading-relaxed text-text-muted">
                                    {t(child.description)}
                                  </span>
                                ) : null}
                              </span>
                            </Link>
                          </li>
                          );
                        })}
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
            onClick={() => {
              const opening = !mobileOpen;
              if (opening) {
                setSheetMounted(true);
              }
              setMobileOpen(opening);
            }}
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
          <ButtonLink href={toHref("/contact")} size="sm" trailing={<ArrowRight />}>
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
            onClick={() => {
              const opening = !mobileOpen;
              if (opening) {
                /* The sheet's content mounts with the click, inside the same
                   fade-in, so the first frame never shows an empty panel — and
                   no observer for its links has existed before this moment. */
                setSheetMounted(true);
              }
              setMobileOpen(opening);
            }}
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

      {/* ── Mobile sheet ───────────────────────────────────────────
         * Absolutely anchored under the bar, never part of the flow.
         *
         * THE GHOST, recorded so it never returns: a sheet that is only made
         * invisible but left IN the layout still takes its full viewport of
         * space inside this fixed header. On scroll, the header's own glass
         * background then paints through that invisible hundred-viewport-high
         * sheet — a full-page panel that had no business existing, on top of
         * content that was fine. Anchored absolutely, closed or open, the
         * sheet participates in nothing that isn't its own box.
         */}
        <div
          id="mobile-menu"
          aria-hidden={!mobileOpen}
          className={cn(
            "nav-sheet absolute inset-x-0 top-full transition-[opacity,transform,visibility] duration-300 ease-industrial wide:hidden",
            mobileOpen
              ? "pointer-events-auto visible translate-y-0 opacity-100 will-change-transform"
              : "pointer-events-none invisible -translate-y-2 opacity-0",
          )}
        >
        {sheetMounted ? (
        <div className="shell max-h-[calc(100vh-var(--nav-height))] overflow-y-auto py-5">
          <nav
            aria-label={t("Mobile")}
            className="space-y-6"
            ref={sheetNavRef}
            onPointerOver={(event) => {
              const target = (event.target as HTMLElement).closest<HTMLElement>("a");
              const listEl = target?.closest<HTMLElement>("ul");
              if (target && listEl) slidePillTo(listEl, target);
            }}
            onPointerLeave={() => {
              sheetNavRef.current
                ?.querySelectorAll<HTMLElement>("ul")
                .forEach((ul) => restSlidePill(ul));
            }}
            onFocusCapture={(event) => {
              const target = (event.target as HTMLElement).closest<HTMLElement>("a");
              const listEl = target?.closest<HTMLElement>("ul");
              if (target && listEl) slidePillTo(listEl, target);
            }}
            onBlurCapture={(event) => {
              if (!sheetNavRef.current?.contains(event.relatedTarget as Node)) {
                sheetNavRef.current
                  ?.querySelectorAll<HTMLElement>("ul")
                  .forEach((ul) => restSlidePill(ul));
              }
            }}
          >
            {entries.map((entry) => (
              <div key={t(entry.label)}>
                {entry.href ? (
                  <Link
                    href={toHref(entry.href)}
                    onClick={() => setMobileOpen(false)}
                    /* The open page must be inked on the sheet without a
                       gesture — the same current-page story the clay bar
                       tells at rest (§15). */
                    aria-current={
                      barePath === entry.href || barePath.startsWith(`${entry.href}/`)
                        ? "page"
                        : undefined
                    }
                    className="flex items-center justify-between border-b border-line/50 pb-2 text-base font-semibold text-text-primary"
                  >
                    {t(entry.label)}
                    <ArrowRight />
                  </Link>
                ) : (
                  <>
                    <p className="eyebrow mb-2">{t(entry.label)}</p>
                    {/* The same card treatment as the desktop menu: a sheet
                        of cards, each carrying the destination's own mark. */}
                    <ul className="relative space-y-1.5">
                      <span aria-hidden="true" className="menu-slide-pill" />
                      {entry.children?.map((child) => {
                        const childPath = child.href.split(/[?#]/)[0];
                        return (
                        <li key={child.href}>
                          <Link
                            href={toHref(child.href)}
                            onClick={() => setMobileOpen(false)}
                            aria-current={
                              barePath === childPath || barePath.startsWith(`${childPath}/`)
                                ? "page"
                                : undefined
                            }
                            className="card-interactive group flex items-start gap-3 rounded-card border-2 border-line-strong/60 bg-ink-900/70 px-3 py-2.5"
                          >
                            <span
                              aria-hidden="true"
                              className="mt-0.5 grid h-6 w-6 shrink-0 place-items-center rounded-[0.4rem] border border-brand-400/35 bg-brand-500/12 text-brand-200"
                            >
                              <Icon path={footerIconFor(child.href)} className="h-3.5 w-3.5" />
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
                        );
                      })}
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
            <ButtonLink href={toHref("/contact")} size="lg" trailing={<ArrowRight />} onClick={() => setMobileOpen(false)}>
              {t("Request a Demo")}
            </ButtonLink>
            <ButtonLink href={toHref("/optimization")} variant="secondary" size="lg" onClick={() => setMobileOpen(false)}>
              {t("See the optimization engine")}
            </ButtonLink>
          </div>

          <p className="mt-6 font-mono text-2xs uppercase tracking-[0.18em] text-text-faint font-medium">
            {t("Desktop software · v{version} · No account required", { version: site.version })}
          </p>

          {/* The same stamp as the desktop panel, with room for the revision. */}
          <BuildStampRow t={t} className="mt-3" />
        </div>
        ) : null}
      </div>
    </header>
  );
}

// @vitest-environment jsdom

/*
 * The desktop top-panel menus, walked against a real DOM with real pointer
 * sequences — the suite that should have existed before two rounds of
 * "verified by reading the source" told the visitor the menus worked while
 * on their screen they did not.
 *
 * What is exercised here is the state machine in `Navbar.tsx`: hover opens
 * instantly, the grace timer closes hover-opens only, a click pins, a
 * re-click on the pinned trigger is the one toggle-off, a hover switch to
 * a sibling reverts to hover mode, and the outside-pointer-down / Escape
 * paths drop whatever is open. The CSS half of the defect — the pill's
 * `overflow: hidden` clipping every panel before it could paint — is
 * guarded in `scripts/nav-pill-clip.test.mjs`.
 */

import { afterEach, describe, expect, it, vi } from "vitest";
import { act, cleanup, fireEvent, render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import React from "react";

vi.mock("next/navigation", () => ({
  usePathname: () => "/about",
}));

vi.mock("next/link", () => ({
  default: ({
    href,
    children,
    onClick,
    ...rest
  }: {
    href: string;
    children: React.ReactNode;
    onClick?: React.MouseEventHandler<HTMLAnchorElement>;
  }) =>
    React.createElement(
      "a",
      {
        href,
        onClick: (e: React.MouseEvent<HTMLAnchorElement>) => {
          onClick?.(e);
          e.preventDefault();
        },
        ...rest,
      },
      children,
    ),
}));

/* Words pass through untranslated; the suite asserts on the English source. */
vi.mock("@/i18n/provider", () => ({
  useT: () => (copy: string) => copy,
}));

/* Neighbouring chrome is not the unit under test — keep the tree to the bar. */
vi.mock("@/components/layout/ThemeModeSwitch", () => ({ ThemeModeSwitch: () => null }));
vi.mock("@/components/layout/ThemeToggle", () => ({ ThemeToggle: () => null }));
vi.mock("@/components/layout/LanguageMenu", () => ({
  LanguageMenu: () => null,
  LanguageMenuBlock: () => null,
}));
vi.mock("@/components/layout/BuildStamp", () => ({
  BuildStampChip: () => null,
  BuildStampPill: () => null,
  BuildStampRow: () => null,
}));
vi.mock("@/components/brand/Logo", () => ({ Logo: () => null }));

import { Navbar } from "@/components/layout/Navbar";
import { setThemeMode } from "@/lib/useTheme";

const PRODUCT_BLURB = "Every module, in the app's own vocabulary";
const RESOURCES_BLURB = "Guides to the glass workflows";
/* The grace close fires at 140 ms; anything past 260 ms has to be settled. */
const GRACE = 260;

const settle = (ms = GRACE) =>
  act(async () => {
    await new Promise((resolve) => setTimeout(resolve, ms));
  });

const productTrigger = () => screen.getByRole("button", { name: /^product$/i });
const resourcesTrigger = () => screen.getByRole("button", { name: /^resources$/i });

afterEach(cleanup);

describe("desktop top-panel menus", () => {
  it("open instantly on hover — no click at all", async () => {
    const user = userEvent.setup();
    render(<Navbar />);
    expect(screen.queryByText(PRODUCT_BLURB)).toBeNull();

    await user.hover(productTrigger());

    expect(screen.queryByText(PRODUCT_BLURB)).not.toBeNull();
    expect(productTrigger().getAttribute("aria-expanded")).toBe("true");
  });

  it("close a hover-opened menu shortly after the pointer leaves", async () => {
    const user = userEvent.setup();
    render(<Navbar />);

    await user.hover(productTrigger());
    expect(screen.queryByText(PRODUCT_BLURB)).not.toBeNull();

    await user.unhover(productTrigger());
    await settle();

    expect(screen.queryByText(PRODUCT_BLURB)).toBeNull();
  });

  it("a single click pins the menu — the pointer leaving does NOT close it", async () => {
    const user = userEvent.setup();
    render(<Navbar />);

    await user.hover(productTrigger());
    await user.click(productTrigger());
    await user.unhover(productTrigger());
    await settle();

    expect(screen.queryByText(PRODUCT_BLURB)).not.toBeNull();
    expect(productTrigger().getAttribute("aria-expanded")).toBe("true");
  });

  it("the stale-pin trap stays closed: pin, return, click — the click OPENS", async () => {
    /* The exact sequence that read as "menus need a double click": the pin
       once outlived its menu, and the next click landed on the stale pin's
       close branch instead of opening. */
    const user = userEvent.setup();
    render(<Navbar />);

    await user.hover(productTrigger());
    await user.click(productTrigger()); /* pin */
    await user.unhover(productTrigger());
    await settle();
    expect(screen.queryByText(PRODUCT_BLURB)).not.toBeNull();

    /* Close it the deliberate way — a re-click on the pinned trigger. */
    await user.hover(productTrigger());
    await user.click(productTrigger());
    expect(screen.queryByText(PRODUCT_BLURB)).toBeNull();

    /* Now the sequence the visitor actually performs: come back later and
       click once. That click must OPEN. */
    await user.unhover(productTrigger());
    await settle();
    await user.hover(productTrigger());
    await user.click(productTrigger());

    expect(screen.queryByText(PRODUCT_BLURB)).not.toBeNull();
    expect(productTrigger().getAttribute("aria-expanded")).toBe("true");
  });

  it("a re-click on the pinned trigger is the one deliberate toggle-off", async () => {
    const user = userEvent.setup();
    render(<Navbar />);

    await user.hover(productTrigger());
    await user.click(productTrigger());
    expect(screen.queryByText(PRODUCT_BLURB)).not.toBeNull();

    await user.click(productTrigger());
    expect(screen.queryByText(PRODUCT_BLURB)).toBeNull();

    /* And the click after that opens again — one click, every time. */
    await user.click(productTrigger());
    expect(screen.queryByText(PRODUCT_BLURB)).not.toBeNull();
  });

  it("hover-switching to a sibling transfers the menu and reverts to hover mode", async () => {
    const user = userEvent.setup();
    render(<Navbar />);

    await user.hover(productTrigger());
    await user.click(productTrigger()); /* pin Product */
    await user.unhover(productTrigger());
    await settle();

    await user.hover(resourcesTrigger());
    expect(screen.queryByText(RESOURCES_BLURB)).not.toBeNull();
    expect(screen.queryByText(PRODUCT_BLURB)).toBeNull();

    /* Resources was opened by hover, so hover-out closes it — the pin must
       not transfer to the sibling behind the visitor's back. */
    await user.unhover(resourcesTrigger());
    await settle();
    expect(screen.queryByText(RESOURCES_BLURB)).toBeNull();
  });

  it("a pointer-down outside the bar closes a pinned menu", async () => {
    const user = userEvent.setup();
    render(<Navbar />);

    await user.hover(productTrigger());
    await user.click(productTrigger());
    expect(screen.queryByText(PRODUCT_BLURB)).not.toBeNull();

    /* eslint-disable-next-line testing-library/no-node-access -- the outside target */
    fireEvent.pointerDown(document.body);

    expect(screen.queryByText(PRODUCT_BLURB)).toBeNull();
  });

  it("Escape closes a pinned menu", async () => {
    const user = userEvent.setup();
    render(<Navbar />);

    await user.hover(productTrigger());
    await user.click(productTrigger());
    expect(screen.queryByText(PRODUCT_BLURB)).not.toBeNull();

    fireEvent.keyDown(window, { key: "Escape" });

    expect(screen.queryByText(PRODUCT_BLURB)).toBeNull();
  });

  it("the lg–2xl Menu button mounts #mobile-menu and keeps it visible up to wide", async () => {
    const user = userEvent.setup();
    render(<Navbar />);

    const menuBtn = screen.getByRole("button", { name: /^Menu$/ });
    const mobileSheet = document.getElementById("mobile-menu")!;
    expect(mobileSheet.className).toContain("wide:hidden");
    expect(mobileSheet.className).not.toContain("xl:hidden");
    expect(within(mobileSheet).queryByRole("navigation", { name: "Mobile" })).toBeNull();

    await user.click(menuBtn);
    expect(menuBtn.getAttribute("aria-expanded")).toBe("true");
    expect(within(mobileSheet).getByRole("navigation", { name: "Mobile" })).not.toBeNull();

    /* Clicking any link in the sheet closes it immediately. */
    const mobileNav = within(mobileSheet).getByRole("navigation", { name: "Mobile" });
    await user.click(within(mobileNav).getByRole("link", { name: /Pricing/i }));
    expect(menuBtn.getAttribute("aria-expanded")).toBe("false");
  });

  it("syncChromeColour updates all theme-color meta tags in place without detaching React-managed head nodes", () => {
    document.documentElement.style.setProperty("--ink-950", "17 11 41");
    const darkMeta = document.createElement("meta");
    darkMeta.setAttribute("name", "theme-color");
    darkMeta.setAttribute("media", "(prefers-color-scheme: dark)");
    darkMeta.setAttribute("content", "#110B29");
    const lightMeta = document.createElement("meta");
    lightMeta.setAttribute("name", "theme-color");
    lightMeta.setAttribute("media", "(prefers-color-scheme: light)");
    lightMeta.setAttribute("content", "#FDFBF7");
    document.head.appendChild(darkMeta);
    document.head.appendChild(lightMeta);

    try {
      setThemeMode("dark");
      expect(darkMeta.parentNode).toBe(document.head);
      expect(lightMeta.parentNode).toBe(document.head);
      expect(darkMeta.content).toBe("rgb(17 11 41)");
      expect(lightMeta.content).toBe("rgb(17 11 41)");
    } finally {
      document.documentElement.style.removeProperty("--ink-950");
      darkMeta.remove();
      lightMeta.remove();
    }
  });
});

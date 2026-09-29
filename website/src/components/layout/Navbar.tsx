"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { useEffect, useRef, useState } from "react";
import { cn } from "@/lib/cn";
import { site } from "@/lib/site";
import { ArrowRight, ButtonLink } from "@/components/ui/Button";

type NavChild = { label: string; href: string; description?: string };
type NavEntry = { label: string; href?: string; children?: NavChild[] };

const entries: NavEntry[] = [
  {
    label: "Product",
    children: [
      { label: "Features", href: "/features", description: "Every module, in the app's own vocabulary" },
      { label: "How It Works", href: "/how-it-works", description: "Inquiry to statement, step by step" },
      { label: "Screenshots", href: "/screenshots", description: "The interface, screen by screen" },
      { label: "Pricing", href: "/pricing", description: "Editions and what each one unlocks" },
    ],
  },
  { label: "Optimization", href: "/optimization" },
  {
    label: "Resources",
    children: [
      { label: "Resources", href: "/resources", description: "Guides to the glass workflows" },
      { label: "Blog", href: "/blog", description: "Notes on the build and the industry" },
    ],
  },
  {
    label: "Company",
    children: [
      { label: "About ProGlass", href: "/about", description: "What the product is and why it exists" },
      { label: "Developer", href: "/developer", description: "The person building it" },
      { label: "Contact", href: "/contact", description: "Request a demo or ask a question" },
    ],
  },
];

export function Navbar() {
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
        "fixed inset-x-0 top-0 z-50 transition-all duration-300 ease-industrial",
        scrolled
          ? "border-b border-line/60 bg-ink-950/85 backdrop-blur-xl supports-[backdrop-filter]:bg-ink-950/70"
          : "border-b border-transparent bg-transparent",
      )}
    >
      <a
        href="#main"
        className="sr-only focus:not-sr-only focus:absolute focus:left-4 focus:top-3 focus:z-50 focus:rounded-lg focus:bg-brand-400 focus:px-3 focus:py-2 focus:text-sm focus:font-semibold focus:text-ink-950"
      >
        Skip to content
      </a>

      <div className="shell flex h-[var(--nav-height)] items-center justify-between gap-4">
        <Link href="/" className="group flex items-center gap-3" aria-label={`${site.name} — home`}>
          <span
            aria-hidden="true"
            className="grid h-9 w-9 place-items-center rounded-[0.7rem] bg-gradient-to-br from-brand-300 via-brand-400 to-brand-600 text-xs font-bold text-ink-950 shadow-[0_8px_22px_-10px_rgba(56,189,248,0.9)]"
          >
            PG
          </span>
          <span className="flex flex-col leading-none">
            <span className="text-[0.9375rem] font-semibold tracking-tight text-text-primary">ProGlass</span>
            <span className="font-mono text-[0.5625rem] uppercase tracking-[0.24em] text-text-faint">
              Automation
            </span>
          </span>
        </Link>

        {/* ── Desktop navigation ─────────────────────────────────── */}
        <nav aria-label="Primary" className="hidden items-center gap-0.5 lg:flex">
          {entries.map((entry) => {
            const active = isActive(entry);
            if (!entry.children) {
              return (
                <Link
                  key={entry.label}
                  href={entry.href ?? "#"}
                  aria-current={active ? "page" : undefined}
                  className={cn(
                    "relative rounded-lg px-3 py-2 text-sm font-medium transition-colors duration-200",
                    active ? "text-text-primary" : "text-text-muted hover:text-text-primary",
                  )}
                >
                  {entry.label}
                  {active ? (
                    <span aria-hidden="true" className="absolute inset-x-3 -bottom-0.5 h-px bg-brand-400/80" />
                  ) : null}
                </Link>
              );
            }

            const open = openMenu === entry.label;
            return (
              <div
                key={entry.label}
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
                  className={cn(
                    "flex items-center gap-1.5 rounded-lg px-3 py-2 text-sm font-medium transition-colors duration-200",
                    active || open ? "text-text-primary" : "text-text-muted hover:text-text-primary",
                  )}
                >
                  {entry.label}
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
                  <div className="absolute left-1/2 top-full w-72 -translate-x-1/2 pt-3">
                    <div className="animate-scale-in overflow-hidden rounded-panel border border-line-strong/70 bg-ink-900/95 p-1.5 shadow-raised backdrop-blur-xl">
                      {entry.children.map((child) => (
                        <Link
                          key={child.href}
                          href={child.href}
                          className="group flex flex-col gap-0.5 rounded-card px-3 py-2.5 transition-colors duration-200 hover:bg-ink-800/80"
                        >
                          <span className="flex items-center gap-1.5 text-sm font-medium text-text-primary">
                            {child.label}
                            <ArrowRight className="opacity-0 transition-opacity duration-200 group-hover:opacity-100" />
                          </span>
                          {child.description ? (
                            <span className="text-xs leading-relaxed text-text-faint">{child.description}</span>
                          ) : null}
                        </Link>
                      ))}
                    </div>
                  </div>
                ) : null}
              </div>
            );
          })}
        </nav>

        <div className="hidden items-center gap-2 lg:flex">
          <ButtonLink href="/screenshots" variant="ghost" size="sm">
            See the app
          </ButtonLink>
          <ButtonLink href="/contact" size="sm" trailing={<ArrowRight />}>
            Request a Demo
          </ButtonLink>
        </div>

        {/* ── Mobile trigger ─────────────────────────────────────── */}
        <button
          type="button"
          onClick={() => setMobileOpen((v) => !v)}
          aria-expanded={mobileOpen}
          aria-controls="mobile-menu"
          className="flex h-10 w-10 items-center justify-center rounded-xl border border-line-strong/70 bg-ink-900/70 text-text-primary transition-colors duration-200 hover:border-brand-400/50 lg:hidden"
        >
          <span className="sr-only">{mobileOpen ? "Close menu" : "Open menu"}</span>
          <svg viewBox="0 0 20 20" className="h-4.5 w-4.5" fill="none" stroke="currentColor" strokeWidth="1.7" strokeLinecap="round">
            {mobileOpen ? <path d="M5 5l10 10M15 5 5 15" /> : <path d="M3 6h14M3 10h14M3 14h14" />}
          </svg>
        </button>
      </div>

      {/* ── Mobile sheet ─────────────────────────────────────────── */}
      <div
        id="mobile-menu"
        hidden={!mobileOpen}
        className={cn(
          "lg:hidden",
          mobileOpen && "animate-fade-in border-t border-line/60 bg-ink-950/98 backdrop-blur-xl",
        )}
      >
        <div className="shell max-h-[calc(100vh-var(--nav-height))] overflow-y-auto py-5">
          <nav aria-label="Mobile" className="space-y-6">
            {entries.map((entry) => (
              <div key={entry.label}>
                {entry.href ? (
                  <Link
                    href={entry.href}
                    className="flex items-center justify-between border-b border-line/50 pb-2 text-base font-semibold text-text-primary"
                  >
                    {entry.label}
                    <ArrowRight />
                  </Link>
                ) : (
                  <>
                    <p className="eyebrow mb-2">{entry.label}</p>
                    <ul className="space-y-0.5">
                      {entry.children?.map((child) => (
                        <li key={child.href}>
                          <Link
                            href={child.href}
                            className="flex flex-col gap-0.5 rounded-lg px-2 py-2.5 transition-colors duration-200 hover:bg-ink-900/80"
                          >
                            <span className="text-sm font-medium text-text-primary">{child.label}</span>
                            {child.description ? (
                              <span className="text-xs leading-snug text-text-faint">{child.description}</span>
                            ) : null}
                          </Link>
                        </li>
                      ))}
                    </ul>
                  </>
                )}
              </div>
            ))}
          </nav>

          <div className="mt-7 flex flex-col gap-2.5">
            <ButtonLink href="/contact" size="lg" trailing={<ArrowRight />}>
              Request a Demo
            </ButtonLink>
            <ButtonLink href="/optimization" variant="secondary" size="lg">
              See the optimization engine
            </ButtonLink>
          </div>

          <p className="mt-6 font-mono text-2xs uppercase tracking-[0.18em] text-text-faint">
            Desktop software · v{site.version} · No account required
          </p>
        </div>
      </div>
    </header>
  );
}

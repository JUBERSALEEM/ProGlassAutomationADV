"use client";

import { useEffect, useRef, useState } from "react";
import { site } from "@/lib/site";
import { useT, useHref } from "@/i18n/provider";
import { Icon } from "@/components/ui/Icon";

/**
 * The support bubble — a chat stoep that hands a real person to you instead
 * of a wizard you have not asked for.
 *
 * How this site does "chat support", because reality beats a fake live-chat:
 * no webhook, no agent roster, no 9-to-5 staffing lie. The visitor presses a
 * button and a real channel opens — WhatsApp first (you answer inside the
 * WhatsApp you already carry, the author answers personally, free both ways),
 * email next, and the demo form last. The panel says so in its own words,
 * because a bubble that pretends a live agent is waiting is a debt you pay
 * forever.
 *
 * Trigger sits at the fixed-left mirror of the theme dial. Panel opens
 * upward; Escape and outside clicks close it; focus moves in on open and
 * back to the trigger on close. WhatsApp hides itself quietly when the
 * number is not configured, and every remaining option still works.
 */
const OPEN_EVENT = "proglass:open-support";

export function SupportBubble() {
  const t = useT();
  const href = useHref();
  const [open, setOpen] = useState(false);
  const rootRef = useRef<HTMLDivElement>(null);
  const triggerRef = useRef<HTMLButtonElement>(null);

  useEffect(() => {
    const show = () => setOpen(true);
    window.addEventListener(OPEN_EVENT, show);
    return () => window.removeEventListener(OPEN_EVENT, show);
  }, []);

  useEffect(() => {
    if (!open) return;
    const onKey = (event: KeyboardEvent) => {
      if (event.key === "Escape") setOpen(false);
    };
    const onPointer = (event: MouseEvent | TouchEvent) => {
      if (!rootRef.current?.contains(event.target as Node)) setOpen(false);
    };
    document.addEventListener("keydown", onKey);
    document.addEventListener("mousedown", onPointer);
    document.addEventListener("touchstart", onPointer);
    rootRef.current?.querySelector<HTMLElement>("a, button")?.focus();
    return () => {
      document.removeEventListener("keydown", onKey);
      document.removeEventListener("mousedown", onPointer);
      document.removeEventListener("touchstart", onPointer);
    };
  }, [open]);

  useEffect(() => {
    if (!open) triggerRef.current?.focus();
  }, [open]);

  const whatsappHref = site.whatsappNumber
    ? `https://wa.me/${site.whatsappNumber}?text=${encodeURIComponent(
        "Hello — I am on the ProGlass Automation site and have a question.",
      )}`
    : "";

  const mailtoHref = `mailto:${site.developerEmail}?subject=${encodeURIComponent("Support — ProGlass Automation")}`;

  return (
    <div ref={rootRef} className="no-print fixed bottom-[max(1.25rem,env(safe-area-inset-bottom))] left-[max(1.25rem,env(safe-area-inset-left))] z-40">
      {open ? (
        <div
          role="dialog"
          aria-label={t("Support")}
          className="menu-card animate-scale-in w-[min(21rem,calc(100vw-2.5rem))] rounded-panel border-2 border-line-strong/60 bg-ink-900/95 p-4 shadow-raised"
        >
          <div className="flex items-start justify-between gap-3">
            <div>
              <p className="text-sm font-bold text-text-primary">{t("Talk to the person who builds it")}</p>
              <p className="mt-1 text-xs leading-relaxed text-text-muted">
                {t("No bots and no ticket queues. Pick a channel and a real reply lands there — usually the same day, Gulf Standard Time.")}
              </p>
            </div>
            <button
              type="button"
              onClick={() => setOpen(false)}
              aria-label={t("Close support panel")}
              className="grid h-7 w-7 shrink-0 place-items-center rounded-card border border-line-strong/60 text-text-muted transition-colors hover:border-brand-400/50 hover:text-text-primary"
            >
              <svg viewBox="0 0 14 14" className="h-3 w-3" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round">
                <path d="M3 3l8 8M11 3l-8 8" />
              </svg>
            </button>
          </div>

          <div className="mt-4 space-y-2">
            {site.whatsappNumber ? (
              <a
                href={whatsappHref}
                target="_blank"
                rel="noreferrer noopener"
                onClick={() => setOpen(false)}
                className="group flex items-center gap-3 rounded-card border-2 border-line-strong/60 bg-ink-950/60 px-3.5 py-3 transition-colors hover:border-emerald-400/60"
              >
                <span className="grid h-9 w-9 shrink-0 place-items-center rounded-card bg-emerald-500/15 text-emerald-300">
                  <Icon path="M8 2.5a5.5 5.5 0 1 0 2.9 10.1L14 13.5l-1.1-3.2A5.5 5.5 0 0 0 8 2.5zM6.4 5.2c.9 0 1.9 2.1 2.3 1.7.4-.3.7-.9 1.2-.9.6 0 1.6 1 1.4 1.5-.2.4-.8.8-.7 1.3.1.8 1 2.6-.4 3.4-.5.3-1 .1-1.5-.4L6.5 9.6c-.4-.5-.6-1-.4-1.4.2-.6.9-1.4.3-1.9-.4-.4-.6-1-0-1.1z" className="h-5 w-5" />
                </span>
                <span className="min-w-0">
                  <span className="block text-sm font-bold text-text-primary">{t("WhatsApp")}</span>
                  <span className="block text-xs text-text-muted">{t("Instant conversation — press send, add the question, done.")}</span>
                </span>
                <svg viewBox="0 0 16 16" className="ms-auto h-3.5 w-3.5 shrink-0 text-text-faint transition-transform group-hover:translate-x-0.5 group-hover:text-emerald-300" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round">
                  <path d="M2.5 8h11M9.5 4l4 4-4 4" />
                </svg>
              </a>
            ) : (
              <div className="flex items-center gap-3 rounded-card border border-dashed border-line-strong/60 px-3.5 py-3">
                <span className="grid h-9 w-9 shrink-0 place-items-center rounded-card bg-ink-800/70 text-text-faint">
                  <Icon path="M8 2.5a5.5 5.5 0 1 0 2.9 10.1L14 13.5l-1.1-3.2A5.5 5.5 0 0 0 8 2.5z" className="h-5 w-5" />
                </span>
                <span className="text-xs leading-relaxed text-text-faint">
                  {t("WhatsApp arrives as soon as the number is set in the site config.")}
                </span>
              </div>
            )}

            <a
              href={mailtoHref}
              onClick={() => setOpen(false)}
              className="group flex items-center gap-3 rounded-card border-2 border-line-strong/60 bg-ink-950/60 px-3.5 py-3 transition-colors hover:border-brand-400/60"
            >
              <span className="grid h-9 w-9 shrink-0 place-items-center rounded-card bg-brand-500/15 text-brand-300">
                <Icon path="M2.5 4.5h11v7h-11zM3 5l5 3.5L13 5" className="h-5 w-5" />
              </span>
              <span className="min-w-0">
                <span className="block text-sm font-bold text-text-primary">{t("Email")}</span>
                <span className="block truncate text-xs text-text-muted">{site.developerEmail}</span>
              </span>
            </a>

            <a
              href={href("/contact")}
              onClick={() => setOpen(false)}
              className="group flex items-center gap-3 rounded-card border-2 border-line-strong/60 bg-ink-950/60 px-3.5 py-3 transition-colors hover:border-brand-400/60"
            >
              <span className="grid h-9 w-9 shrink-0 place-items-center rounded-card bg-brand-500/15 text-brand-300">
                <Icon path="M4 2.5h8a1 1 0 0 1 1 1v9a1 1 0 0 1-1 1H4a1 1 0 0 1-1-1v-9a1 1 0 0 1 1-1zM6 6h4M6 8.5h4M6 11h2.5" className="h-5 w-5" />
              </span>
              <span className="min-w-0">
                <span className="block text-sm font-semibold text-text-primary">{t("Demo request")}</span>
                <span className="block text-xs text-text-muted">{t("Written quote on your sizes and rates — straight to the Gmail inbox.")}</span>
              </span>
            </a>
          </div>

          <p className="mt-3 border-t border-line/60 pt-2.5 text-2xs leading-relaxed text-text-faint">
            {t("Nothing here is stored, tracked, or seen by anyone else until you choose to send.")}
          </p>
        </div>
      ) : null}

      <button
        ref={triggerRef}
        type="button"
        onClick={() => setOpen((value) => !value)}
        aria-expanded={open}
        aria-label={open ? t("Close chat support") : t("Chat with support")}
        className="menu-card support-launcher group flex items-center gap-2.5 rounded-full border-2 border-line-strong/70 bg-ink-900/90 px-4 py-3 text-xs font-bold text-text-primary shadow-raised transition-transform hover:-translate-y-0.5"
      >
        <span className="relative grid h-6 w-6 place-items-center rounded-full bg-emerald-500/20 text-emerald-300">
          <svg viewBox="0 0 20 20" className="h-4 w-4" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round">
            <path d="M17 12.5a1.5 1.5 0 0 1-1.5 1.5H8l-4.5 3.5v-3.5A1.5 1.5 0 0 1 2 12.5v-7A1.5 1.5 0 0 1 3.5 4h12A1.5 1.5 0 0 1 17 5.5z" />
            <path d="M6 8.5h8M6 11h5" />
          </svg>
          <span aria-hidden="true" className="absolute -top-0.5 -right-0.5 h-2.5 w-2.5 rounded-full border-2 border-ink-950 bg-emerald-400" />
        </span>
        {t("Chat")}
      </button>
    </div>
  );
}

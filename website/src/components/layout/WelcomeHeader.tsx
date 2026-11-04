"use client";

import { useEffect, useRef, useState } from "react";
import { LocaleLink } from "@/components/ui/LocaleLink";
import { Logo } from "@/components/brand/Logo";
import { LanguageMenu } from "@/components/layout/LanguageMenu";
import { ThemeModeSwitch } from "@/components/layout/ThemeModeSwitch";
import { ThemeToggle } from "@/components/layout/ThemeToggle";
import { globalChatDisplayName } from "@/lib/global-chat";
import { supabaseBrowser } from "@/lib/supabase";
import { useWebsiteSession } from "@/lib/website-session";

/** Compact SaaS chrome: account access, inquiry actions, chat and appearance. */
export function WelcomeHeader() {
  const [inquiryMenuOpen, setInquiryMenuOpen] = useState(false);
  const inquiryMenuRef = useRef<HTMLDivElement>(null);
  const { checking, user, userEmail } = useWebsiteSession();
  const [savedProfile, setSavedProfile] = useState<{ userId: string; displayName: string } | null>(null);
  const metadata = user?.user_metadata ?? {};
  const metadataName = [metadata.full_name, metadata.name].find((value) => typeof value === "string" && value.trim());
  const profileName = savedProfile && savedProfile.userId === user?.id ? savedProfile.displayName : "";
  const accountName = user
    ? profileName || (typeof metadataName === "string" ? metadataName.trim() : "") || globalChatDisplayName({ email: user.email, metadata })
    : null;

  useEffect(() => {
    if (!user?.id) return;
    let active = true;
    void (async () => {
      try {
        const { data, error } = await supabaseBrowser()
          .from("user_profiles")
          .select("display_name")
          .eq("user_id", user.id)
          .maybeSingle();
        if (active) setSavedProfile({ userId: user.id, displayName: !error && typeof data?.display_name === "string" ? data.display_name.trim() : "" });
      } catch {
        if (active) setSavedProfile({ userId: user.id, displayName: "" });
      }
    })();
    return () => { active = false; };
  }, [user?.id, metadata.full_name, metadata.name]);

  useEffect(() => {
    if (!inquiryMenuOpen) return;
    const closeOutside = (event: PointerEvent) => {
      if (!inquiryMenuRef.current?.contains(event.target as Node)) setInquiryMenuOpen(false);
    };
    const closeOnEscape = (event: KeyboardEvent) => {
      if (event.key === "Escape") setInquiryMenuOpen(false);
    };
    document.addEventListener("pointerdown", closeOutside);
    document.addEventListener("keydown", closeOnEscape);
    return () => {
      document.removeEventListener("pointerdown", closeOutside);
      document.removeEventListener("keydown", closeOnEscape);
    };
  }, [inquiryMenuOpen]);

  const closeMenu = () => setInquiryMenuOpen(false);

  return (
    <header className="sticky top-0 z-40 border-b border-line/80 bg-ink-950/95 shadow-panel backdrop-blur-xl">
      <div className="mx-auto flex max-w-[1440px] flex-wrap items-center justify-between gap-x-4 gap-y-2 px-4 py-2.5 sm:px-6 lg:min-h-[72px] lg:flex-nowrap lg:px-8">
        <LocaleLink href="/" aria-label="ProGlass Automation home" className="shrink-0">
          <Logo size={34} subtitle="Automation" />
        </LocaleLink>

        <div className="flex min-w-0 flex-1 flex-wrap items-center justify-end gap-2 lg:flex-nowrap lg:gap-2.5">
          <div className="order-1 flex items-center gap-1.5 sm:gap-2">
            <LanguageMenu compact />
            <ThemeModeSwitch className="h-9 scale-[.92] origin-right sm:scale-100" />
            <ThemeToggle />
          </div>

          <nav aria-label="Account and customer actions" className="order-2 flex w-full flex-wrap items-center justify-end gap-1.5 sm:w-auto sm:flex-nowrap sm:gap-2 lg:ms-1">
            <LocaleLink
              href="/"
              aria-label="Home"
              title="Home"
              className="inline-flex h-10 items-center justify-center gap-1.5 rounded-full border border-line/80 px-3 text-sm font-semibold text-text-secondary transition-[background-color,border-color,color] duration-150 hover:border-brand-400/50 hover:bg-ink-900 hover:text-text-primary focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-brand-300"
            >
              <svg aria-hidden="true" viewBox="0 0 16 16" className="h-4 w-4" fill="none" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round">
                <path d="m2 7 6-5 6 5v6.25A.75.75 0 0 1 13.25 14h-3.5V9.5h-3.5V14h-2.5a.75.75 0 0 1-.75-.75V7Z" />
              </svg>
              <span className="hidden sm:inline">Home</span>
            </LocaleLink>
            <LocaleLink
              href="/chat"
              aria-label="Open community chat"
              title="Community chat"
              className="inline-flex h-10 items-center justify-center gap-2 rounded-full border border-line/80 px-3 text-sm font-semibold text-text-secondary transition-[background-color,border-color,color] duration-150 hover:border-brand-400/50 hover:bg-ink-900 hover:text-text-primary focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-brand-300"
            >
              <svg aria-hidden="true" viewBox="0 0 16 16" className="h-4 w-4" fill="none" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round">
                <path d="M3 3.25h10a1.75 1.75 0 0 1 1.75 1.75v5A1.75 1.75 0 0 1 13 11.75H7l-3.75 2v-2.1A1.75 1.75 0 0 1 1.25 10V5A1.75 1.75 0 0 1 3 3.25Z" />
                <path d="M4.5 6.2h7M4.5 8.6h4.5" />
              </svg>
              <span className="hidden xl:inline">Chat</span>
            </LocaleLink>
            <LocaleLink
              href={accountName ? "/account" : "/account?redirect=%2Finquiries%2Fchat"}
              aria-label={accountName ? `Customer account for ${accountName}` : "Sign in or create a customer account"}
              title={userEmail || undefined}
              className="inline-flex h-10 max-w-[11rem] items-center justify-center truncate rounded-full border border-line-strong/80 px-3.5 text-sm font-semibold text-text-primary transition-[background-color,border-color,color] duration-150 hover:border-brand-400/60 hover:bg-ink-900 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-brand-300 sm:max-w-[15rem]"
            >
              <span className="truncate">{accountName || (checking ? "Account" : "Sign in / sign up")}</span>
            </LocaleLink>
            <div className="relative shrink-0" ref={inquiryMenuRef}>
              <button
                type="button"
                aria-expanded={inquiryMenuOpen}
                aria-haspopup="menu"
                onClick={() => setInquiryMenuOpen((open) => !open)}
                className="inline-flex h-10 items-center justify-center gap-2 rounded-full bg-brand-600 px-3.5 text-sm font-bold text-accent-ink shadow-accent transition-[background-color,box-shadow,transform] duration-150 hover:-translate-y-px hover:bg-brand-500 hover:shadow-accent-strong active:translate-y-0 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-brand-300 sm:px-4"
              >
                <span>Create inquiry</span>
                <svg aria-hidden="true" viewBox="0 0 12 12" className={`h-3 w-3 transition-transform ${inquiryMenuOpen ? "rotate-180" : ""}`} fill="none" stroke="currentColor" strokeWidth="1.7" strokeLinecap="round" strokeLinejoin="round">
                  <path d="m2.5 4.5 3.5 3 3.5-3" />
                </svg>
              </button>
              {inquiryMenuOpen ? (
                <div role="menu" aria-label="Choose an inquiry type" className="absolute end-0 top-full z-50 mt-3 w-[min(21rem,calc(100vw-2rem))] overflow-hidden rounded-2xl border border-line-strong/80 bg-ink-900/95 p-2 shadow-raised backdrop-blur-xl">
                  <LocaleLink
                    role="menuitem"
                    href="/inquiries/submit"
                    onClick={closeMenu}
                    className="block rounded-xl px-4 py-3 transition-colors duration-150 hover:bg-ink-850 focus-visible:bg-ink-850"
                  >
                    <span className="block text-sm font-bold text-text-primary">General inquiry</span>
                    <span className="mt-1 block text-xs leading-5 text-text-muted">Tell us about your glass project and send drawings or a bill of quantities.</span>
                  </LocaleLink>
                  <LocaleLink
                    role="menuitem"
                    href="/inquiries/submit/proforma"
                    onClick={closeMenu}
                    className="mt-1 block rounded-xl border border-brand-300/25 bg-brand-400/[.08] px-4 py-3 transition-[background-color,border-color] duration-150 hover:border-brand-300/45 hover:bg-brand-400/[.14] focus-visible:bg-brand-400/[.14]"
                  >
                    <span className="block text-sm font-bold text-brand-100">Request a PI</span>
                    <span className="mt-1 block text-xs leading-5 text-text-muted">Send project details to the ProGlass inquiry inbox for the team to prepare a proforma invoice.</span>
                  </LocaleLink>
                </div>
              ) : null}
            </div>
          </nav>
        </div>
      </div>
    </header>
  );
}

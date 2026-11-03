import { LocaleLink } from "@/components/ui/LocaleLink";
import { Logo } from "@/components/brand/Logo";

/** A deliberately small site header for the welcome-first website. */
export function WelcomeHeader() {
  return (
    <header className="sticky top-0 z-40 border-b border-white/10 bg-ink-950/85 backdrop-blur-xl">
      <div className="mx-auto flex min-h-[76px] max-w-7xl items-center justify-between gap-4 px-5 sm:px-8">
        <LocaleLink href="/" aria-label="ProGlass Automation home" className="shrink-0">
          <Logo size={34} subtitle="Automation" />
        </LocaleLink>
        <nav aria-label="Account and customer actions" className="flex items-center gap-2 sm:gap-3">
          <LocaleLink
            href="/reports/login?redirect=%2Finquiries%2Fchat"
            className="hidden rounded-full px-3 py-2 text-sm font-semibold text-text-muted transition hover:bg-white/5 hover:text-white sm:inline-flex"
          >
            Log in
          </LocaleLink>
          <LocaleLink
            href="/reports/login?mode=signup&redirect=%2Finquiries%2Fsubmit"
            className="hidden rounded-full border border-white/15 px-4 py-2 text-sm font-semibold text-text-primary transition hover:border-brand-300/60 hover:bg-white/5 sm:inline-flex"
          >
            Sign up
          </LocaleLink>
          <LocaleLink
            href="/inquiries/submit"
            className="inline-flex min-h-10 items-center justify-center rounded-full bg-brand-400 px-4 py-2 text-sm font-bold text-ink-950 shadow-lg shadow-brand-950/30 transition hover:bg-brand-300 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-brand-200"
          >
            Create inquiry
          </LocaleLink>
        </nav>
      </div>
    </header>
  );
}

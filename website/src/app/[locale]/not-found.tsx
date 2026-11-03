import { LocaleLink } from "@/components/ui/LocaleLink";

export default function NotFound() {
  return (
    <section className="mx-auto flex min-h-[65svh] max-w-3xl flex-col items-start justify-center px-5 py-20 sm:px-8">
      <p className="font-mono text-xs font-semibold uppercase tracking-[.2em] text-brand-200">404 · ProGlass Automation</p>
      <h1 className="mt-5 text-4xl font-semibold tracking-tight text-white sm:text-5xl">This page isn’t here.</h1>
      <p className="mt-4 max-w-xl text-base leading-7 text-text-muted">
        We’ve simplified the website. Head back to the welcome page or start a customer inquiry.
      </p>
      <div className="mt-8 flex flex-wrap gap-3">
        <LocaleLink href="/" className="rounded-xl bg-brand-400 px-5 py-3 text-sm font-bold text-ink-950 hover:bg-brand-300">Go to welcome page</LocaleLink>
        <LocaleLink href="/inquiries/submit" className="rounded-xl border border-white/15 px-5 py-3 text-sm font-semibold text-white hover:bg-white/5">Create an inquiry</LocaleLink>
      </div>
    </section>
  );
}

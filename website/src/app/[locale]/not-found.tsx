"use client";

import { LocaleLink as Link } from "@/components/ui/LocaleLink";
import { Container, Section } from "@/components/ui/Container";
import { NotFoundSuggestions } from "@/components/layout/NotFoundSuggestions";
import { ArrowRight, ButtonLink } from "@/components/ui/Button";
import { nav } from "@/lib/site";
import { useT } from "@/i18n/provider";

/**
 * The 404 page.
 *
 * It is a client component for one reason: the language comes from the
 * provider rather than from route params. Next does not hand a not-found
 * boundary the params of the segment it replaced, and a 404 that snaps back to
 * English is the last thing a reader who has been on a German page should see.
 * If it is ever rendered with no provider above it — an address that never
 * reached a language segment — `useT` falls back to English rather than
 * throwing.
 */
export default function NotFound() {
  const t = useT();
  return (
    <Section className="min-h-[70vh]">
      <Container>
        <div className="max-w-3xl">
          <p className="font-mono text-2xs uppercase tracking-[0.22em] text-brand-300">{t("Error 404")}</p>
          <h1 className="display-1 mt-5">{t("That page is not here.")}</h1>
          <p className="lede mt-5">
            {t("The link may be out of date, or the page may never have existed. Everything on this site is reachable from the navigation — here are the pages people usually want.")}
          </p>

          <div className="mt-8 flex flex-wrap gap-3">
            <ButtonLink href="/" size="lg" trailing={<ArrowRight />}>
          {t("Back to the homepage")}
        </ButtonLink>
            <ButtonLink href="/contact" variant="secondary" size="lg">
          {t("Request a Demo")}
        </ButtonLink>
          </div>

          <NotFoundSuggestions />

          <nav aria-label={t("All pages")} className="mt-10 border-t border-line/60 pt-6">
            <p className="eyebrow">{t("Everything else")}</p>
            <ul className="mt-3 flex flex-wrap gap-x-5 gap-y-2">
              {nav.primary.map((item) => (
                <li key={item.href}>
                  <Link href={item.href} className="link-underline text-sm text-text-secondary">
                    {t(item.label)}
                  </Link>
                </li>
              ))}
              {nav.footer.resources.map((item) => (
                <li key={item.href}>
                  <Link href={item.href} className="link-underline text-sm text-text-secondary">
                    {t(item.label)}
                  </Link>
                </li>
              ))}
              <li>
                <Link href="/contact" className="link-underline text-sm text-text-secondary">
          {t("Contact")}
        </Link>
              </li>
            </ul>
          </nav>
        </div>
      </Container>
    </Section>
  );
}

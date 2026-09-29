import Link from "next/link";

import { Container, Section } from "@/components/ui/Container";
import { NotFoundSuggestions } from "@/components/layout/NotFoundSuggestions";
import { ArrowRight, ButtonLink } from "@/components/ui/Button";
import { nav } from "@/lib/site";

export default function NotFound() {
  return (
    <Section className="min-h-[70vh]">
      <Container>
        <div className="max-w-3xl">
          <p className="font-mono text-2xs uppercase tracking-[0.22em] text-brand-300">Error 404</p>
          <h1 className="mt-5 text-3xl leading-tight sm:text-4xl">That page is not here.</h1>
          <p className="lede mt-5">
            The link may be out of date, or the page may never have existed. Everything on this site is reachable from the
            navigation — here are the pages people usually want.
          </p>

          <div className="mt-8 flex flex-wrap gap-3">
            <ButtonLink href="/" size="lg" trailing={<ArrowRight />}>
              Back to the homepage
            </ButtonLink>
            <ButtonLink href="/contact" variant="secondary" size="lg">
              Request a Demo
            </ButtonLink>
          </div>

          <NotFoundSuggestions />

          <nav aria-label="All pages" className="mt-10 border-t border-line/60 pt-6">
            <p className="eyebrow">Everything else</p>
            <ul className="mt-3 flex flex-wrap gap-x-5 gap-y-2">
              {nav.primary.map((item) => (
                <li key={item.href}>
                  <Link href={item.href} className="link-underline text-sm text-text-secondary">
                    {item.label}
                  </Link>
                </li>
              ))}
              {nav.footer.resources.map((item) => (
                <li key={item.href}>
                  <Link href={item.href} className="link-underline text-sm text-text-secondary">
                    {item.label}
                  </Link>
                </li>
              ))}
              <li>
                <Link href="/contact" className="link-underline text-sm text-text-secondary">
                  Contact
                </Link>
              </li>
            </ul>
          </nav>
        </div>
      </Container>
    </Section>
  );
}

import { Container, Section } from "@/components/ui/Container";
import { ArrowRight, ButtonLink } from "@/components/ui/Button";
import { nav } from "@/lib/site";

export default function NotFound() {
  return (
    <Section className="min-h-[70vh]">
      <Container>
        <div className="max-w-2xl">
          <p className="font-mono text-2xs uppercase tracking-[0.22em] text-brand-300/80">Error 404</p>
          <h1 className="mt-5 text-3xl leading-tight sm:text-4xl">That page is not here.</h1>
          <p className="lede mt-5">
            The link may be out of date, or the page may never have existed. Everything on this site is reachable from the
            navigation — here are the pages people usually want.
          </p>

          <div className="mt-8 flex flex-wrap justify-center gap-3">
            <ButtonLink href="/" size="lg" trailing={<ArrowRight />}>
              Back to the homepage
            </ButtonLink>
            <ButtonLink href="/contact" variant="secondary" size="lg">
              Request a Demo
            </ButtonLink>
          </div>

          <ul className="mt-12 grid gap-3 text-left sm:grid-cols-2">
            {nav.primary.map((item) => (
              <li key={item.href}>
                <a
                  href={item.href}
                  className="surface flex items-center justify-between gap-4 px-4 py-3 text-sm text-text-secondary transition-colors duration-200 hover:border-brand-400/40 hover:text-text-primary"
                >
                  {item.label}
                  <ArrowRight />
                </a>
              </li>
            ))}
          </ul>
        </div>
      </Container>
    </Section>
  );
}

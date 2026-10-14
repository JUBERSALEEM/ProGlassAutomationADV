import type { Metadata } from "next";
import { Container, Section } from "@/components/ui/Container";
import { translatorFor } from "@/i18n/server";
import { LiquidGlassPrototype } from "@/components/glass/LiquidGlassPrototype";
import "../../../liquid-prototype.css";

/*
 * The Liquid Glass prototype — Phase 1 of docs/LIQUID_GLASS_PROTOTYPE.md.
 *
 * One interactive stage on an internal, noindex review route: the
 * floating nav, reactive buttons, morphing panel, distortion lens and
 * material lab from the two references, built to be validated BEFORE
 * any of its grammar is promoted into a production engine (Phase 5,
 * gated on user approval). Like the clay/crystal catalogs this is an
 * internal English review surface; it is excluded from the sitemap and
 * marked noindex. Every prototype selector is scoped `.lgx` and the
 * sheet is imported only here, so production pages are untouched.
 */

type PageProps = { params: Promise<{ locale: string }> };

export async function generateMetadata({ params }: PageProps): Promise<Metadata> {
  await translatorFor(params);
  return {
    title: "Liquid Glass prototype",
    description: "Internal review surface: the interactive Liquid Glass prototype — nav, buttons, morphing panel, distortion lens, material lab.",
    robots: { index: false, follow: false },
  };
}

export default async function LiquidGlassPrototypePage({ params }: PageProps) {
  /* Validates the locale like every other route; the page itself is an
     internal English review surface (noindex), so it takes no copy key. */
  await translatorFor(params);
  return (
    <Section aria-labelledby="liquid-glass-prototype" className="pt-10">
      <Container>
        <p className="mono-label">Internal · review surface · Phase 1 prototype</p>
        <h1 id="liquid-glass-prototype" className="mt-2 text-3xl font-bold tracking-tight text-text-primary sm:text-4xl">
          Liquid Glass prototype
        </h1>
        <p className="mt-3 max-w-2xl text-sm leading-relaxed text-text-muted">
          The interactive prototype for the Liquid Glass redesign track — built against the two
          references and the checklist in <span className="mono-label">docs/LIQUID_GLASS_PROTOTYPE.md</span>.
          Nothing here has been applied to production pages; validate this stage first.
        </p>
        <div className="mt-8">
          <LiquidGlassPrototype />
        </div>
      </Container>
    </Section>
  );
}

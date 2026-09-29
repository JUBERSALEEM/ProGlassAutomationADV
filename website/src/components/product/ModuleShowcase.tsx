import Image from "next/image";
import Link from "next/link";
import type { ProductModule } from "@/data/modules";
import { cn } from "@/lib/cn";
import { Reveal } from "@/components/ui/Reveal";
import { ArrowRight, ButtonLink } from "@/components/ui/Button";
import { IndexChip } from "@/components/ui/Badge";

/**
 * Alternating module presentation.
 *
 * Image left / text right, then text left / image right — the rhythm keeps a
 * long feature page from reading as one column of identical cards.
 */
export function ModuleShowcase({
  modules,
  reversed = false,
}: {
  modules: ProductModule[];
  reversed?: boolean;
}) {
  return (
    <div className="space-y-16 sm:space-y-20 lg:space-y-24">
      {modules.map((module, index) => {
        const flip = reversed ? index % 2 === 0 : index % 2 === 1;
        return (
          <Reveal key={module.id} as="article" className="scroll-mt-28" >
            <div id={module.id} className="grid items-center gap-8 lg:grid-cols-2 lg:gap-14">
              <div className={cn(flip && "lg:order-2")}>
                <div className="flex items-center gap-3">
                  <IndexChip>{module.number}</IndexChip>
                  <div>
                    <p className="eyebrow">{module.railGroup}</p>
                    <h3 className="text-xl font-semibold text-text-primary sm:text-2xl">{module.name}</h3>
                  </div>
                </div>

                <p className="mt-5 text-base leading-relaxed text-text-secondary">{module.summary}</p>
                <p className="mt-3 text-sm leading-relaxed text-text-muted">{module.detail}</p>

                <ul className="mt-6 space-y-2.5">
                  {module.capabilities.map((item) => (
                    <li key={item} className="flex gap-2.5 text-sm leading-relaxed text-text-muted">
                      <span aria-hidden="true" className="mt-2 h-1 w-1 shrink-0 rounded-full bg-brand-400/80" />
                      {item}
                    </li>
                  ))}
                </ul>

                <div className="mt-6 flex flex-wrap items-center gap-4">
                  <ButtonLink href={module.href} variant="secondary" size="sm" trailing={<ArrowRight />}>
                    View module
                  </ButtonLink>
                  <span className="mono-label">{module.evidence}</span>
                </div>
              </div>

              <div className={cn(flip && "lg:order-1")}>
                {module.illustration ? (
                  <figure className="plate overflow-hidden">
                    <div className="relative aspect-[3/2] w-full">
                      <Image
                        src={module.illustration.src}
                        alt={module.illustration.alt}
                        fill
                        sizes="(max-width: 1024px) 100vw, 560px"
                        className="object-cover"
                      />
                    </div>
                    <figcaption className="border-t border-line/60 bg-ink-950/60 px-4 py-3 text-xs leading-relaxed text-text-faint">
                      {module.illustration.alt} — shipped inside the application, declared in ProGlassAutomation.csproj.
                    </figcaption>
                  </figure>
                ) : (
                  <ModulePanel module={module} />
                )}
              </div>
            </div>
          </Reveal>
        );
      })}
    </div>
  );
}

/**
 * Compact panel used when a module has no shipped illustration. It shows the
 * module's real surface — rail label, capability list, evidence path — rather
 * than a decorative graphic standing in for a screenshot.
 */
function ModulePanel({ module }: { module: ProductModule }) {
  return (
    <div className="plate p-5 sm:p-6">
      <div className="flex items-center justify-between gap-3">
        <span className="font-mono text-2xs uppercase tracking-[0.16em] text-text-faint">
          Navigation rail
        </span>
        <span className="font-mono text-2xs text-brand-300">{module.moduleKey}</span>
      </div>

      <div className="mt-4 flex items-center gap-2.5 rounded-xl border border-line-strong/70 bg-ink-900/70 px-3 py-2.5">
        <span aria-hidden="true" className="h-1.5 w-1.5 rounded-full bg-brand-400" />
        <span className="text-sm font-medium text-text-primary">{module.railLabel}</span>
        <span className="ml-auto font-mono text-2xs uppercase tracking-[0.14em] text-text-faint">
          {module.railGroup}
        </span>
      </div>

      <ul className="mt-4 space-y-2">
        {module.capabilities.slice(0, 4).map((item) => (
          <li
            key={item}
            className="flex items-start gap-2.5 rounded-lg border border-line/50 bg-ink-950/40 px-3 py-2 text-xs leading-relaxed text-text-muted"
          >
            <span aria-hidden="true" className="mt-1.5 h-1 w-1 shrink-0 rounded-full bg-line-strong" />
            {item}
          </li>
        ))}
      </ul>

      <Link href={module.href} className="link-underline mt-4 text-xs">
        Read the full module <ArrowRight />
      </Link>
    </div>
  );
}

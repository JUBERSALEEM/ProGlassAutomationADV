import Image from "next/image";
import Link from "next/link";
import type { ProductModule } from "@/data/modules";
import { cn } from "@/lib/cn";
import { Reveal } from "@/components/ui/Reveal";
import { ArrowRight, ButtonLink } from "@/components/ui/Button";
import { IndexChip } from "@/components/ui/Badge";
import { LiveSurface } from "@/components/product/LiveSurface";

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
 * The module's live panel.
 *
 * Every module gets a working preview rather than a decorative graphic or an
 * empty placeholder: its own tabs, its own field names, sample data the visitor
 * can sort, filter and step through. Where the module has real content to
 * compute — the optimizer, the production queues — it computes it.
 */
function ModulePanel({ module }: { module: ProductModule }) {
  return (
    <div className="plate p-4 sm:p-5">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <span className="inline-flex items-center gap-2.5 rounded-full border border-line-strong/70 bg-ink-900/70 px-3 py-1.5">
          <span aria-hidden="true" className="h-1.5 w-1.5 rounded-full bg-brand-400" />
          <span className="text-xs font-medium text-text-primary">{module.railLabel}</span>
          <span className="font-mono text-[0.5625rem] uppercase tracking-[0.14em] text-text-faint">
            {module.railGroup}
          </span>
        </span>
        <span className="font-mono text-2xs text-brand-300">{module.moduleKey}</span>
      </div>

      <div className="card-live mt-4 p-3.5 sm:p-4">
        <LiveSurface
          moduleKey={module.moduleKey}
          name={module.name}
          capabilities={module.capabilities}
        />
      </div>

      <div className="mt-3.5 flex flex-wrap items-center justify-between gap-3">
        <Link href={module.href} className="link-underline text-xs">
          Read the full module <ArrowRight />
        </Link>
        <span className="mono-label">{module.evidence}</span>
      </div>
    </div>
  );
}

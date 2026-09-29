import Link from "next/link";
import { businessChain } from "@/data/workflows";
import { cn } from "@/lib/cn";
import { Reveal } from "@/components/ui/Reveal";

/**
 * The business chain as a vertical rail.
 *
 * Every stage below exists: the statuses are the real status vocabularies
 * from Services/Workflow and docs/WORKFLOW.md, and the guarantees are the
 * ones the code actually enforces (atomic BOQ import, a single pricing
 * engine, double-count-safe delivery balance, and so on).
 */
export function WorkflowRail({ compact = false }: { compact?: boolean }) {
  return (
    <ol className="relative space-y-3">
      {/* The connecting spine. */}
      <span
        aria-hidden="true"
        className="absolute left-[1.375rem] top-4 bottom-4 w-px bg-gradient-to-b from-brand-400/60 via-line-strong to-brand-400/25"
      />
      {businessChain.map((stage, index) => (
        <Reveal key={stage.id} delay={index * 60} as="li" className="relative">
          <div className="group flex gap-4">
            <span
              aria-hidden="true"
              className="relative z-10 mt-4 grid h-11 w-11 shrink-0 place-items-center rounded-xl border border-line-strong/80 bg-ink-900 font-mono text-xs font-semibold text-brand-300 transition-colors duration-300 group-hover:border-brand-400/50 group-hover:text-brand-200"
            >
              {stage.index}
            </span>

            <div className="min-w-0 flex-1 rounded-card border border-line/60 bg-ink-900/55 p-4 transition-colors duration-300 group-hover:border-line-strong sm:p-5">
              <div className="flex flex-wrap items-center gap-x-3 gap-y-1.5">
                <h3 className="text-base font-semibold text-text-primary">{stage.name}</h3>
                <span className="mono-label">{stage.moduleKey}</span>
              </div>

              <p className="mt-2 text-sm leading-relaxed text-text-muted">{stage.summary}</p>

              {!compact ? (
                <>
                  <dl className="mt-3.5 space-y-2 border-t border-line/50 pt-3.5">
                    <div className="flex flex-col gap-0.5 sm:flex-row sm:gap-3">
                      <dt className="w-32 shrink-0 font-mono text-2xs uppercase tracking-[0.14em] text-text-faint font-medium">
                        Carries forward
                      </dt>
                      <dd className="text-sm text-text-secondary">{stage.handoff}</dd>
                    </div>
                    <div className="flex flex-col gap-0.5 sm:flex-row sm:gap-3">
                      <dt className="w-32 shrink-0 font-mono text-2xs uppercase tracking-[0.14em] text-text-faint font-medium">
                        Guarantee
                      </dt>
                      <dd className="text-sm leading-relaxed text-text-muted">{stage.guarantee}</dd>
                    </div>
                  </dl>

                  <div className="mt-3.5 flex flex-wrap items-center gap-1.5">
                    {stage.statuses.map((status) => (
                      <span
                        key={status}
                        className="rounded-md border border-line-strong/60 bg-ink-850/80 px-2 py-0.5 font-mono text-[0.75rem] tracking-tight text-text-muted"
                      >
                        {status}
                      </span>
                    ))}
                  </div>

                  <p className="mono-label mt-3">{stage.evidence}</p>
                  <Link
                    href={stage.href}
                    className="link-underline mt-3 text-xs"
                    aria-label={`Read more about ${stage.name}`}
                  >
                    Read more
                  </Link>
                </>
              ) : (
                <p className="mono-label mt-2.5">{stage.handoff}</p>
              )}
            </div>
          </div>
        </Reveal>
      ))}
    </ol>
  );
}

/**
 * The same chain as a horizontal rail — used in the hero band and on
 * "How it works" as a sticky progress indicator.
 */
export function ChainStrip({ activeId, className }: { activeId?: string; className?: string }) {
  return (
    <ol className={cn("no-scrollbar flex gap-2 overflow-x-auto pb-1", className)}>
      {businessChain.map((stage, index) => {
        const active = stage.id === activeId;
        return (
          <li key={stage.id} className="flex shrink-0 items-center gap-2">
            <span
              className={cn(
                "flex items-center gap-2 rounded-full border px-3 py-1.5 font-mono text-2xs uppercase tracking-[0.12em] transition-colors duration-300",
                active
                  ? "border-brand-400/60 bg-brand-400/12 text-brand-100"
                  : "border-line-strong/60 bg-ink-900/60 text-text-faint",
              )}
            >
              <span className={cn("tabular-nums", active && "text-brand-300")}>{stage.index}</span>
              {stage.name}
            </span>
            {index < businessChain.length - 1 ? (
              <span aria-hidden="true" className="h-px w-4 bg-line-strong" />
            ) : null}
          </li>
        );
      })}
    </ol>
  );
}

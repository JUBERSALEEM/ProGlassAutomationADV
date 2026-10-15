"use client";

import { useMemo, useState } from "react";
import { cn } from "@/lib/cn";
import { Badge } from "@/components/ui/Badge";
import { AnimatePresence, motion, useReducedMotion } from "motion/react";
import { dur, springs } from "@/lib/motion";
import { moduleCounts, modules, type ModuleGroup, type ProductModule } from "@/data/modules";
import { groupIcons } from "@/data/groupIcons";
import { Icon } from "@/components/ui/Icon";
import { useT } from "@/i18n/provider";

/**
 * The module browser.
 *
 * Every module in the application, grouped exactly as the sidebar groups them,
 * filterable by rail group and searchable by name, capability or product
 * vocabulary. The rail group is the outer structure rather than a flat grid,
 * because that is how an operator actually finds a screen.
 */
export function ModuleBrowser() {
  const t = useT();
  const [group, setGroup] = useState<ModuleGroup | "all">("all");
  const [query, setQuery] = useState("");
  const reduced = useReducedMotion();

  const counts = useMemo(() => moduleCounts(), []);

  const visible = useMemo(() => {
    const needle = query.trim().toLowerCase();
    return modules.filter((item) => {
      if (group !== "all" && item.railGroup !== group) return false;
      if (!needle) return true;
      const haystack = [
        item.name,
        item.railLabel,
        item.summary,
        item.evidence,
        item.tags.join(" "),
        item.capabilities.join(" "),
      ]
        .join(" ")
        .toLowerCase();
      return haystack.includes(needle);
    });
  }, [group, query]);

  const grouped = useMemo(() => {
    const map = new Map<ModuleGroup, ProductModule[]>();
    for (const item of visible) {
      const list = map.get(item.railGroup) ?? [];
      list.push(item);
      map.set(item.railGroup, list);
    }
    return [...map.entries()];
  }, [visible]);

  return (
    <div>
      {/* ── Controls ─────────────────────────────────────────────── */}
      <div className="flex flex-col gap-4 lg:flex-row lg:items-center lg:justify-between">
        <div className="flex flex-wrap gap-2" role="group" aria-label={t("Filter modules by navigation group")}>
          <FilterChip active={group === "all"} onClick={() => setGroup("all")} count={modules.length}>
            {t("All modules")}
          </FilterChip>
          {counts.map((entry) => (
            <FilterChip
              key={t(entry.group)}
              active={group === entry.group}
              onClick={() => setGroup(entry.group)}
              count={entry.count}
              title={t(entry.note)}
            >
              {t(entry.group)}
            </FilterChip>
          ))}
        </div>

        <label className="relative block lg:w-72">
          <span className="sr-only">{t("Search modules")}</span>
          <svg
            aria-hidden="true"
            viewBox="0 0 16 16"
            className="pointer-events-none absolute left-3 top-1/2 h-3.5 w-3.5 -translate-y-1/2 text-text-faint"
            fill="none"
            stroke="currentColor"
            strokeWidth="1.6"
            strokeLinecap="round"
          >
            <circle cx="7" cy="7" r="4.5" />
            <path d="m10.5 10.5 3 3" />
          </svg>
          <input
            type="search"
            value={query}
            onChange={(event) => setQuery(event.target.value)}
            placeholder={t("Search modules, e.g. tempering, VAT, traceability")}
            className="h-10 w-full rounded-xl border border-line-strong/70 bg-ink-900/70 pl-9 pr-3 text-sm text-text-primary transition-colors duration-200 placeholder:text-text-faint focus:border-brand-400/70"
          />
        </label>
      </div>

      <p className="mt-4 text-xs leading-relaxed text-text-faint font-medium" role="status">
        {visible.length} of {modules.length} modules shown
        {group === "all" ? "" : ` in ${group}`}
        {query.trim() ? ` matching “${query.trim()}”` : ""}.
      </p>

      {/* ── Groups ───────────────────────────────────────────────── */}
      <div className="mt-10 space-y-14">
        {grouped.map(([groupName, list]) => {
          const note = counts.find((entry) => entry.group === groupName)?.note;
          return (
          <div key={groupName} id={`group-${slug(groupName)}`} className="scroll-mt-28">
            <div className="flex flex-wrap items-baseline justify-between gap-3 border-b border-line/60 pb-3">
              <h3 className="flex items-center gap-2.5 font-mono text-sm font-semibold uppercase tracking-[0.18em] text-brand-300">
                <span className="grid h-7 w-7 place-items-center rounded-lg border border-line/60 bg-ink-900/50">
                  <Icon path={groupIcons[groupName]} className="h-3.5 w-3.5" />
                </span>
                {t(groupName)}
              </h3>
              <p className="text-xs text-text-faint font-medium">
                {note ? t(note) : null}
              </p>
            </div>

            {/*
              Each card is the anchor for its own module, so a link like
              /features#tax-invoice lands on that module instead of at the top
              of a very long page. `scroll-mt` keeps it clear of the sticky
              navigation once it is there.
            */}
            <ul className="mt-6 grid gap-5 sm:grid-cols-2 xl:grid-cols-3">
              <AnimatePresence mode="popLayout">
                {list.map((item) => (
                  <motion.li
                    key={item.id}
                    id={item.id}
                    layout={!reduced}
                    initial={{ opacity: 0, scale: reduced ? 1 : 0.96 }}
                    animate={{ opacity: 1, scale: 1 }}
                    exit={{ opacity: 0, scale: reduced ? 1 : 0.96 }}
                    transition={{ layout: springs.layout, duration: dur.card, ease: "easeOut" }}
                    className="scroll-mt-28"
                  >
                    <ModuleCard module={item} />
                  </motion.li>
                ))}
              </AnimatePresence>
            </ul>
          </div>
          );
        })}

        {visible.length === 0 ? (
          <div className="surface text-center card-pad">
            <p className="text-sm text-text-muted">{t("No module matches that search. The application has {v1} navigation targets; clear the search or pick another group.", { v1: modules.length })}</p>
          </div>
        ) : null}
      </div>
    </div>
  );
}

function ModuleCard({ module }: { module: ProductModule }) {
  const t = useT();
  return (
    <article className="surface card-interactive flex h-full flex-col p-5">
      <div className="flex items-start justify-between gap-3">
        <span className="font-mono text-2xs tabular-nums text-text-faint font-medium">{module.number}</span>
        <Badge tone="neutral">{module.railGroup}</Badge>
      </div>

      <h4 className="mt-3 text-base font-semibold text-text-primary">{t(module.name)}</h4>
      <p className="mt-2 text-sm leading-relaxed text-text-muted">{t(module.summary)}</p>

      <ul className="mt-4 space-y-1.5">
        {module.capabilities.slice(0, 3).map((item) => (
          <li key={item} className="flex gap-2.5 text-xs leading-relaxed text-text-muted">
            <span aria-hidden="true" className="mt-1.5 h-1 w-1 shrink-0 rounded-full bg-brand-400/80" />
            {t(item)}
          </li>
        ))}
      </ul>

      <div className="mt-auto pt-5">
        {module.statusNote ? (
          <p className="mb-3 rounded-card border border-line/60 bg-ink-800/60 px-2.5 py-1.5 text-2xs leading-relaxed text-text-faint font-medium">
            {t(module.statusNote)}
          </p>
        ) : null}
        <p className="mono-label border-t border-line/60 pt-3">
          <span className="sr-only">{t("Navigation label:")}</span>
          {t(module.railLabel)}
        </p>
      </div>
    </article>
  );
}

function FilterChip({
  active,
  onClick,
  count,
  title,
  children,
}: {
  active: boolean;
  onClick: () => void;
  count: number;
  title?: string;
  children: React.ReactNode;
}) {
  return (
    <button
      type="button"
      onClick={onClick}
      aria-pressed={active}
      title={title}
      className={cn(
        "rounded-full border px-3.5 py-1.5 text-xs font-medium transition-colors duration-200",
        active
          ? "border-brand-400/60 bg-brand-400/12 text-brand-200"
          : "border-line-strong/70 bg-ink-850/60 text-text-muted hover:border-brand-400/40 hover:text-text-primary",
      )}
    >
      {children}
      <span className="ml-1.5 font-mono text-[0.75rem] tabular-nums text-text-faint font-medium">{count}</span>
    </button>
  );
}

function slug(value: string): string {
  return value.toLowerCase().replace(/[^a-z0-9]+/g, "-");
}

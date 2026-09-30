import type { ElementType, ReactNode } from "react";
import { cn } from "@/lib/cn";

/**
 * The glass primitives.
 *
 * Three components, and they exist because the site needed one card system
 * rather than a fifth way to draw a box. Everything here is a thin shell over
 * the classes in `globals.css` — `glass-primary`, `surface-raised`, `surface`,
 * `glass-metric` — so a page composes a layout out of them, and the appearance
 * family decides what they are made of. Studio renders them as the product's
 * own flat cards; Crystal renders the same markup as layered glass. Nothing in
 * a page ever mentions colour, blur or shadow.
 *
 * They are server components on purpose. A card is structure, not behaviour:
 * wrapping one in `<Reveal>` is what animates it, and a card that has to be a
 * client component to be drawn would ship JavaScript for a border-radius.
 */

type GlassTier = "primary" | "raised" | "card";

const tierClass: Record<GlassTier, string> = {
  primary: "glass-primary",
  raised: "surface-raised",
  card: "surface",
};

export type GlassCardHead = {
  /** Small spaced label above the title. */
  eyebrow?: string;
  title: ReactNode;
  /** One short line under the title. Keep it to a sentence. */
  lead?: ReactNode;
  /** Buttons or links, right-aligned on a wide card. */
  actions?: ReactNode;
  /** Heading level for the card's own title. Defaults to `h3`. */
  titleAs?: "h2" | "h3" | "h4";
  id?: string;
};

/**
 * A single card in the site's surface system.
 *
 * `tier="primary"` is the one card per section that carries the section's
 * subject — it is larger, more lit, and it is where the metrics and the way in
 * go. `tier="raised"` is an elevated panel; `tier="card"` is a normal card of
 * content. Use one primary card per section, and let the related cards below it
 * be quieter: a page where every card is the primary card has no hierarchy at
 * all, which is the problem this system exists to solve.
 */
export function GlassCard({
  tier = "card",
  head,
  interactive = false,
  className,
  bodyClassName,
  as: Tag = "div",
  children,
}: {
  tier?: GlassTier;
  head?: GlassCardHead;
  /** Adds the hover/focus edge from the application's task cards. */
  interactive?: boolean;
  className?: string;
  /** Padding for the body, when a card needs to be tighter than the default. */
  bodyClassName?: string;
  as?: ElementType;
  children?: ReactNode;
}) {
  const Title = head?.titleAs ?? "h3";

  return (
    <Tag
      className={cn(
        tierClass[tier],
        "card-pad",
        interactive && "card-interactive",
        className,
      )}
    >
      {head ? (
        <div
          className={cn(
            "glass-primary-head flex flex-col gap-3 pb-5 sm:flex-row sm:items-start sm:justify-between sm:gap-6",
            !children && "glass-primary-head-last pb-0",
          )}
        >
          <div className="min-w-0">
            {head.eyebrow ? <p className="eyebrow mb-2.5">{head.eyebrow}</p> : null}
            <Title id={head.id} className={tier === "primary" ? "display-2" : "card-title"}>
              {head.title}
            </Title>
            {head.lead ? (
              <p
                className={cn(
                  "mt-2.5 leading-relaxed text-text-muted",
                  tier === "primary" ? "max-w-2xl text-sm sm:text-base" : "text-sm",
                )}
              >
                {head.lead}
              </p>
            ) : null}
          </div>
          {head.actions ? (
            <div className="flex shrink-0 flex-wrap items-center gap-2.5">{head.actions}</div>
          ) : null}
        </div>
      ) : null}
      {children ? <div className={cn(head ? "pt-5" : undefined, bodyClassName)}>{children}</div> : null}
    </Tag>
  );
}

/**
 * A figure inside a primary card.
 *
 * The number is the point, so it is the loudest thing on the tile and it is set
 * in the mono face with tabular figures — a row of these must line up, and
 * proportional digits in a row of statistics is the tell that nobody looked.
 *
 * These are counts of what the application actually contains, or values derived
 * from the site's own data. There are no performance percentages anywhere on
 * this site and there is nothing here that would need one.
 */
export function GlassMetric({
  value,
  label,
  hint,
  className,
}: {
  value: ReactNode;
  label: ReactNode;
  hint?: ReactNode;
  className?: string;
}) {
  return (
    <div className={cn("glass-metric", className)}>
      <p className="metric-value">{value}</p>
      <p className="mt-2 text-sm font-medium text-text-secondary">{label}</p>
      {hint ? <p className="mt-1.5 text-xs leading-relaxed text-text-muted">{hint}</p> : null}
    </div>
  );
}

/**
 * A row of metrics belonging to one card.
 *
 * Two columns on a phone and up to four on a wide screen: a metric is a short
 * number and a short label, and squeezing four of them into 320 px is how a
 * statistic becomes unreadable.
 */
export function GlassMetricRow({
  className,
  children,
}: {
  className?: string;
  children: ReactNode;
}) {
  return (
    <div className={cn("grid gap-x-6 gap-y-5 sm:grid-cols-2 lg:grid-cols-4", className)}>
      {children}
    </div>
  );
}

/** A rule between blocks inside a card, lit rather than drawn. */
export function GlassDivider({ className }: { className?: string }) {
  return <hr className={cn("glass-divider my-6 border-0", className)} />;
}

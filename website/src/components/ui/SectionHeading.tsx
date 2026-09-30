import type { ReactNode } from "react";
import { cn } from "@/lib/cn";

/**
 * Section heading.
 *
 * A server component on purpose: headings are static content and should not
 * ship JavaScript. Wrap one in `<Reveal>` when it should animate in.
 */
export function SectionHeading({
  eyebrow,
  title,
  lead,
  align = "left",
  as: Tag = "h2",
  id,
  className,
  gradient = false,
  children,
}: {
  eyebrow?: string;
  title: ReactNode;
  lead?: ReactNode;
  align?: "left" | "center";
  as?: "h1" | "h2" | "h3";
  id?: string;
  className?: string;
  /** Paints the headline in the accent gradient. Use once per page at most. */
  gradient?: boolean;
  children?: ReactNode;
}) {
  return (
    <div className={cn("max-w-3xl", align === "center" && "mx-auto text-center", className)}>
      {eyebrow ? (
        <p className={cn("eyebrow mb-3 flex items-center gap-3", align === "center" && "justify-center")}>
          {align === "center" ? null : <span aria-hidden="true" className="h-px w-8 bg-brand-400/60" />}
          {eyebrow}
        </p>
      ) : null}
      <Tag
        id={id}
        className={cn(
          /*
           * One hierarchy for every section on the site. The size steps down
           * with the viewport rather than being fixed at the top: a 30 px
           * heading on a 375 px phone is a third of the screen, and the point
           * of the type scale is that the heading is read *and* the lede under
           * it is still visible without scrolling.
           */
          "display-2",
          gradient && "text-gradient",
        )}
      >
        {title}
      </Tag>
      {lead ? <p className="lede mt-5">{lead}</p> : null}
      {children}
    </div>
  );
}

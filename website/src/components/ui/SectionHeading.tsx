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
  /* The site reads centred: a heading is a stage mark, and one ragged left
     column per section was the residue of a draft. Pages that genuinely want
     a left column (long prose) say so with align="left". */
  align = "center",
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
    /* flex-col + items-center so any block child under the heading (a row of
       buttons, e.g.) lands on the same centre axis as the text itself. */
    <div
      className={cn(
        "max-w-3xl",
        align === "center" && "mx-auto flex flex-col items-center text-center",
        className,
      )}
    >
      {eyebrow ? (
        /*
         * The eyebrow carries the same rule it always did when it opens a
         * left-aligned section, and drops it when it is the label on a card or
         * a centred heading — a rule that leads into nothing is a line drawn
         * for its own sake. Either way it is the site's own label treatment:
         * 12 px, wide tracking, the accent ramp.
         */
        <p className={cn("eyebrow mb-3.5 flex items-center gap-3", align === "center" && "justify-center")}>
          {align === "left" ? <span aria-hidden="true" className="h-px w-9 bg-gradient-to-r from-brand-400/80 to-transparent" /> : null}
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
          gradient && "text-gradient-rich",
        )}
      >
        {title}
      </Tag>
      {lead ? <p className="lede mt-5 max-w-measure">{lead}</p> : null}
      {children}
    </div>
  );
}

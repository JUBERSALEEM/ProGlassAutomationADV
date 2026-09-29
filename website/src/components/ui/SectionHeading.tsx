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
  children,
}: {
  eyebrow?: string;
  title: ReactNode;
  lead?: ReactNode;
  align?: "left" | "center";
  as?: "h1" | "h2" | "h3";
  id?: string;
  className?: string;
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
      <Tag id={id} className="text-3xl leading-[1.1] sm:text-4xl lg:text-[2.75rem]">
        {title}
      </Tag>
      {lead ? <p className="lede mt-5">{lead}</p> : null}
      {children}
    </div>
  );
}

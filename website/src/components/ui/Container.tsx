import type { ElementType, ReactNode } from "react";
import { cn } from "@/lib/cn";

export function Container({
  children,
  className,
  as: Tag = "div",
}: {
  children: ReactNode;
  className?: string;
  as?: ElementType;
}) {
  return <Tag className={cn("shell", className)}>{children}</Tag>;
}

export function Section({
  children,
  className,
  id,
  tone = "default",
  as: Tag = "section",
  ...rest
}: {
  children: ReactNode;
  className?: string;
  id?: string;
  tone?: "default" | "raised" | "sunken";
  as?: ElementType;
  "aria-labelledby"?: string;
}) {
  /*
   * Three tones, and each one is a surface rather than a fill.
   *
   * They used to be `bg-ink-900/55` and `bg-ink-850/60` — a flat wash of one
   * of the page's own shades, which on a deep page is very nearly the same
   * colour as the page and reads as nothing at all. Each tone now carries its
   * own light:
   *
   *   raised  the family's accent washing in from the top-left corner and the
   *           glass colour from the bottom-right, i.e. a lit band
   *   sunken  a well: a shade below the page colour, with a soft lip at the
   *           top where the page steps down into it
   *
   * Both are drawn from tokens (`.band-rich` / `.band-deep` in globals.css), so
   * switching palette family re-hues every banded section on the site at once.
   */
  const tones = {
    default: "",
    raised: "band-rich border-y border-line-strong/50",
    sunken: "band-deep border-y border-line/50",
  } as const;

  return (
    <Tag id={id} className={cn("section relative", tones[tone], className)} {...rest}>
      {tone === "raised" ? (
        /* A violet hairline marks the top of a banded section without adding weight. */
        <>
          <span aria-hidden="true" className="rule-violet absolute inset-x-0 -top-px" />
          <span aria-hidden="true" className="rule-violet absolute inset-x-0 -bottom-px" />
        </>
      ) : null}
      {children}
    </Tag>
  );
}

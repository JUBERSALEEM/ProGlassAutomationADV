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
  const tones = {
    default: "",
    raised: "border-y border-line-strong/45 bg-ink-900/55",
    sunken: "border-y border-line/45 bg-ink-850/60",
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

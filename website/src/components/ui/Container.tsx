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
    raised: "bg-ink-900/45 border-y border-line/60",
    sunken: "bg-ink-950/60 border-y border-line/40",
  } as const;

  return (
    <Tag id={id} className={cn("section relative", tones[tone], className)} {...rest}>
      {children}
    </Tag>
  );
}

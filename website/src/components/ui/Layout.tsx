import type { ElementType, ReactNode } from "react";
import { cn } from "@/lib/cn";

type Space = "xs" | "sm" | "md" | "lg" | "xl";

const flowGap: Record<Space, string> = {
  xs: "flow-xs",
  sm: "flow-sm",
  md: "flow-md",
  lg: "flow-lg",
  xl: "flow-xl",
};

/** Vertical rhythm primitive. It keeps route templates on the shared spacing scale. */
export function Flow({
  children,
  space = "md",
  className,
  as: Tag = "div",
}: {
  children: ReactNode;
  space?: Space;
  className?: string;
  as?: ElementType;
}) {
  return <Tag className={cn("flow", flowGap[space], className)}>{children}</Tag>;
}

/** Wrapping action/metadata row with a consistent touch-safe gap. */
export function Cluster({
  children,
  space = "sm",
  align = "center",
  justify = "start",
  className,
  as: Tag = "div",
}: {
  children: ReactNode;
  space?: "xs" | "sm" | "md";
  align?: "start" | "center" | "end";
  justify?: "start" | "center" | "between";
  className?: string;
  as?: ElementType;
}) {
  return (
    <Tag
      className={cn(
        "cluster",
        `cluster-${space}`,
        `cluster-align-${align}`,
        `cluster-justify-${justify}`,
        className,
      )}
    >
      {children}
    </Tag>
  );
}

/** Count-aware card collection; children grow instead of leaving empty tracks. */
export function AutoGrid({
  children,
  density = "standard",
  className,
  as: Tag = "div",
}: {
  children: ReactNode;
  density?: "compact" | "standard" | "wide";
  className?: string;
  as?: ElementType;
}) {
  return <Tag className={cn("auto-grid", `auto-grid-${density}`, className)}>{children}</Tag>;
}

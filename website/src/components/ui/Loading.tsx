import { cn } from "@/lib/cn";

type Size = "sm" | "md" | "lg";

const dimensions: Record<Size, string> = { sm: "h-3", md: "h-4", lg: "h-6" };

export function Skeleton({ className, size = "md" }: { className?: string; size?: Size }) {
  return <span aria-hidden="true" className={cn("skeleton block rounded-md bg-line-strong/35", dimensions[size], className)} />;
}

/** Determinate progress uses native semantics; omit value for indeterminate. */
export function Progress({
  label,
  value,
  className,
}: {
  label: string;
  value?: number;
  className?: string;
}) {
  const bounded = value === undefined ? undefined : Math.min(100, Math.max(0, value));
  return (
    <div className={className}>
      <div className="mb-2 flex items-center justify-between gap-3 text-xs font-medium text-text-secondary">
        <span>{label}</span>
        {bounded !== undefined ? <span className="font-mono tabular-nums">{Math.round(bounded)}%</span> : null}
      </div>
      <progress
        className="theme-progress h-2 w-full overflow-hidden rounded-full"
        max={100}
        value={bounded}
        aria-label={label}
      />
    </div>
  );
}

export function Spinner({ label, size = "md", className }: { label: string; size?: Size; className?: string }) {
  const ring = size === "sm" ? "h-4 w-4" : size === "lg" ? "h-7 w-7" : "h-5 w-5";
  return (
    <span className={cn("inline-flex items-center gap-2 text-sm text-text-secondary", className)} role="status">
      <span aria-hidden="true" className={cn("spinner rounded-full border-2 border-current border-t-transparent", ring)} />
      <span>{label}</span>
    </span>
  );
}

import { cn } from "@/lib/cn";

/**
 * The ProGlass Automation mark.
 *
 * The product cuts glass sheets, so the mark is a sheet: a square pane with one
 * corner taken off — the mitre — holding a second pane behind it for the
 * double-glazed build-up, with a specular highlight across the face. It is
 * drawn with the site's own tokens rather than fixed colours, so it re-tints
 * with the palette family and the appearance studio like everything else.
 *
 * `id` must be unique per instance on a page because the gradients are shared
 * by reference.
 */
export function LogoMark({
  className,
  size = 28,
  id = "pg-logo",
}: {
  className?: string;
  size?: number;
  id?: string;
}) {
  return (
    <svg
      viewBox="0 0 32 32"
      width={size}
      height={size}
      className={cn("shrink-0", className)}
      role="img"
      aria-label="ProGlass Automation"
    >
      <defs>
        <linearGradient id={`${id}-face`} x1="0" y1="0" x2="1" y2="1">
          <stop offset="0%" stopColor="rgb(var(--brand-300))" />
          <stop offset="52%" stopColor="rgb(var(--brand-500))" />
          <stop offset="100%" stopColor="rgb(var(--brand-700))" />
        </linearGradient>
        <linearGradient id={`${id}-inner`} x1="0" y1="1" x2="1" y2="0">
          <stop offset="0%" stopColor="rgb(var(--glass-cyan))" stopOpacity="0.85" />
          <stop offset="100%" stopColor="rgb(var(--brand-300))" stopOpacity="0.55" />
        </linearGradient>
        <linearGradient id={`${id}-glint`} x1="0" y1="0" x2="1" y2="1">
          <stop offset="0%" stopColor="#FFFFFF" stopOpacity="0.5" />
          <stop offset="60%" stopColor="#FFFFFF" stopOpacity="0" />
        </linearGradient>
      </defs>

      {/* Second pane, offset behind the front sheet. */}
      <path
        d="M8.5 3.5h20l-4 4h-16z"
        fill={`url(#${id}-inner)`}
        opacity="0.85"
      />
      <path d="M4.5 12.5h20l-4 4h-16z" fill={`url(#${id}-inner)`} opacity="0.6" />

      {/* Front sheet with the mitred corner. */}
      <path
        d="M3.5 3.5h17.5l7.5 7.5v17.5H3.5z M21 3.5v7.5h7.5"
        fill={`url(#${id}-face)`}
        stroke="rgb(var(--brand-200) / 0.55)"
        strokeWidth="0.8"
        strokeLinejoin="round"
      />

      {/* Specular highlight and the cut-line that defines the mitre. */}
      <path d="M3.5 3.5 L21 3.5 L12 24 L3.5 24 z" fill={`url(#${id}-glint)`} />
      <path
        d="M21 3.5v7.5h7.5"
        fill="none"
        stroke="rgb(255 255 255 / 0.55)"
        strokeWidth="0.9"
        strokeLinejoin="round"
      />
    </svg>
  );
}

/** Mark plus wordmark. The lockup used in the header and the footer. */
export function Logo({
  className,
  size = 28,
  showWordmark = true,
  subtitle = "Automation",
}: {
  className?: string;
  size?: number;
  showWordmark?: boolean;
  subtitle?: string | null;
}) {
  return (
    <span className={cn("flex items-center gap-2.5", className)}>
      <LogoMark size={size} id={`lockup-${size}`} />
      {showWordmark ? (
        <span className="flex flex-col leading-none">
          <span className="text-[0.95rem] font-semibold tracking-tight text-text-primary">
            Pro<span className="text-brand-300">Glass</span>
          </span>
          {subtitle ? (
            <span className="mt-0.5 font-mono text-[0.5rem] uppercase tracking-[0.28em] text-text-faint">
              {subtitle}
            </span>
          ) : null}
        </span>
      ) : null}
    </span>
  );
}

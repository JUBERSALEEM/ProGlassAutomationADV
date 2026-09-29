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
 * The gradients live once in `<SvgDefs />` (rendered by the root layout) and
 * are referenced by a fixed id, so a page with 39 of these marks still carries
 * a single copy of the definitions.
 */
export function LogoMark({
  className,
  size = 28,
}: {
  className?: string;
  size?: number;
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

      {/* Second pane, offset behind the front sheet. */}
      <path
        d="M8.5 3.5h20l-4 4h-16z"
        fill="url(#pg-logo-inner)"
        opacity="0.85"
      />
      <path d="M4.5 12.5h20l-4 4h-16z" fill="url(#pg-logo-inner)" opacity="0.6" />

      {/* Front sheet with the mitred corner. */}
      <path
        d="M3.5 3.5h17.5l7.5 7.5v17.5H3.5z M21 3.5v7.5h7.5"
        fill="url(#pg-logo-face)"
        stroke="rgb(var(--brand-200) / 0.55)"
        strokeWidth="0.8"
        strokeLinejoin="round"
      />

      {/* Specular highlight and the cut-line that defines the mitre. */}
      <path d="M3.5 3.5 L21 3.5 L12 24 L3.5 24 z" fill="url(#pg-logo-glint)" />
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
      <LogoMark size={size} />
      {showWordmark ? (
        <span className="flex flex-col leading-none">
          <span className="text-[0.95rem] font-semibold tracking-tight text-text-primary">
            Pro<span className="text-brand-300">Glass</span>
          </span>
          {subtitle ? (
            <span className="mt-0.5 font-mono text-[0.5rem] uppercase tracking-[0.28em] text-text-faint font-medium">
              {subtitle}
            </span>
          ) : null}
        </span>
      ) : null}
    </span>
  );
}

import Link from "next/link";
import type { ComponentPropsWithoutRef, ReactNode } from "react";
import { cn } from "@/lib/cn";

type Variant = "primary" | "secondary" | "ghost" | "quiet";
type Size = "sm" | "md" | "lg";

const base =
  "group relative inline-flex select-none items-center justify-center gap-2 rounded-xl font-semibold tracking-tight transition-all duration-200 ease-industrial disabled:cursor-not-allowed disabled:opacity-55";

const variants: Record<Variant, string> = {
  primary:
    "btn-primary bg-brand-400 text-accent-ink shadow-accent hover:bg-brand-300 hover:shadow-accent-strong hover:-translate-y-px active:translate-y-0",
  secondary:
    "border border-line-strong/80 bg-ink-850/80 text-text-primary hover:border-brand-400/60 hover:bg-ink-800 hover:-translate-y-px active:translate-y-0",
  ghost: "border border-line/70 text-text-secondary hover:border-brand-400/45 hover:text-text-primary hover:bg-ink-900/70",
  quiet: "text-brand-300 hover:text-brand-200 px-0 py-0",
};

const sizes: Record<Size, string> = {
  sm: "h-9 px-3.5 text-[0.8125rem]",
  md: "h-11 px-5 text-sm",
  lg: "h-12 px-6 text-[0.9375rem] sm:h-[3.25rem] sm:px-7",
};

type CommonProps = {
  children: ReactNode;
  variant?: Variant;
  size?: Size;
  className?: string;
  /** Shown after the label — typically an arrow glyph. */
  trailing?: ReactNode;
};

export function ButtonLink({
  href,
  children,
  variant = "primary",
  size = "md",
  className,
  trailing,
  external,
  ...rest
}: CommonProps & { href: string; external?: boolean } & Omit<ComponentPropsWithoutRef<"a">, "href" | "children" | "className">) {
  const classes = cn(base, variants[variant], sizes[size], variant === "quiet" && "h-auto px-0", className);

  /*
   * A protocol link is not a route. `mailto:` and `tel:` go straight through
   * the locale-aware `next/link` wrapper otherwise, and `hrefIn` — which is
   * right to assume every href it sees is a path — turns them into
   * `/mailto:…`. They are plain anchors: there is no route to prefetch and no
   * language to keep, and opening the mail client in a new tab is not what a
   * visitor pressing "Email the developer" asked for.
   */
  const isProtocolLink = /^(mailto:|tel:|sms:)/i.test(href);
  if (isProtocolLink) {
    return (
      <a href={href} className={classes} {...rest}>
        {children}
        {trailing}
      </a>
    );
  }

  if (external) {
    return (
      <a href={href} className={classes} rel="noreferrer noopener" target="_blank" {...rest}>
        {children}
        {trailing}
      </a>
    );
  }

  return (
    <Link href={href} className={classes} {...rest}>
      {children}
      {trailing}
    </Link>
  );
}

export function Button({
  children,
  variant = "primary",
  size = "md",
  className,
  trailing,
  loading,
  ...rest
}: CommonProps & { loading?: boolean } & ComponentPropsWithoutRef<"button">) {
  return (
    <button
      className={cn(base, variants[variant], sizes[size], variant === "quiet" && "h-auto px-0", className)}
      aria-busy={loading || undefined}
      {...rest}
    >
      {loading ? (
        <span
          aria-hidden="true"
          className="h-4 w-4 shrink-0 animate-spin rounded-full border-2 border-current border-t-transparent"
        />
      ) : null}
      {children}
      {!loading && trailing}
    </button>
  );
}

/** The standard forward arrow — a glyph, not an emoji. */
export function ArrowRight({ className }: { className?: string }) {
  return (
    <svg
      aria-hidden="true"
      viewBox="0 0 16 16"
      className={cn("h-3.5 w-3.5 shrink-0 transition-transform duration-200 group-hover:translate-x-0.5", className)}
      fill="none"
      stroke="currentColor"
      strokeWidth="1.75"
      strokeLinecap="round"
      strokeLinejoin="round"
    >
      <path d="M2.5 8h11M9.5 4l4 4-4 4" />
    </svg>
  );
}

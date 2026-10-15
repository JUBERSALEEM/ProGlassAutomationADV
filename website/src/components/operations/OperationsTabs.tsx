"use client";

/**
 * The visible document hand-off chain shared by the operations pages.
 *
 * The first four steps have live website views: the read-only inquiry register,
 * confirmed proformas, the Job Order queue, and the delivery ledger. Tax Invoice and Statement
 * are the next product modules in the chain; until their live dashboards exist,
 * their links deliberately lead to the product overview rather than a fake
 * operational screen.
 */
import { usePathname } from "next/navigation";
import { LocaleLink } from "@/components/ui/LocaleLink";
import { pathWithoutLocale } from "@/i18n/core";
import { cn } from "@/lib/cn";
import { Icon } from "@/components/ui/Icon";
import { footerLinkIcons } from "@/data/footerIcons";
import { groupIcons } from "@/data/groupIcons";

/*
 * The document chain gets its marks here rather than in a data module: the
 * proforma's sales document, the job order's clipboard, the delivery truck,
 * the invoice's banknote and the statement's bars are the same glyphs the
 * footer and the rail hand out for those subjects — one chain, one hand.
 */
const STEPS = [
  { href: "/inquiries", match: "/inquiries", label: "Inquiry", hint: "Documents", icon: groupIcons.Sales },
  { href: "/reports?status=Confirmed", match: "/reports", label: "Proforma Invoice", hint: "Confirmed", icon: groupIcons.Sales },
  { href: "/job-orders", match: "/job-orders", label: "Job Order", hint: "Hand-off", icon: footerLinkIcons["/job-orders"]! },
  { href: "/deliveries", match: "/deliveries", label: "Delivery Note", hint: "Dispatch", icon: footerLinkIcons["/deliveries"]! },
  { href: "/features#group-sales", match: null, label: "Tax Invoice", hint: "Overview", icon: groupIcons.Finance },
  { href: "/features#group-reports", match: null, label: "Statement", hint: "Overview", icon: groupIcons.Reports },
] as const;

export function OperationsTabs({ className }: { className?: string }) {
  const pathname = usePathname();
  const current = pathWithoutLocale(pathname || "/");

  return (
    <nav
      aria-label="Proforma Invoice to Statement document chain"
      className={cn(
        "inline-flex max-w-full items-center gap-1 overflow-x-auto rounded-xl border border-line-strong/70 bg-ink-950/70 p-1",
        className,
      )}
    >
      {STEPS.map((step, index) => {
        const isActive = step.match !== null && (current === step.match || current.startsWith(`${step.match}/`));
        return (
          <div key={step.label} className="flex shrink-0 items-center gap-1">
            {index > 0 && <span aria-hidden="true" className="px-0.5 font-mono text-3xs text-text-faint/70">›</span>}
            <LocaleLink
              href={step.href}
              aria-current={isActive ? "page" : undefined}
              className={cn(
                "group flex shrink-0 items-center gap-1.5 whitespace-nowrap rounded-lg px-2.5 py-1.5 text-xs font-semibold transition-colors sm:px-3",
                isActive
                  ? "bg-brand-500/25 text-brand-200 shadow-sm"
                  : "text-text-muted hover:bg-ink-900/80 hover:text-text-primary",
              )}
              title={step.match ? step.label : `${step.label} product overview`}
            >
              <Icon path={step.icon} className={cn("h-3.5 w-3.5", isActive ? "text-brand-300" : "text-text-faint group-hover:text-brand-300")} />
              <span>{step.label}</span>
              <span
                className={cn(
                  "hidden font-mono text-3xs uppercase tracking-wider sm:inline",
                  isActive ? "text-brand-300/80" : "text-text-faint",
                )}
              >
                {step.hint}
              </span>
            </LocaleLink>
          </div>
        );
      })}
    </nav>
  );
}

export default OperationsTabs;

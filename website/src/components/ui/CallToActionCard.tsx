import type { ReactNode } from "react";
import { GlassCard } from "@/components/ui/Glass";

/**
 * Shared shell for the closing marketing calls to action.
 *
 * The content and links stay owned by each route; the surface, dynamic
 * corner-safe padding, alignment and compact-but-airy vertical rhythm are
 * shared. Composing GlassCard keeps family appearance and padding decisions
 * in the existing surface system rather than creating a second card engine.
 */
export function CallToActionCard({ children }: { children: ReactNode }) {
  return (
    <GlassCard
      tier="raised"
      className="text-center"
      bodyClassName="flex flex-col gap-5"
    >
      {children}
    </GlassCard>
  );
}

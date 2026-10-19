import type { Metadata } from "next";
import { Suspense } from "react";

import { OperatorOpener } from "@/components/operations/OperatorOpener";
import { ProductionRoom } from "@/components/production-room/ProductionRoom";
import { PageOverlay } from "@/i18n/PageOverlay";
import { translatorFor } from "@/i18n/server";
import { pageMetadata } from "@/lib/metadata";

type PageProps = { params: Promise<{ locale: string }> };

export async function generateMetadata({ params }: PageProps): Promise<Metadata> {
  const t = await translatorFor(params);
  return pageMetadata({
    locale: t.locale,
    title: t("Production coordination room"),
    description: t(
      "Restricted production-only coordination room for approved department heads and room admins.",
    ),
    path: "/production-room",
    ogEyebrow: t("Production operations"),
  });
}

function ProductionRoomPageSkeleton() {
  return (
    <div className="min-h-screen bg-ink-950 pb-20 text-text-primary">
      <div className="shell mt-6 space-y-6">
        <div className="grid grid-cols-2 gap-3.5 lg:grid-cols-4">
          {Array.from({ length: 4 }).map((_, index) => (
            <div
              key={index}
              className="h-28 animate-pulse rounded-panel border border-line-strong/40 bg-ink-900/60"
            />
          ))}
        </div>
        <div className="h-40 animate-pulse rounded-panel border border-line-strong/40 bg-ink-900/60" />
        <div className="h-96 animate-pulse rounded-panel border border-line-strong/40 bg-ink-900/60" />
      </div>
    </div>
  );
}

export default async function ProductionRoomPage({ params }: PageProps) {
  const t = await translatorFor(params);

  return (
    <PageOverlay locale={t.locale} route="production-room">
      <div className="operations-route">
        <OperatorOpener
          t={t}
          eyebrow="Production · coordination"
          title="Production Coordination Room"
          lead="One restricted room for production blockers, material and machine issues, quality and schedule changes, dispatch dependencies, decisions, and action items."
          meta={[
            { label: "Access", value: "Explicit membership" },
            { label: "Scope", value: "Production only" },
            { label: "History", value: "Immutable audit" },
          ]}
        />
        <Suspense fallback={<ProductionRoomPageSkeleton />}>
          <ProductionRoom />
        </Suspense>
      </div>
    </PageOverlay>
  );
}

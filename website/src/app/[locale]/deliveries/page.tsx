import type { Metadata } from "next";
import { Suspense } from "react";
import { DeliveriesDashboard } from "@/components/deliveries/DeliveriesDashboard";
import { RouteSchema } from "@/components/seo/RouteSchema";
import { pageMetadata } from "@/lib/metadata";
import { translatorFor } from "@/i18n/server";

type PageProps = { params: Promise<{ locale: string }> };

export async function generateMetadata({ params }: PageProps): Promise<Metadata> {
  const t = await translatorFor(params);
  return pageMetadata({
    locale: t.locale,
    title: t("Delivery Balance Tracker"),
    description: t(
      "Track confirmed proforma PIs to dispatch: part deliveries, outstanding balances and delivery-note history from the same Excel Sync records as Reports.",
    ),
    path: "/deliveries",
    ogEyebrow: t("Operations & ERP"),
  });
}

function DeliveriesPageSkeleton() {
  return (
    <div className="min-h-screen bg-ink-950 pb-20 text-text-primary">
      <div className="border-b border-line-strong/60 bg-ink-950/80 py-5">
        <div className="shell flex items-center justify-between">
          <div className="space-y-2">
            <div className="h-4 w-32 rounded bg-line-strong/40 animate-pulse" />
            <div className="h-7 w-64 rounded bg-line-strong/60 animate-pulse" />
          </div>
          <div className="h-9 w-24 rounded-xl bg-line-strong/50 animate-pulse" />
        </div>
      </div>
      <div className="shell mt-6 space-y-6">
        <div className="grid grid-cols-2 gap-3.5 sm:grid-cols-3 lg:grid-cols-6">
          {Array.from({ length: 6 }).map((_, index) => (
            <div
              key={index}
              className="h-28 animate-pulse rounded-2xl border border-line-strong/40 bg-ink-900/60"
            />
          ))}
        </div>
        <div className="grid grid-cols-2 gap-3.5 lg:grid-cols-4">
          {Array.from({ length: 4 }).map((_, index) => (
            <div
              key={index}
              className="h-16 animate-pulse rounded-2xl border border-line-strong/40 bg-ink-900/60"
            />
          ))}
        </div>
        <div className="h-32 animate-pulse rounded-2xl border border-line-strong/40 bg-ink-900/60" />
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
          {Array.from({ length: 4 }).map((_, index) => (
            <div
              key={index}
              className="h-60 animate-pulse rounded-2xl border border-line-strong/40 bg-ink-900/60"
            />
          ))}
        </div>
        <div className="h-96 animate-pulse rounded-2xl border border-line-strong/40 bg-ink-900/60" />
      </div>
    </div>
  );
}

export default async function DeliveriesPage({ params }: PageProps) {
  const { locale } = await params;
  const t = await translatorFor(params);

  return (
    <>
      <RouteSchema
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        locale={locale as any}
        path="/deliveries"
        title={t("Delivery Balance Tracker")}
        description={t(
          "Deliveries tracker over Supabase public.records and public.delivery_entries: confirmed PIs, part loads and balances.",
        )}
      />
      <Suspense fallback={<DeliveriesPageSkeleton />}>
        <DeliveriesDashboard />
      </Suspense>
    </>
  );
}

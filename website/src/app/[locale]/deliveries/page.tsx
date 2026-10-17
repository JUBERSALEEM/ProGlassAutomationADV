import type { Metadata } from "next";
import { Suspense } from "react";
import { DeliveriesDashboard } from "@/components/deliveries/DeliveriesDashboard";
import { OperatorOpener } from "@/components/operations/OperatorOpener";
import { RouteSchema } from "@/components/seo/RouteSchema";
import { pageMetadata } from "@/lib/metadata";
import { translatorFor } from "@/i18n/server";
import { PageOverlay } from "@/i18n/PageOverlay";

type PageProps = { params: Promise<{ locale: string }> };

export async function generateMetadata({ params }: PageProps): Promise<Metadata> {
  const t = await translatorFor(params);
  return pageMetadata({
    locale: t.locale,
    title: t("Delivery balance tracker"),
    description: t(
      "Track confirmed proformas to dispatch: part deliveries, outstanding balances and the delivery-note history behind each one.",
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
        <div className="dense-card-grid grid grid-cols-3 gap-3.5 sm:grid-cols-6">
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
        <div className="dense-card-grid grid grid-cols-2 gap-4 sm:grid-cols-4">
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
    <PageOverlay locale={t.locale} route="deliveries">
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
      <OperatorOpener
        t={t}
        eyebrow="Dispatch · balance"
        title="Deliveries Tracker"
        lead="What has left the yard and what is still owed, per document and per line. Part deliveries accumulate against the same proforma, and the balance is computed from the ledger rather than from anything typed onto this page."
        meta={[
          { label: "Grouped by", value: "Confirmed proforma" },
          { label: "Ledger", value: "Per movement, per PI" },
          { label: "Reads", value: "Published records" },
        ]}
      />
      <Suspense fallback={<DeliveriesPageSkeleton />}>
        <DeliveriesDashboard />
      </Suspense>
    </>
    </PageOverlay>
  );
}

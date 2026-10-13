import type { Metadata } from "next";
import { Suspense } from "react";
import { RecordsDashboard } from "@/components/records/RecordsDashboard";
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
    title: t("Operational records dashboard"),
    description: t(
      "Search every proforma published from Excel Sync, with the job orders and deliveries raised against it, and export the filtered set.",
    ),
    path: "/reports",
    ogEyebrow: t("Operations & ERP"),
  });
}

function ReportsPageSkeleton() {
  return (
    <div className="min-h-screen bg-ink-950 pb-20 text-text-primary">
      <div className="border-b border-line-strong/60 bg-ink-950/80 py-5">
        <div className="shell flex items-center justify-between">
          <div className="space-y-2">
            <div className="h-4 w-32 bg-line-strong/40 rounded animate-pulse" />
            <div className="h-7 w-64 bg-line-strong/60 rounded animate-pulse" />
          </div>
          <div className="h-9 w-24 bg-line-strong/50 rounded-xl animate-pulse" />
        </div>
      </div>
      <div className="shell mt-6 space-y-6">
        <div className="grid grid-cols-2 gap-3.5 sm:grid-cols-3 lg:grid-cols-6">
          {Array.from({ length: 6 }).map((_, i) => (
            <div key={i} className="h-28 rounded-2xl bg-ink-900/60 border border-line-strong/40 animate-pulse" />
          ))}
        </div>
        <div className="h-32 rounded-2xl bg-ink-900/60 border border-line-strong/40 animate-pulse" />
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
          {Array.from({ length: 4 }).map((_, i) => (
            <div key={i} className="h-60 rounded-2xl bg-ink-900/60 border border-line-strong/40 animate-pulse" />
          ))}
        </div>
        <div className="h-96 rounded-2xl bg-ink-900/60 border border-line-strong/40 animate-pulse" />
      </div>
    </div>
  );
}

export default async function ReportsPage({ params }: PageProps) {
  const { locale } = await params;
  const t = await translatorFor(params);

  return (
    <PageOverlay locale={t.locale} route="reports">
    <>
      <RouteSchema
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        locale={locale as any}
        path="/reports"
        title={t("Operational Reports & Records Dashboard")}
        description={t(
          "Live operational reports dashboard for ProGlass Automation, consuming Supabase public.records.",
        )}
      />
      <OperatorOpener
        t={t}
        eyebrow="Live operations · read-only"
        title="Records & Operations Dashboard"
        lead="Every proforma that Excel Sync has published, indexed and searchable, with the job orders and deliveries raised against it. This page reads the published table; the desktop application remains the only thing that writes to it."
        meta={[
          { label: "Source", value: "Supabase · public.records" },
          { label: "Written by", value: "ProGlass Records Sync" },
          { label: "Signed in as", value: "Operator" },
        ]}
      />
      <Suspense fallback={<ReportsPageSkeleton />}>
        <RecordsDashboard />
      </Suspense>
    </>
    </PageOverlay>
  );
}

import type { Metadata } from "next";
import { Suspense } from "react";
import { JobOrdersDashboard } from "@/components/job-orders/JobOrdersDashboard";
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
    title: t("Job orders release queue"),
    description: t(
      "Confirmed proformas waiting to be released into job orders, with order quantities and customer details published from Excel Sync.",
    ),
    path: "/job-orders",
    ogEyebrow: t("Operations & ERP"),
  });
}

function JobOrdersPageSkeleton() {
  return (
    <div className="min-h-screen bg-ink-950 pb-20 text-text-primary">
      <div className="border-b border-line-strong/60 bg-ink-950/80 py-5">
        <div className="shell flex items-center justify-between">
          <div className="space-y-2">
            <div className="h-4 w-32 animate-pulse rounded bg-line-strong/40" />
            <div className="h-7 w-64 animate-pulse rounded bg-line-strong/60" />
          </div>
          <div className="h-9 w-24 animate-pulse rounded-xl bg-line-strong/50" />
        </div>
      </div>
      <div className="shell mt-6 space-y-6">
        <div className="dense-card-grid dense-fixed-grid grid grid-cols-3 gap-3.5 sm:grid-cols-5">
          {Array.from({ length: 5 }).map((_, index) => (
            <div key={index} className="h-28 animate-pulse rounded-2xl border border-line-strong/40 bg-ink-900/60" />
          ))}
        </div>
        <div className="h-24 animate-pulse rounded-2xl border border-line-strong/40 bg-ink-900/60" />
        <div className="h-96 animate-pulse rounded-2xl border border-line-strong/40 bg-ink-900/60" />
      </div>
    </div>
  );
}

export default async function JobOrdersPage({ params }: PageProps) {
  const { locale } = await params;
  const t = await translatorFor(params);

  return (
    <PageOverlay locale={t.locale} route="job-orders">
    <div className="operations-route">
      <RouteSchema
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        locale={locale as any}
        path="/job-orders"
        title={t("Job Orders from Confirmed Proformas")}
        description={t(
          "Confirmed proformas enter the Job Order hand-off queue; unconfirmed proformas and later statuses are excluded.",
        )}
      />
      <OperatorOpener
        t={t}
        eyebrow="Production · release queue"
        title="Job Orders"
        lead="Confirmed proformas waiting to be released, and the job orders already running. Releasing a proforma is the one write this surface performs, and it goes through the same window the desktop application uses."
        meta={[
          { label: "Admitted", value: "Proformas marked Confirmed" },
          { label: "Excluded", value: "Every later status" },
          { label: "Writes", value: "Release only" },
        ]}
      />
      <Suspense fallback={<JobOrdersPageSkeleton />}>
        <JobOrdersDashboard />
      </Suspense>
    </div>
    </PageOverlay>
  );
}

import type { Metadata } from "next";
import { OptimizationWorkspace } from "@/components/optimization/OptimizationWorkspace";
import { PageOverlay } from "@/i18n/PageOverlay";
import { optimizationWorkspaceKeys } from "@/data/optimizationWorkspace";
import { translatorFor } from "@/i18n/server";
import { pageMetadata } from "@/lib/metadata";

type PageProps = { params: Promise<{ locale: string }> };

export async function generateMetadata({ params }: PageProps): Promise<Metadata> {
  const t = await translatorFor(params);
  return pageMetadata({
    locale: t.locale,
    title: t("Glass cutting optimization workspace"),
    description: t("Explore the eight-tab ProGlass Optimization workflow: jobs, sizes, cut list, stocks, settings, summary, 2D layout and report. Interactive browser preview uses sample data and a separate illustrative optimizer."),
    path: "/optimization",
    ogEyebrow: t("Production module"),
  });
}

export default async function OptimizationPage({ params }: PageProps) {
  const t = await translatorFor(params);
  return (
    <PageOverlay locale={t.locale} route="optimization" extraKeys={optimizationWorkspaceKeys}>
      <OptimizationWorkspace />
    </PageOverlay>
  );
}

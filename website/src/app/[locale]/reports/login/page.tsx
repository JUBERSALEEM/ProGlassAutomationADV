import type { Metadata } from "next";
import { Suspense } from "react";
import { ReportsAuthForm } from "@/components/reports/ReportsAuthForm";
import { RouteSchema } from "@/components/seo/RouteSchema";
import { pageMetadata } from "@/lib/metadata";
import { translatorFor } from "@/i18n/server";

type PageProps = { params: Promise<{ locale: string }> };

export async function generateMetadata({ params }: PageProps): Promise<Metadata> {
  const t = await translatorFor(params);
  return pageMetadata({
    locale: t.locale,
    title: t("Reports Access & Sign In"),
    description: t(
      "Operator sign in and authentication gateway for ProGlass Automation operational reports and Excel Sync records.",
    ),
    path: "/reports/login",
    ogEyebrow: t("Operations Portal"),
  });
}

export default async function ReportsLoginPage({ params }: PageProps) {
  const { locale } = await params;
  const t = await translatorFor(params);

  return (
    <main className="min-h-screen bg-ink-950 flex flex-col justify-center py-16 px-4 sm:px-6 lg:px-8">
      <RouteSchema
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        locale={locale as any}
        path="/reports/login"
        title={t("Reports Access & Sign In")}
        description={t("Operator sign in for ProGlass Automation operational reports.")}
      />
      <Suspense fallback={<div className="h-96 w-full max-w-md mx-auto rounded-panel bg-ink-900/60 animate-pulse" />}>
        <ReportsAuthForm />
      </Suspense>
    </main>
  );
}

import type { Metadata } from "next";
import { Suspense } from "react";
import { ReportsAuthForm } from "@/components/reports/ReportsAuthForm";
import { RouteSchema } from "@/components/seo/RouteSchema";
import { pageMetadata } from "@/lib/metadata";
import { translatorFor } from "@/i18n/server";
import { PageOverlay } from "@/i18n/PageOverlay";

type PageProps = { params: Promise<{ locale: string }> };

export async function generateMetadata({ params }: PageProps): Promise<Metadata> {
  const t = await translatorFor(params);
  return pageMetadata({
    locale: t.locale,
    title: t("Sign in or create an account"),
    description: t("Sign in or create a ProGlass Automation customer account to manage your inquiries."),
    path: "/reports/login",
    ogEyebrow: t("Customer Portal"),
  });
}

export default async function ReportsLoginPage({ params }: PageProps) {
  const { locale } = await params;
  const t = await translatorFor(params);

  return (
    <PageOverlay locale={t.locale} route="reports/login">
    <main className="min-h-screen bg-ink-950 flex flex-col justify-center py-16 px-4 sm:px-6 lg:px-8">
      <RouteSchema
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        locale={locale as any}
        path="/reports/login"
        title={t("Sign in or create an account")}
        description={t("Customer access for ProGlass Automation inquiries and account services.")}
      />
      <Suspense fallback={<div className="h-96 w-full max-w-md mx-auto rounded-panel bg-ink-900/60 animate-pulse" />}>
        <ReportsAuthForm defaultRedirect="/inquiries/chat" />
      </Suspense>
    </main>
    </PageOverlay>
  );
}

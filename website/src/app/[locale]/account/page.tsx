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
    title: t("Customer Account, Inquiry Chat & Sign In"),
    description: t(
      "Sign in or create a secure ProGlass Automation website account to submit inquiries, follow current status, manage documents, and chat on your own inquiry.",
    ),
    path: "/account",
    ogEyebrow: t("Customer portal"),
  });
}

export default async function AccountPage({ params }: PageProps) {
  const { locale } = await params;
  const t = await translatorFor(params);

  return (
    <PageOverlay locale={t.locale} route="reports/login">
      <main className="min-h-screen bg-ink-950 flex flex-col justify-center py-16 px-4 sm:px-6 lg:px-8">
        <RouteSchema
          // eslint-disable-next-line @typescript-eslint/no-explicit-any
          locale={locale as any}
          path="/account"
          title={t("Customer Account, Inquiry Chat & Sign In")}
          description={t("Secure customer access for ProGlass Automation inquiries, status, documents, and inquiry chat.")}
        />
        <Suspense fallback={<div className="h-96 w-full max-w-md mx-auto rounded-panel bg-ink-900/60 animate-pulse" />}>
          <ReportsAuthForm variant="account" defaultRedirect="/inquiries/chat" />
        </Suspense>
      </main>
    </PageOverlay>
  );
}

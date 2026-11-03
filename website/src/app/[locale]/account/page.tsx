import type { Metadata } from "next";
import { Suspense } from "react";
import { CustomerAccountWorkspace } from "@/components/account/CustomerAccountWorkspace";
import { RouteSchema } from "@/components/seo/RouteSchema";
import { pageMetadata } from "@/lib/metadata";
import { translatorFor } from "@/i18n/server";
import { PageOverlay } from "@/i18n/PageOverlay";

type PageProps = { params: Promise<{ locale: string }> };

export async function generateMetadata({ params }: PageProps): Promise<Metadata> {
  const t = await translatorFor(params);
  return pageMetadata({
    locale: t.locale,
    title: t("Customer Account, Profile, Inquiry Chat & Sign In"),
    description: t(
      "Use one saved ProGlass Automation sign-in to manage your chat profile icon, submit inquiries, follow status, manage documents, and chat securely.",
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
      <main className="min-h-screen bg-ink-950 text-text-primary">
        <RouteSchema
          // eslint-disable-next-line @typescript-eslint/no-explicit-any
          locale={locale as any}
          path="/account"
          title={t("Customer Account, Profile, Inquiry Chat & Sign In")}
          description={t("Secure customer access and chat profile settings for ProGlass Automation inquiries, status, documents, and chat.")}
        />
        <Suspense fallback={<div className="shell h-96 w-full py-12"><div className="h-full rounded-[28px] bg-ink-900/60 animate-pulse" /></div>}>
          <CustomerAccountWorkspace />
        </Suspense>
      </main>
    </PageOverlay>
  );
}

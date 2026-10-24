import type { Metadata } from "next";
import { Suspense } from "react";
import { PageOverlay } from "@/i18n/PageOverlay";
import { translatorFor } from "@/i18n/server";
import { pageMetadata } from "@/lib/metadata";
import { CustomerProformaList } from "@/components/account/CustomerProformas";

type PageProps = { params: Promise<{ locale: string }> };

export async function generateMetadata({ params }: PageProps): Promise<Metadata> {
  const values = await params;
  const t = await translatorFor(Promise.resolve({ locale: values.locale }));
  return pageMetadata({
    locale: t.locale,
    title: t("Proforma invoices"),
    description: t("The proforma invoices shared with your account."),
    path: "/account/proformas",
    ogEyebrow: t("Your documents"),
  });
}

export default async function AccountProformasPage({ params }: PageProps) {
  const values = await params;
  const t = await translatorFor(Promise.resolve({ locale: values.locale }));
  return (
    <PageOverlay locale={t.locale} route="account/proformas">
      <main className="min-h-screen bg-ink-950 pb-20 text-text-primary">
        <div className="shell py-8">
          <Suspense fallback={<p className="p-10 text-xs text-text-muted">Loading your documents</p>}>
            <CustomerProformaList />
          </Suspense>
        </div>
      </main>
    </PageOverlay>
  );
}

import type { Metadata } from "next";
import { CustomerAuthPage } from "@/components/account/CustomerAuthPage";
import { PageOverlay } from "@/i18n/PageOverlay";
import { translatorFor } from "@/i18n/server";
import { pageMetadata } from "@/lib/metadata";
import type { AuthSearchParams } from "@/components/reports/ReportsAuthForm";

type PageProps = {
  params: Promise<{ locale: string }>;
  searchParams: Promise<AuthSearchParams>;
};

export async function generateMetadata({ params }: PageProps): Promise<Metadata> {
  const t = await translatorFor(params);
  return pageMetadata({
    locale: t.locale,
    title: t("Sign in to your ProGlass account"),
    description: t("Sign in securely to manage your ProGlass inquiries, documents, and private chats."),
    path: "/login",
    ogEyebrow: t("Customer account"),
  });
}

export default async function LoginPage({ params, searchParams }: PageProps) {
  const [t, query] = await Promise.all([translatorFor(params), searchParams]);
  return (
    <PageOverlay locale={t.locale} route="account">
      <CustomerAuthPage mode="password" searchParams={query} />
    </PageOverlay>
  );
}

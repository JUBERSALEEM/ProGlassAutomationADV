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
    title: t("Create a ProGlass account"),
    description: t("Create your secure ProGlass account to submit inquiries, follow projects, manage documents, and chat."),
    path: "/signup",
    ogEyebrow: t("Customer account"),
  });
}

export default async function SignupPage({ params, searchParams }: PageProps) {
  const [t, query] = await Promise.all([translatorFor(params), searchParams]);
  return (
    <PageOverlay locale={t.locale} route="account">
      <CustomerAuthPage mode="otp" searchParams={query} />
    </PageOverlay>
  );
}

import type { Metadata } from "next";
import { CustomerInquiryChatWorkspace } from "@/components/inquiries/CustomerInquiryChatWorkspace";
import { PageOverlay } from "@/i18n/PageOverlay";
import { pageMetadata } from "@/lib/metadata";
import { translatorFor } from "@/i18n/server";

type Props = {
  params: Promise<{ locale: string }>;
  searchParams: Promise<{ inquiry?: string | string[] }>;
};

export async function generateMetadata({ params }: Props): Promise<Metadata> {
  const t = await translatorFor(params);
  return pageMetadata({
    locale: t.locale,
    title: t("My inquiries & chat"),
    description: t(
      "Follow the current status of your ProGlass Automation inquiries and chat securely on your selected inquiry reference.",
    ),
    path: "/inquiries/chat",
    ogEyebrow: t("Customer account"),
  });
}

export default async function Page({ params, searchParams }: Props) {
  const t = await translatorFor(params);
  const query = await searchParams;
  const initialInquiryId = Array.isArray(query.inquiry) ? query.inquiry[0] : query.inquiry;

  return (
    <PageOverlay locale={t.locale} route="inquiries/chat">
      <CustomerInquiryChatWorkspace initialInquiryId={initialInquiryId} />
    </PageOverlay>
  );
}

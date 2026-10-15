import type { Metadata } from "next";
import { InquiryDetail } from "@/components/inquiries/InquiryDetail";
import { PageOverlay } from "@/i18n/PageOverlay";
import { pageMetadata } from "@/lib/metadata";
import { translatorFor } from "@/i18n/server";

type PageProps = { params: Promise<{ locale: string; id: string }> };

export async function generateMetadata({ params }: PageProps): Promise<Metadata> {
  const values = await params;
  const t = await translatorFor(Promise.resolve({ locale: values.locale }));
  return pageMetadata({
    locale: t.locale,
    title: t("Inquiry details"),
    description: t("Restricted inquiry metadata and private published attachments."),
    path: `/inquiries/${values.id}`,
    ogEyebrow: t("Internal operations"),
  });
}

export default async function InquiryDetailPage({ params }: PageProps) {
  const values = await params;
  const t = await translatorFor(Promise.resolve({ locale: values.locale }));
  return <PageOverlay locale={t.locale} route="inquiries/detail"><InquiryDetail inquiryId={values.id} /></PageOverlay>;
}

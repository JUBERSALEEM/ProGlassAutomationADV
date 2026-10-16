import type { Metadata } from "next";
import { CustomerInquiryPortal } from "@/components/inquiries/CustomerInquiryPortal";
import { PageOverlay } from "@/i18n/PageOverlay";
import { pageMetadata } from "@/lib/metadata";
import { translatorFor } from "@/i18n/server";

type Props = { params: Promise<{ locale: string }> };
export async function generateMetadata({ params }: Props): Promise<Metadata> { const t = await translatorFor(params); return pageMetadata({ locale: t.locale, title: t("Submit an inquiry"), description: t("Securely submit project requirements and private inquiry documents."), path: "/inquiries/submit", ogEyebrow: t("Customer inquiry") }); }
export default async function Page({ params }: Props) { const t = await translatorFor(params); return <PageOverlay locale={t.locale} route="inquiries/submit"><CustomerInquiryPortal /></PageOverlay>; }

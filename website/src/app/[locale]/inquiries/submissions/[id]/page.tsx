import type { Metadata } from "next";
import { CustomerInquiryDetail } from "@/components/inquiries/CustomerInquiryDetail";
import { PageOverlay } from "@/i18n/PageOverlay";
import { pageMetadata } from "@/lib/metadata";
import { translatorFor } from "@/i18n/server";

type Props={params:Promise<{locale:string;id:string}>};
export async function generateMetadata({params}:Props):Promise<Metadata>{const t=await translatorFor(params);return pageMetadata({locale:t.locale,title:t("Inquiry submission"),description:t("Track your secure website inquiry and submitted documents."),path:"/inquiries/submissions",ogEyebrow:t("Customer inquiry")});}
export default async function Page({params}:Props){const value=await params;const t=await translatorFor(Promise.resolve({locale:value.locale}));return <PageOverlay locale={t.locale} route="inquiries/submissions/[id]"><CustomerInquiryDetail id={value.id}/></PageOverlay>;}

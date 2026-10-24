import type { Metadata } from "next";
import { CustomerSubmissionRegister } from "@/components/inquiries/CustomerSubmissionRegister";
import { PageOverlay } from "@/i18n/PageOverlay";
import { pageMetadata } from "@/lib/metadata";
import { translatorFor } from "@/i18n/server";
type Props={params:Promise<{locale:string}>;searchParams:Promise<Record<string,string|string[]|undefined>>};
export async function generateMetadata({params}:Props):Promise<Metadata>{const t=await translatorFor(params);return pageMetadata({locale:t.locale,title:t("Customer submissions"),description:t("Restricted internal review queue for website-originated customer inquiries."),path:"/inquiries/customer-submissions",ogEyebrow:t("Internal operations")});}
export default async function Page({params,searchParams}:Props){
  const t=await translatorFor(params);
  const query=await searchParams;
  const first=(value:string|string[]|undefined)=>Array.isArray(value)?value[0]:value;
  // The filter can be linked to, so the inbox can send an operator straight to
  // the four submissions that are still waiting. Everything unreadable is
  // dropped by the register's own parser.
  return (
    <PageOverlay locale={t.locale} route="inquiries">
      <CustomerSubmissionRegister
        initialStatus={first(query.status) ?? ""}
        initialSearch={first(query.q) ?? ""}
        initialPage={Number(first(query.page)) || 1}
      />
    </PageOverlay>
  );
}

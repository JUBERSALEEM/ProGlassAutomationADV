import { CustomerSubmissionReview } from "@/components/inquiries/CustomerSubmissionReview";
import { PageOverlay } from "@/i18n/PageOverlay";
import { translatorFor } from "@/i18n/server";
type Props={params:Promise<{locale:string;id:string}>};
export default async function Page({params}:Props){const values=await params;const t=await translatorFor(Promise.resolve({locale:values.locale}));return <PageOverlay locale={t.locale} route="inquiries"><CustomerSubmissionReview id={values.id}/></PageOverlay>}

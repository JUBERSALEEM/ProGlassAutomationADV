import type { Metadata } from "next";
import { CustomerInquiryPortal } from "@/components/inquiries/CustomerInquiryPortal";
import { PageOverlay } from "@/i18n/PageOverlay";
import { pageMetadata } from "@/lib/metadata";
import { translatorFor } from "@/i18n/server";

type Props = { params: Promise<{ locale: string }> };

export async function generateMetadata({ params }: Props): Promise<Metadata> {
  const t = await translatorFor(params);
  return pageMetadata({
    locale: t.locale,
    title: t("Submit an inquiry with a proforma invoice"),
    description: t("Send a requirement with the proforma invoice details. A coordinator confirms it before the document is issued."),
    path: "/inquiries/submit/proforma",
    ogEyebrow: t("Customer inquiry"),
  });
}

/**
 * The same portal, opened on the one inquiry type that is held for review.
 *
 * A proforma invoice is a priced document, so the submission is created in
 * `under_review` instead of `submitted` and the desktop inbox does not receive
 * it until a coordinator accepts it on the website.
 */
export default async function Page({ params }: Props) {
  const t = await translatorFor(params);
  return (
    <PageOverlay locale={t.locale} route="inquiries/submit">
      <CustomerInquiryPortal intent="proforma_invoice" />
    </PageOverlay>
  );
}

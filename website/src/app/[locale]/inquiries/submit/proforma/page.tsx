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
    title: t("Request a proforma invoice"),
    description: t("Send your company and project details to the ProGlass inquiry inbox so the team can prepare a proforma invoice in the desktop workflow."),
    path: "/inquiries/submit/proforma",
    ogEyebrow: t("Customer inquiry"),
  });
}

/**
 * The same portal, opened in PI-request mode.
 *
 * A request is finalized as a normal submitted intake item and marked
 * `approval_required` for the desktop workflow. It reaches the WPF inbox via
 * the configured Supabase-to-Windows bridge; the website does not create or
 * price the invoice itself.
 */
export default async function Page({ params }: Props) {
  const t = await translatorFor(params);
  return (
    <PageOverlay locale={t.locale} route="inquiries/submit">
      <CustomerInquiryPortal intent="proforma_invoice" />
    </PageOverlay>
  );
}

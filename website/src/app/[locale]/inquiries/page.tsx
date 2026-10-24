import type { Metadata } from "next";
import { InquiryWelcome } from "@/components/inquiries/InquiryWelcome";
import { PageOverlay } from "@/i18n/PageOverlay";
import { pageMetadata } from "@/lib/metadata";
import { translatorFor } from "@/i18n/server";

type PageProps = { params: Promise<{ locale: string }> };

export async function generateMetadata({ params }: PageProps): Promise<Metadata> {
  const t = await translatorFor(params);
  return pageMetadata({
    locale: t.locale,
    title: t("Inquiries"),
    description: t(
      "Submit a private inquiry or a proforma invoice request to ProGlass, then follow the review, the messages and the documents shared with your account.",
    ),
    path: "/inquiries",
    ogEyebrow: t("Customer inquiries"),
  });
}

/**
 * The public inquiry page.
 *
 * This is a welcome screen, not a register: send an inquiry, send one with a
 * proforma invoice, sign in or create an account. The internal register is a
 * different page (`/inquiries/register`) and a different audience; the two used
 * to share a URL, which made a private queue look public.
 */
export default async function InquiriesPage({ params }: PageProps) {
  const t = await translatorFor(params);
  return (
    <PageOverlay locale={t.locale} route="inquiries">
      <InquiryWelcome />
    </PageOverlay>
  );
}

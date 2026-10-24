import type { Metadata } from "next";
import { Suspense } from "react";
import { InquiryLanding } from "@/components/inquiries/InquiryLanding";
import { PageOverlay } from "@/i18n/PageOverlay";
import { pageMetadata } from "@/lib/metadata";
import { translatorFor } from "@/i18n/server";

type PageProps = { params: Promise<{ locale: string }> };

export async function generateMetadata({ params }: PageProps): Promise<Metadata> {
  const t = await translatorFor(params);
  return pageMetadata({
    locale: t.locale,
    title: t("Inquiry register"),
    description: t("Restricted internal register of desktop-published inquiries and the website submission queue."),
    path: "/inquiries/register",
    ogEyebrow: t("Internal operations"),
  });
}

/**
 * The staff register, at its own URL.
 *
 * `InquiryLanding` decides by role: `inquiry_auditor` and `inquiry_admin` get
 * the register, an estimator is pointed at the assignment workspace, the bridge
 * account is told it is a sync identity, and a customer is sent back to the
 * public inquiry page rather than to a refusal screen.
 */
export default async function InquiryRegisterPage({ params }: PageProps) {
  const t = await translatorFor(params);
  return (
    <PageOverlay locale={t.locale} route="inquiries/register">
      <Suspense
        fallback={
          <div className="grid min-h-[60vh] place-items-center bg-ink-950 text-xs text-text-muted">
            Loading the inquiry register…
          </div>
        }
      >
        <InquiryLanding />
      </Suspense>
    </PageOverlay>
  );
}

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
    title: t("Inquiries"),
    description: t(
      "Submit a private customer inquiry, follow its review and read the documents ProGlass shares with your account.",
    ),
    path: "/inquiries",
    ogEyebrow: t("Inquiries"),
  });
}

/**
 * One URL, two audiences.
 *
 * A customer sees their own inquiry surfaces; a staff role sees the register.
 * The decision is made in the browser by `InquiryLanding` from the account's
 * single role, but it is only a router: every register read still goes through
 * an API that checks the role and through Row Level Security that returns a
 * customer nothing but their own submissions.
 */
export default async function InquiriesPage({ params }: PageProps) {
  const t = await translatorFor(params);
  return (
    <PageOverlay locale={t.locale} route="inquiries">
      <Suspense
        fallback={
          <div className="grid min-h-[60vh] place-items-center bg-ink-950 text-xs text-text-muted">
            Loading inquiry access…
          </div>
        }
      >
        <InquiryLanding />
      </Suspense>
    </PageOverlay>
  );
}

import type { Metadata } from "next";
import { Suspense } from "react";
import { PageOverlay } from "@/i18n/PageOverlay";
import { translatorFor } from "@/i18n/server";
import { pageMetadata } from "@/lib/metadata";
import { ProformaWorkspace } from "@/components/inquiries/ProformaWorkspace";

type PageProps = { params: Promise<{ locale: string; id: string }> };

export async function generateMetadata({ params }: PageProps): Promise<Metadata> {
  const values = await params;
  const t = await translatorFor(Promise.resolve({ locale: values.locale }));
  return pageMetadata({
    locale: t.locale,
    title: t("Proforma invoice"),
    description: t("A priced proforma specification for one published inquiry."),
    path: `/inquiries/${values.id}/proforma`,
    ogEyebrow: t("Internal operations"),
  });
}

export default async function InquiryProformaPage({ params }: PageProps) {
  const values = await params;
  const t = await translatorFor(Promise.resolve({ locale: values.locale }));
  return (
    <PageOverlay locale={t.locale} route="inquiries/proforma">
      <Suspense
        fallback={
          <div className="grid min-h-[60vh] place-items-center bg-ink-950 text-xs text-text-muted">
            Loading the proforma workspace
          </div>
        }
      >
        <ProformaWorkspace inquiryId={values.id} />
      </Suspense>
    </PageOverlay>
  );
}

import type { Metadata } from "next";
import { Suspense } from "react";
import { InquiriesDashboard } from "@/components/inquiries/InquiriesDashboard";
import { PageOverlay } from "@/i18n/PageOverlay";
import { pageMetadata } from "@/lib/metadata";
import { translatorFor } from "@/i18n/server";

type PageProps = { params: Promise<{ locale: string }> };

export async function generateMetadata({ params }: PageProps): Promise<Metadata> {
  const t = await translatorFor(params);
  return pageMetadata({
    locale: t.locale,
    title: t("Inquiry document register"),
    description: t("Restricted internal register of desktop-published inquiries and their private inquiry attachments."),
    path: "/inquiries",
    ogEyebrow: t("Internal operations"),
  });
}

export default async function InquiriesPage({ params }: PageProps) {
  const t = await translatorFor(params);
  return <PageOverlay locale={t.locale} route="inquiries"><Suspense fallback={<div className="grid min-h-[60vh] place-items-center bg-ink-950 text-xs text-text-muted">Loading inquiry register…</div>}><InquiriesDashboard /></Suspense></PageOverlay>;
}

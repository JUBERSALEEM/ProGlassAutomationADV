import type { Metadata } from "next";
import { ProductionRoomWorkspace } from "@/components/production/ProductionRoomWorkspace";
import { PageOverlay } from "@/i18n/PageOverlay";
import { pageMetadata } from "@/lib/metadata";
import { translatorFor } from "@/i18n/server";

type Props = {
  params: Promise<{ locale: string }>;
};

export async function generateMetadata({ params }: Props): Promise<Metadata> {
  const t = await translatorFor(params);
  const metadata = pageMetadata({
    locale: t.locale,
    title: t("Production Coordination Room"),
    description: t(
      "One internal room for the ProGlass shop floor: blockers, material shortages, machine issues, quality, schedule changes, dispatch dependencies, pinned decisions and action items.",
    ),
    path: "/production",
    ogEyebrow: t("Internal"),
  });
  // An internal room that names customers, machines and dates has no business
  // in an index, and it is not linked from any public navigation.
  return { ...metadata, robots: { index: false, follow: false } };
}

export default async function Page({ params }: Props) {
  const t = await translatorFor(params);

  return (
    <PageOverlay locale={t.locale} route="production">
      <ProductionRoomWorkspace />
    </PageOverlay>
  );
}

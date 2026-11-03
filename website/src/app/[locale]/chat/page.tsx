import type { Metadata } from "next";
import { GlobalChatWorkspace } from "@/components/chat/GlobalChatWorkspace";
import { PageOverlay } from "@/i18n/PageOverlay";
import { pageMetadata } from "@/lib/metadata";
import { translatorFor } from "@/i18n/server";

type Props = {
  params: Promise<{ locale: string }>;
};

export async function generateMetadata({ params }: Props): Promise<Metadata> {
  const t = await translatorFor(params);
  return pageMetadata({
    locale: t.locale,
    title: t("Community Chat"),
    description: t(
      "Chat live with other signed-in ProGlass Automation users in one community room. Keep private project details in My inquiries & chat.",
    ),
    path: "/chat",
    ogEyebrow: t("Community"),
  });
}

export default async function Page({ params }: Props) {
  const t = await translatorFor(params);

  return (
    <PageOverlay locale={t.locale} route="chat">
      <GlobalChatWorkspace />
    </PageOverlay>
  );
}

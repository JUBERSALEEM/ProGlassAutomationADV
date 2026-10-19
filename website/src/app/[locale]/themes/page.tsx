import type { Metadata } from "next";
import { Container, Section } from "@/components/ui/Container";
import { PageHero } from "@/components/layout/PageHero";
import { translatorFor } from "@/i18n/server";
import { PageOverlay } from "@/i18n/PageOverlay";
import { ThemeCenter } from "@/components/theme/ThemeCenter";
import { pageMetadata } from "@/lib/metadata";

type PageProps = { params: Promise<{ locale: string }> };

export async function generateMetadata({ params }: PageProps): Promise<Metadata> {
  const t = await translatorFor(params);
  return pageMetadata({
    locale: t.locale,
    title: t("Theme Center — eight visual systems"),
    description: t("Explore eight ProGlass visual systems, each available in Light and Dark, with responsive surfaces and accessible motion."),
    path: "/themes",
    keywords: ["ProGlass themes", "Liquid Glass", "Claymorphism", "Glassmorphism", "WPF theme system"],
    ogEyebrow: t("Appearance studio"),
  });
}

export default async function ThemesPage({ params }: PageProps) {
  const t = await translatorFor(params);
  return (
    <PageOverlay locale={t.locale} route="themes">
      <>
        <PageHero
          t={t}
          eyebrow={t("Theme Center")}
          title={t("Eight visual systems. One consistent ProGlass experience.")}
          lead={t("Explore the material language of ProGlass Automation. Each family changes surfaces, depth, borders and emphasis while preserving the same product structure and readable content.")}
          banner="product"
          breadcrumb={[
            { label: t("Home"), href: "/" },
            { label: t("Theme Center"), href: "/themes" },
          ]}
          meta={[
            { label: t("Visual families"), value: "8" },
            { label: t("Appearance modes"), value: t("Light + Dark") },
            { label: t("Interaction"), value: t("Live preview") },
            { label: t("Design rule"), value: t("Accessible by default") },
          ]}
        />
        <Section aria-labelledby="theme-center-heading">
          <Container>
            <h2 id="theme-center-heading" className="sr-only">{t("Select a visual theme")}</h2>
            <ThemeCenter />
          </Container>
        </Section>
      </>
    </PageOverlay>
  );
}

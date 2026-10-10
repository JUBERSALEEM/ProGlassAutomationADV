import type { Metadata } from "next";
import { LegalPage } from "@/components/layout/LegalPage";
import { RouteSchema } from "@/components/seo/RouteSchema";
import { pageMetadata } from "@/lib/metadata";
import { site } from "@/lib/site";
import { translatorFor } from "@/i18n/server";
import { PageOverlay } from "@/i18n/PageOverlay";

type PageProps = { params: Promise<{ locale: string }> };

export async function generateMetadata({ params }: PageProps): Promise<Metadata> {
  const t = await translatorFor(params);
  return pageMetadata({
  locale: t.locale,
  title: t("Terms"),
  description: t("Terms covering this website and the use of the ProGlass Automation desktop application, including what the website does and does not claim about the product."),
  path: "/legal/terms",
  });
}

export default async function TermsPage({ params }: PageProps) {
  const t = await translatorFor(params);
  const locale = t.locale;
  return (
    <PageOverlay locale={t.locale} route="legal/terms">
    <>
    {/* Structured data for this route — see lib/schema.ts. */}
    <RouteSchema
      locale={locale}
      path="/legal/terms"
      title={t("Terms")}
      description={t("Terms covering this website and the use of the ProGlass Automation desktop application, including what the website does and does not claim about the product.")}
    />
    <LegalPage
      t={t}
        eyebrow={t("Terms")}
        title={t("Terms of use")}
        lead={t("Short, and written to be read. These terms cover this website and set the expectations that apply to the desktop application.")}
        updated="September 2026"
        banner="licence"
        sections={[
          {
            heading: "This website",
            paragraphs: [
              "This website is provided as a description of the ProGlass Automation desktop application. It is documentation and product information, not software-as-a-service, and nothing on it is offered to you as an online service.",
              "You may read it, link to it and quote it with attribution. You may not present it, or the application's assets, as your own work.",
            ],
          },
          {
            heading: "Accuracy of what is published here",
            paragraphs: [
              "Every functional claim on this website is written to describe behaviour that exists in the application at the version stated in the footer. Where an implementation detail is unknown to the author, the page says so rather than filling the gap with a plausible statement.",
              "Screenshots: no photograph of a running build is published on this site yet. Where a capture is missing the page says so on the card and shows an interactive preview of that module instead — built from the application's own navigation labels, field names and calculations, filled with clearly labelled sample data. No generated image on this site is presented as a screenshot of the product.",
            ],
          },
          {
            heading: "What is not promised",
            paragraphs: [
              "Nothing on this website promises a business outcome. There is no claim of a percentage saving, a speed improvement or a productivity multiple, because no such measurement exists in the product's own repository.",
              "The optimization engine reports what it produced for the cutting list it was given — sheets used, utilization, offcut, unplaced pieces and the gap to an area-only lower bound. What that is worth to your business depends on your stock, your cutting list and your margins.",
            ],
          },
          {
            heading: "The desktop application",
            paragraphs: [
              "Use of the application is governed by the licence you are issued, not by this page. Licensing is covered in more detail under the Licence page, which describes how activation, editions and expiry behave.",
              "The application is proprietary software. It is licensed, not sold, and the licence grants the rights written on it — the edition, the number of devices, and the feature set.",
            ],
          },
          {
            heading: "Your data is your responsibility to hold",
            paragraphs: [
              "The application stores your business data locally and provides backup and restore tooling. Keeping a copy of that data is your responsibility, as it is with any desktop system. The application writes backup sets to a local folder and takes a pre-restore snapshot, but nothing is uploaded on your behalf.",
            ],
          },
          {
            heading: "Trademarks",
            paragraphs: [
              "Machine and software names that appear in exports — for example cutting-machine formats — are the trademarks of their respective owners and are used only to identify the file formats the application can write.",
            ],
          },
          {
            heading: "Contact",
            paragraphs: [t.template`Questions about these terms can be sent to ${site.developerEmail}.`],
          },
        ]}
      />
  </>
    </PageOverlay>
  );
}

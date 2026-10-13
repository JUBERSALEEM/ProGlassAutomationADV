import type { Metadata } from "next";
import { LegalPage } from "@/components/layout/LegalPage";
import { footerIconFor } from "@/data/footerIcons";
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
  title: t("Privacy"),
  description: t("How this website and the ProGlass desktop app handle data: no analytics, no advertising cookies, and what the demo request form does with what you type."),
  path: "/legal/privacy",
  });
}

export default async function PrivacyPage({ params }: PageProps) {
  const t = await translatorFor(params);
  const locale = t.locale;
  return (
    <PageOverlay locale={t.locale} route="legal/privacy">
    <>
    {/* Structured data for this route — see lib/schema.ts. */}
    <RouteSchema
      locale={locale}
      path="/legal/privacy"
      title={t("Privacy")}
      description={t("How this website and the ProGlass desktop app handle data: no analytics, no advertising cookies, and what the demo request form does with what you type.")}
    />
    <LegalPage
      t={t}
      glyph={footerIconFor("/legal/privacy")!}
        eyebrow={t("Privacy")}
        title={t("Privacy")}
        lead={t("Two different things are covered here, and they behave very differently. This website is a set of static pages. The application is desktop software that keeps your business data on your own machine.")}
        updated="September 2026"
        banner="licence"
        sections={[
          {
            heading: "This website",
            paragraphs: [
              "This website does not run analytics, advertising tags, session recording or third-party trackers. It does not set advertising or profiling cookies. Typefaces are self-hosted, so visiting a page does not call a font service.",
              "The only information this website receives is what you actively type into the demo request form.",
            ],
          },
          {
            heading: "The demo request form",
            paragraphs: [
              "The form asks for your name, company, email address, an optional phone number, your country and your business type, plus a message. That information is used to reply to you and for nothing else.",
              "The website does not store form submissions in a database. When a mail transport is configured the request is relayed to the developer's inbox; when none is configured the form says so plainly and asks you to email instead, rather than pretending the message was delivered.",
            ],
          },
          {
            heading: "The desktop application",
            paragraphs: [
              "ProGlass Automation runs on your computer. Your business data — customers, quotations, stock, deliveries, invoices — is stored in SQLite database files in your own Windows user profile. It is not uploaded anywhere.",
              "Backups are written to a local Backups folder. Nothing is uploaded automatically.",
            ],
            list: [
              "Application programming interface keys you configure for an AI provider are encrypted with Windows user-level protection (DPAPI) — never stored in plain text, and never sent anywhere except the provider you chose.",
              "The activity log records meaningful actions only: create, edit, delete, print and login. It does not record keystrokes or mouse movement.",
              "Technical logs contain error details and context, not invoice contents or personal data.",
              "The AI assistant sends only your question plus the business summary you explicitly allow. Left unconfigured, it answers from the on-device model and your local data.",
            ],
          },
          {
            heading: "Your rights over your own data",
            paragraphs: [
              "Because the data lives on your machine in standard SQLite files, you have direct access to it: you can inspect it, copy it, back it up or export it through the application's own report screens. There is no request process to go through, and no party holding your data on your behalf.",
            ],
          },
          {
            heading: "Contact",
            paragraphs: [
              t.template`Questions about this page, or a request to delete a demo request you sent, can be sent to ${site.developerEmail}.`,
            ],
          },
        ]}
      />
  </>
    </PageOverlay>
  );
}

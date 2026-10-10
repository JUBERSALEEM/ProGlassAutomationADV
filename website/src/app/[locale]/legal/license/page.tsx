import type { Metadata } from "next";
import { LegalPage } from "@/components/layout/LegalPage";
import { ButtonLink } from "@/components/ui/Button";
import { ArrowRight } from "@/components/ui/Button";
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
  title: t("Licence"),
  description: t("How a ProGlass licence works: the licence file, offline activation, editions, expiry, grace mode and renewal, with no signing material exposed."),
  path: "/legal/license",
  });
}

export default async function LicensePage({ params }: PageProps) {
  const t = await translatorFor(params);
  const locale = t.locale;
  return (
    <PageOverlay locale={t.locale} route="legal/license">
    <>
    {/* Structured data for this route — see lib/schema.ts. */}
    <RouteSchema
      locale={locale}
      path="/legal/license"
      title={t("Licence")}
      description={t("How a ProGlass licence works: the licence file, offline activation, editions, expiry, grace mode and renewal, with no signing material exposed.")}
    />
    <LegalPage
      t={t}
        eyebrow={t("License")}
        title={t("How licensing works")}
        lead={t("ProGlass Automation is licensed with a signed licence file that is activated offline. There is no licence server that has to be reachable, and no account to create. This page describes what a customer needs to know.")}
        updated="September 2026"
        sections={[
          {
            heading: "The licence file",
            paragraphs: [
              "A licence is a small file your vendor sends you, named license.lic and placed in the application's data folder. It carries your customer code, your company name as it should appear in the application, the edition you bought, the issue and expiry dates, how many devices it may be activated on, and the feature list it grants.",
              "The file is signed. The application verifies that signature on every launch, so a tampered or edited licence is rejected. Only the matching public key is shipped in the application — no signing material exists in the product, and this website does not describe the signing process.",
            ],
          },
          {
            heading: "Activating it",
            paragraphs: ["There are three ways to activate, all of which end with the licence file in the right place:"],
            list: [
              "First run — the welcome screen offers ACTIVATE LICENSE FILE…, where you select the file.",
              "After first run — Settings → Data → Database Health has the same button and the same flow.",
              "Manually — place the file at the documented location under your AppData folder and restart. The application picks it up on next launch.",
            ],
          },
          {
            heading: "The trial",
            paragraphs: [
              "The application issues an automatic 30-day evaluation trial the first time it runs on a machine. The trial is machine-scoped and recorded in a tamper-evident ledger, so it cannot be renewed by deleting files, and a real licence always takes precedence over the trial state.",
              "During an active trial every feature set is available, including the production stages and the cutting-optimization engine.",
            ],
          },
          {
            heading: "Editions",
            paragraphs: [
              "The edition sets what the licence grants. Trial is a 30-day evaluation with all features; Basic is a single user with inquiries and the proforma wizard; Business covers up to five users and the full document chain without production optimization; Factory is unlimited users with the cutting-optimization engine and the production stages; Enterprise adds multi-site deployment and custom integration work.",
              "Feature gating is applied by the licence's feature list, not by the edition name alone. The gating map is the same one described on the pricing page, and it is checked when a module is opened.",
            ],
          },
          {
            heading: "Expiry, grace and renewal",
            paragraphs: [
              "Licences are issued for a fixed term, typically a year. Within fourteen days of expiry the application shows a renewal notice. Within the final seven days it continues to run in grace mode: you keep full read access, and writes and exports are disabled.",
              "If a licence expires past its grace period, the application degrades to read-only rather than locking you away from your records — you can still open and export your data. Renewal is a new licence file dropped in the same place; the application records both events in its audit log.",
            ],
          },
          {
            heading: "Device activations",
            paragraphs: [
              "A portable licence counts distinct devices through a local ledger and refuses to run once the maximum is reached. A machine-bound licence is tied to one device using characteristics of that machine.",
              "The fingerprint exists to prevent accidental copying between machines — for example, restoring an image onto a second PC — rather than to make copying impossible, and the application says as much in its own documentation.",
            ],
          },
          {
            heading: "The fonts on this website",
            paragraphs: [
              "The application's licence and this website's assets are separate things, and the typefaces are the part of the second one with an upstream author. Every face used here is self-hosted — opening a page never calls a font service — and each is under the SIL Open Font License 1.1, which permits commercial use and redistribution as part of a website:",
              "Inter (Rasmus Andersson), JetBrains Mono (JetBrains), Source Serif 4 (Frank Grießhammer, Adobe) and IBM Plex Sans (IBM). Those four are served from this origin, which is what lets the privacy page state plainly that visiting a page does not call a font service. The theme and colour panel offers them under “bundled with this site”, and beneath them a second group — Segoe UI, the system face, Arial, Verdana, Tahoma, Trebuchet MS, Georgia, Times New Roman, Courier New and Consolas — which are not downloaded at all: the browser resolves them from the visitor's own computer, so they carry no licence obligation here and none is claimed.",
            ],
          },
          {
            heading: "What this page does not cover",
            paragraphs: [
              "Nothing here describes how a licence is produced, how it is signed, or what the internal validation performs. That information is deliberately not published. If you need a licence reissued or a device slot freed, contact your vendor.",
            ],
          },
        ]}
        banner="licence"
        footer={
          <div className="flex flex-wrap gap-3">
            <ButtonLink href="/pricing" size="md" trailing={<ArrowRight />}>
          {t("Editions and gating")}
        </ButtonLink>
            <ButtonLink href="/contact" variant="secondary" size="md">
          {t("Request pricing")}
        </ButtonLink>
            <ButtonLink href={`mailto:${site.developerEmail}`} variant="ghost" size="md" external>
              {site.developerEmail}
            </ButtonLink>
          </div>
        }
      />
  </>
    </PageOverlay>
  );
}

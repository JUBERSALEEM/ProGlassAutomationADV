import type { Metadata } from "next";
import { LocaleLink as Link } from "@/components/ui/LocaleLink";
import { Container, Section } from "@/components/ui/Container";
import { PageHero } from "@/components/layout/PageHero";
import { ArrowRight, ButtonLink } from "@/components/ui/Button";
import { Badge, IconChip } from "@/components/ui/Badge";
import { groupIcons } from "@/data/groupIcons";
import { navIcons } from "@/data/navIcons";
import { Reveal } from "@/components/ui/Reveal";
import { SectionHeading } from "@/components/ui/SectionHeading";
import { guides, resourceCategories } from "@/data/resources";
import { allPosts } from "@/lib/posts";
import { RouteSchema } from "@/components/seo/RouteSchema";
import { pageMetadata } from "@/lib/metadata";
import { translatorFor } from "@/i18n/server";
import { PageOverlay } from "@/i18n/PageOverlay";

type PageProps = { params: Promise<{ locale: string }> };

export async function generateMetadata({ params }: PageProps): Promise<Metadata> {
  const t = await translatorFor(params);
  return pageMetadata({
  locale: t.locale,
  title: t("Guides to glass estimation"),
  description: t("Guides for glass businesses: the vocabulary of an order, why a cutting plan must be cuttable, and how stock accuracy works when movements are recorded."),
  path: "/resources",
  keywords: ["glass estimation guide", "glass cutting optimization guide", "glass industry terminology", "glass stock management guide"],
  ogEyebrow: t("Resources"),
  });
}

export default async function ResourcesPage({ params }: PageProps) {
  const t = await translatorFor(params);
  const locale = t.locale;
  const posts = allPosts();
  return (
    <PageOverlay locale={t.locale} route="resources">
    <>
    {/* Structured data for this route — see lib/schema.ts. */}
    <RouteSchema
      locale={locale}
      path="/resources"
      title={t("Guides to glass estimation")}
      description={t("Guides for glass businesses: the vocabulary of an order, why a cutting plan must be cuttable, and how stock accuracy works when movements are recorded.")}
    />
      <PageHero
      t={t}
        eyebrow={t("Resources")}
        title={t("Reference material for glass workflows.")}
        lead={t("Written for the person who has to make the quotation add up: how the vocabulary works, why a cutting plan has to be cuttable, what a job order should inherit, and how stock stays auditable. No product pitch in the body text — these are explanations first.")}
        banner="writing"
        breadcrumb={[
          { label: t("Home"), href: "/" },
          { label: t("Resources"), href: "/resources" },
        ]}
        meta={[
          { label: t("Guides"), value: `${guides.length}` },
          { label: t("Notes"), value: t.template`${posts.length} in the blog` },
          { label: t("Categories"), value: `${resourceCategories.length}` },
          { label: t("Written from"), value: t("The product's own vocabulary") },
        ]}
        actions={
          <>
            <ButtonLink href="#guides" size="lg" trailing={<ArrowRight />}>
          {t("Read the guides")}
        </ButtonLink>
            <ButtonLink href="/blog" variant="secondary" size="lg">
          {t("Visit the blog")}
        </ButtonLink>
          </>
        }
      />

      {/* ── Categories ───────────────────────────────────────────── */}
      <Section aria-labelledby="categories-heading">
        <Container>
          <SectionHeading
            id="categories-heading"
            eyebrow={t("Topics")}
            title={t("What this section covers.")}
            lead={t("The topic list is deliberately narrow. It follows the questions a glass business actually has to answer, not the keywords a software website usually chases.")}
          />
          <Reveal delay={80} className="mt-8 flex flex-wrap gap-2">
            {resourceCategories.map((category) => (
              <span
                key={category}
                className="rounded-full border border-line-strong/70 bg-ink-900/60 px-3.5 py-1.5 text-xs font-medium text-text-muted"
              >
                {t(category)}
              </span>
            ))}
          </Reveal>
        </Container>
      </Section>

      {/* ── Guides ───────────────────────────────────────────────── */}
      <Section id="guides" tone="raised" aria-labelledby="guides-heading">
        <Container>
          <SectionHeading
            id="guides-heading"
            eyebrow={t("Guides")}
            title={t("Four places a glass order goes wrong.")}
            lead={t("Each guide explains the concept, then says what the application does about it — separately, so you can read the explanation whether or not you ever use the software.")}
          />

          <div className="mt-12 space-y-8">
            {guides.map((guide, index) => (
              /* The glyph names the guide's subject in the same hand the rail
                 names its modules: vocabulary is a book, the cutting guide is
                 optimization's arrow, quoting is the sales document, and stock
                 is the inventory stack. */
              <Reveal key={guide.id} id={guide.id} delay={index * 60} as="article" className="surface-raised p-6 sm:p-8">
                <IconChip
                  path={
                    guide.id === "cutting-optimization-basics"
                      ? navIcons.Optimization!
                      : guide.id === "quote-versus-order"
                        ? groupIcons.Sales!
                        : guide.id === "stock-accuracy"
                          ? groupIcons.Inventory!
                          : navIcons.Resources!
                  }
                  className="mb-5"
                />
                <header className="flex flex-wrap items-start justify-between gap-4">
                  <div className="max-w-3xl">
                    <div className="flex flex-wrap items-center gap-3">
                      <Badge tone="accent">{t(guide.category)}</Badge>
                      <span className="font-mono text-2xs uppercase tracking-[0.14em] text-text-faint font-medium">
                        {t("{minutes} min read", { minutes: guide.readingMinutes })}
                      </span>
                    </div>
                    <h3 className="display-3 mt-4">{t(guide.title)}</h3>
                    <p className="mt-3 text-sm leading-relaxed text-text-muted">{t(guide.summary)}</p>
                  </div>
                </header>

                <div className="mt-7 grid gap-6 border-t border-line/60 pt-7 lg:grid-cols-2">
                  {guide.sections.map((section) => (
                    <div key={section.heading}>
                      <h4 className="font-mono text-2xs uppercase tracking-[0.16em] text-brand-300">
                        {t(section.heading)}
                      </h4>
                      <p className="mt-2.5 text-sm leading-relaxed text-text-muted">{t(section.body)}</p>
                      {section.list ? (
                        <ul className="mt-3 space-y-2">
                          {section.list.map((item) => (
                            <li key={item} className="flex gap-2.5 text-sm leading-relaxed text-text-faint">
                              <span aria-hidden="true" className="mt-2 h-1 w-1 shrink-0 rounded-full bg-brand-400/70" />
                              {item}
                            </li>
                          ))}
                        </ul>
                      ) : null}
                    </div>
                  ))}
                </div>
              </Reveal>
            ))}
          </div>
        </Container>
      </Section>

      {/* ── Blog teaser ──────────────────────────────────────────── */}
      <Section aria-labelledby="notes-heading">
        <Container>
          <div className="flex flex-col gap-8 lg:flex-row lg:items-end lg:justify-between">
            <SectionHeading
              id="notes-heading"
              eyebrow={t("Notes")}
              title={t("Longer pieces on how the software is built.")}
              lead={t("Short essays on the engineering decisions behind the product, written so they are useful even if you use something else.")}
            />
            <ButtonLink href="/blog" variant="secondary" size="lg" trailing={<ArrowRight />} className="shrink-0">
          {t("All notes")}
        </ButtonLink>
          </div>

          <ul className="mt-10 grid gap-5 lg:grid-cols-3">
            {posts.map((post, index) => (
              <Reveal key={post.slug} as="li" delay={index * 60}>
                <Link
                  href={`/blog/${post.slug}`}
                  className="surface group flex h-full flex-col p-6 transition-colors duration-300 hover:border-brand-400/40"
                >
                  <div className="flex items-center gap-3">
                    <Badge>{t(post.category)}</Badge>
                    <span className="font-mono text-2xs uppercase tracking-[0.14em] text-text-faint font-medium">
                      {t("{minutes} min", { minutes: post.readingMinutes })}
                    </span>
                  </div>
                  <h3 className="mt-4 text-base font-semibold text-text-primary transition-colors group-hover:text-brand-200">
                    {t(post.title)}
                  </h3>
                  <p className="mt-2 flex-1 text-sm leading-relaxed text-text-muted">{t(post.summary)}</p>
                  <span className="link-underline mt-4 text-xs">{t("Read the note")}<ArrowRight />
                  </span>
                </Link>
              </Reveal>
            ))}
          </ul>
        </Container>
      </Section>

      {/* ── CTA ──────────────────────────────────────────────────── */}
      <Section tone="raised" aria-label={t("Request a demo")}>
        <Container>
          <Reveal>
            <div className="glass-primary flex flex-col gap-6 p-8 sm:flex-row sm:items-center sm:justify-between sm:p-10">
              <div>
                <h2 className="display-2">{t("See how these ideas look on a real job.")}</h2>
                <p className="mt-3 max-w-xl text-sm leading-relaxed text-text-muted">{t("The guides explain the concepts; a demonstration shows the application applying them to your glass.")}</p>
              </div>
              <div className="flex shrink-0 flex-wrap gap-3">
                <ButtonLink href="/contact" size="lg" trailing={<ArrowRight />}>
          {t("Request a Demo")}
        </ButtonLink>
                <ButtonLink href="/optimization" variant="secondary" size="lg">
          {t("The engine")}
        </ButtonLink>
              </div>
            </div>
          </Reveal>
        </Container>
      </Section>
    </>
    </PageOverlay>
  );
}

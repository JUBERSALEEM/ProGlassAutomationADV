import type { Metadata } from "next";
import Link from "next/link";
import { Container, Section } from "@/components/ui/Container";
import { PageHero } from "@/components/layout/PageHero";
import { ArrowRight, ButtonLink } from "@/components/ui/Button";
import { Badge } from "@/components/ui/Badge";
import { Reveal } from "@/components/ui/Reveal";
import { SectionHeading } from "@/components/ui/SectionHeading";
import { guides, posts, resourceCategories } from "@/data/resources";
import { pageMetadata } from "@/lib/metadata";

export const metadata: Metadata = pageMetadata({
  title: "Guides to glass estimation and optimization",
  description:
    "Guides for glass businesses: the vocabulary of an order, why a cutting plan must be cuttable, and how stock accuracy works when movements are recorded.",
  path: "/resources",
  keywords: [
    "glass estimation guide",
    "glass cutting optimization guide",
    "glass industry terminology",
    "glass stock management guide",
  ],
});

export default function ResourcesPage() {
  return (
    <>
      <PageHero
        eyebrow="Resources"
        title="Reference material for glass workflows."
        lead="Written for the person who has to make the quotation add up: how the vocabulary works, why a cutting plan has to be cuttable, what a job order should inherit, and how stock stays auditable. No product pitch in the body text — these are explanations first."
        breadcrumb={[
          { label: "Home", href: "/" },
          { label: "Resources", href: "/resources" },
        ]}
        meta={[
          { label: "Guides", value: `${guides.length}` },
          { label: "Notes", value: `${posts.length} in the blog` },
          { label: "Categories", value: `${resourceCategories.length}` },
          { label: "Written from", value: "The product's own vocabulary" },
        ]}
        actions={
          <>
            <ButtonLink href="#guides" size="lg" trailing={<ArrowRight />}>
              Read the guides
            </ButtonLink>
            <ButtonLink href="/blog" variant="secondary" size="lg">
              Visit the blog
            </ButtonLink>
          </>
        }
      />

      {/* ── Categories ───────────────────────────────────────────── */}
      <Section aria-labelledby="categories-heading">
        <Container>
          <SectionHeading
            id="categories-heading"
            eyebrow="Topics"
            title="What this section covers."
            lead="The topic list is deliberately narrow. It follows the questions a glass business actually has to answer, not the keywords a software website usually chases."
          />
          <Reveal delay={80} className="mt-8 flex flex-wrap gap-2">
            {resourceCategories.map((category) => (
              <span
                key={category}
                className="rounded-full border border-line-strong/70 bg-ink-900/60 px-3.5 py-1.5 text-xs font-medium text-text-muted"
              >
                {category}
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
            eyebrow="Guides"
            title="Four places a glass order goes wrong."
            lead="Each guide explains the concept, then says what the application does about it — separately, so you can read the explanation whether or not you ever use the software."
          />

          <div className="mt-12 space-y-8">
            {guides.map((guide, index) => (
              <Reveal key={guide.id} id={guide.id} delay={index * 60} as="article" className="surface-raised p-6 sm:p-8">
                <header className="flex flex-wrap items-start justify-between gap-4">
                  <div className="max-w-3xl">
                    <div className="flex flex-wrap items-center gap-3">
                      <Badge tone="accent">{guide.category}</Badge>
                      <span className="font-mono text-2xs uppercase tracking-[0.14em] text-text-faint">
                        {guide.readingMinutes} min read
                      </span>
                    </div>
                    <h3 className="mt-4 text-xl font-semibold text-text-primary sm:text-2xl">{guide.title}</h3>
                    <p className="mt-3 text-sm leading-relaxed text-text-muted">{guide.summary}</p>
                  </div>
                </header>

                <div className="mt-7 grid gap-6 border-t border-line/60 pt-7 lg:grid-cols-2">
                  {guide.sections.map((section) => (
                    <div key={section.heading}>
                      <h4 className="font-mono text-2xs uppercase tracking-[0.16em] text-brand-300/85">
                        {section.heading}
                      </h4>
                      <p className="mt-2.5 text-sm leading-relaxed text-text-muted">{section.body}</p>
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
              eyebrow="Notes"
              title="Longer pieces on how the software is built."
              lead="Short essays on the engineering decisions behind the product, written so they are useful even if you use something else."
            />
            <ButtonLink href="/blog" variant="secondary" size="lg" trailing={<ArrowRight />} className="shrink-0">
              All notes
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
                    <Badge>{post.category}</Badge>
                    <span className="font-mono text-2xs uppercase tracking-[0.14em] text-text-faint">
                      {post.readingMinutes} min
                    </span>
                  </div>
                  <h3 className="mt-4 text-base font-semibold text-text-primary transition-colors group-hover:text-brand-200">
                    {post.title}
                  </h3>
                  <p className="mt-2 flex-1 text-sm leading-relaxed text-text-muted">{post.summary}</p>
                  <span className="link-underline mt-4 text-xs">
                    Read the note <ArrowRight />
                  </span>
                </Link>
              </Reveal>
            ))}
          </ul>
        </Container>
      </Section>

      {/* ── CTA ──────────────────────────────────────────────────── */}
      <Section tone="raised" aria-label="Request a demo">
        <Container>
          <Reveal>
            <div className="plate flex flex-col gap-6 p-8 sm:flex-row sm:items-center sm:justify-between sm:p-10">
              <div>
                <h2 className="text-2xl leading-tight sm:text-3xl">See how these ideas look on a real job.</h2>
                <p className="mt-3 max-w-xl text-sm leading-relaxed text-text-muted">
                  The guides explain the concepts; a demonstration shows the application applying them to your glass.
                </p>
              </div>
              <div className="flex shrink-0 flex-wrap gap-3">
                <ButtonLink href="/contact" size="lg" trailing={<ArrowRight />}>
                  Request a Demo
                </ButtonLink>
                <ButtonLink href="/optimization" variant="secondary" size="lg">
                  The engine
                </ButtonLink>
              </div>
            </div>
          </Reveal>
        </Container>
      </Section>
    </>
  );
}

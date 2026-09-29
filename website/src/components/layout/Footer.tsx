import Link from "next/link";
import { nav, site } from "@/lib/site";
import { Logo } from "@/components/brand/Logo";

function Column({ title, links }: { title: string; links: readonly { label: string; href: string }[] }) {
  return (
    <div>
      <h2 className="eyebrow mb-4">{title}</h2>
      <ul className="space-y-2.5">
        {links.map((link) => (
          <li key={link.href}>
            <Link
              href={link.href}
              className="text-sm text-text-muted transition-colors duration-200 hover:text-brand-200"
            >
              {link.label}
            </Link>
          </li>
        ))}
      </ul>
    </div>
  );
}

export function Footer() {
  return (
    <footer className="relative border-t border-line-strong/50 bg-ink-950/80 backdrop-blur-sm">
      {/* Violet band along the top edge of the footer. */}
      <div aria-hidden="true" className="rule-violet absolute inset-x-0 -top-px" />
      <div className="shell py-14 lg:py-16">
        <div className="grid gap-10 lg:grid-cols-[1.4fr_2.6fr]">
          <div className="max-w-sm">
            <Logo size={32} />
            <p className="mt-5 text-sm leading-relaxed text-text-muted">
              Industrial glass-fabrication software for Windows. Estimation, production preparation, stock, dispatch,
              VAT invoicing, statements and reporting — on one machine, in one application.
            </p>
            <dl className="mt-6 space-y-1.5 font-mono text-2xs uppercase tracking-[0.14em] text-text-faint font-medium">
              <div className="flex gap-2">
                <dt>Version</dt>
                <dd className="text-text-muted">{site.version}</dd>
              </div>
              <div className="flex gap-2">
                <dt>Platform</dt>
                <dd className="text-text-muted">{site.platform}</dd>
              </div>
              <div className="flex gap-2">
                <dt>Built with</dt>
                <dd className="text-text-muted">{site.framework}</dd>
              </div>
            </dl>
          </div>

          <div className="grid grid-cols-2 gap-8 sm:grid-cols-4">
            <Column title="Product" links={nav.footer.product} />
            <Column title="Company" links={nav.footer.company} />
            <Column title="Resources" links={nav.footer.resources} />
            <Column title="Legal" links={nav.footer.legal} />
          </div>
        </div>

        <div className="mt-12 flex flex-col gap-4 border-t border-line/60 pt-6 sm:flex-row sm:items-center sm:justify-between">
          <p className="text-xs text-text-faint font-medium">{site.copyrightLine}</p>
          <p className="text-xs text-text-faint font-medium">
            ProGlass Automation is desktop software. There is no cloud account, no subscription dashboard and nothing
            to sign in to on this website.
          </p>
        </div>
      </div>
    </footer>
  );
}

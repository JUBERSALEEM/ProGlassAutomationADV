import { cn } from "@/lib/cn";
import type { Translator } from "@/i18n/core";
import { buildEnvironmentKey, buildStamp, buildStampLine } from "@/lib/buildStamp";

/**
 * The build stamp — the top panel and the footer both print one.
 *
 * Why it exists at all: this site is deployed from one repository to more than
 * one host, and each host keeps its own build of the same branch. Nothing on
 * the page said which of them a visitor was looking at, so two builds minutes
 * apart — a preview and the live site — looked identical while being different
 * code. A date, a time in UTC and the revision settle it in one glance, and the
 * hover text adds the branch and where the build ran.
 *
 * Two shapes, because the two places have different amounts of room:
 *
 *   `BuildStampPill`  a two-line block for the top panel, narrow enough to sit
 *                     beside the settings controls without crowding them;
 *   `BuildStampRow`   a labelled line for the mobile panel and the footer,
 *                     where the revision can be shown in full.
 *
 * Both are pure functions of props: the values are constants baked into the
 * bundle at build time (see `lib/buildStamp.ts`), so the server and the browser
 * render exactly the same text and there is nothing to hydrate differently.
 */

/** The hover text: the instant, the revision, the branch and where it ran. */
function stampTitle(t: Translator): string {
  return [
    t.template`Updated ${buildStamp.line}`,
    buildStamp.revision ? t.template`revision ${buildStamp.revision}` : null,
    buildStamp.ref,
    t(buildEnvironmentKey),
  ]
    .filter(Boolean)
    .join(" · ");
}

export function BuildStampPill({
  t,
  className,
}: {
  /** The translator — the hover text is a sentence like any other. */
  t: Translator;
  className?: string;
}) {
  return (
    <span
      /*
       * `title` rather than a popover: this is a fact a visitor checks once,
       * and the platform tooltip costs nothing to show and nothing to dismiss.
       */
      title={stampTitle(t)}
      className={cn(
        "select-none rounded-card border border-line-strong/60 bg-ink-900/50 px-2 py-1 text-center font-mono text-[0.59375rem] font-semibold leading-[1.35] tracking-tight text-text-muted",
        className,
      )}
    >
      <span className="block whitespace-nowrap">{buildStamp.date}</span>
      <span className="block whitespace-nowrap text-text-faint">{buildStamp.time}</span>
    </span>
  );
}

export function BuildStampRow({
  t,
  className,
}: {
  /** The translator — the label and the hover text are sentences like any other. */
  t: Translator;
  className?: string;
}) {
  return (
    <p
      title={stampTitle(t)}
      className={cn(
        "flex flex-wrap items-baseline gap-x-2 gap-y-1 text-[0.75rem] font-medium text-text-faint",
        className,
      )}
    >
      <span className="mono-label">{t("Updated")}</span>
      <span className="font-mono font-semibold text-text-muted">{buildStampLine}</span>
      <span className="text-text-faint">{t(buildEnvironmentKey)}</span>
    </p>
  );
}

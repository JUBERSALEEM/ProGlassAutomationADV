import { FeaturesShowcase, FEATURES_CAPTIONS } from "./FeaturesShowcase";
import { focusModules, railModules } from "@/lib/featuresModules";
import type { Translator } from "@/i18n/core";

/**
 * The server half of the Features stack.
 *
 * The interactive exhibit is a client component; its copy must not be.
 * `useT` in the browser sees the chrome dictionary only (the provider ships
 * `APP_STRINGS` plus the chrome overlay — page sentences never cross over),
 * so every string the exhibit carries is translated here, on the server
 * with the full route dictionary, and handed over as plain props. A German
 * address renders the German exhibit without waiting for hydration.
 */
export function FeaturesShowcaseServer({ t }: { t: Translator }) {
  const names = [...focusModules, ...railModules].map((m) => t(m.short));
  const captions = Object.fromEntries(
    Object.entries(FEATURES_CAPTIONS).map(([id, sentence]) => [id, t(sentence)]),
  );
  return (
    <FeaturesShowcase
      names={names}
      captions={captions}
      strings={{
        eyebrow: t("The interface, in evidence"),
        headlineFirst: t("Four modules, one workflow"),
        indexLabel: t("Features index"),
        railLabel: t("Compact rail"),
        readModule: t("Read the module"),
      }}
    />
  );
}

import type { Translator } from "@/i18n/core";
import { cn } from "@/lib/cn";

/**
 * The state machine, verbatim.
 *
 * Section §03 claims in a footnote that the status vocabulary comes from the
 * application's own transition code. This strip makes that provable on the
 * page: the two flows below are transcribed from the enum files in the
 * desktop application's Models folder, and each row names the file it came
 * from so a reader with the repository can open it and check. The chips print
 * the enum members, not marketing prose — `InProduction`, not "in progress".
 *
 * Sources (verified 2026-10-12):
 *   Models/JobOrder/Common/JobOrderStatus/JobOrderStatus.cs
 *   Models/DeliveryNote/Common/DeliveryNoteStatus/DeliveryNoteStatus.cs
 */

type Flow = {
  /** Translated row label (the document's name). */
  labelKey: string;
  /** The enum's members, in stored order, printed as the code writes them. */
  members: string[];
  /** The file the row is transcribed from — a real path in this repository. */
  source: string;
  /** One honest caveat from the same file, kept tiny. */
  caveatKey?: string;
};

const FLOWS: Flow[] = [
  {
    labelKey: "Job Order",
    members: ["Draft", "Released", "InProduction", "Completed", "Delivered"],
    source: "Models/JobOrder/Common/JobOrderStatus/JobOrderStatus.cs",
    caveatKey:
      "Draft is the state a Job Order holds between conversion from a Proforma Invoice and release to the floor.",
  },
  {
    labelKey: "Delivery Note",
    members: ["Dispatching", "Dispatched", "Signed", "Received"],
    source: "Models/DeliveryNote/Common/DeliveryNoteStatus/DeliveryNoteStatus.cs",
    caveatKey: "Draft stays in the database as a legacy alias of Dispatching — the code says so in the same file.",
  },
];

export function StatusFlows({ t, className }: { t: Translator; className?: string }) {
  return (
    <div className={cn("surface-raised card-interactive p-5", className)}>
      <p className="eyebrow">{t("The state machine, verbatim")}</p>
      <div className="mt-4 space-y-4">
        {FLOWS.map((flow) => (
          <div key={flow.labelKey}>
            <p className="text-xs font-semibold text-text-secondary">{t(flow.labelKey)}</p>
            <ol className="mt-2 flex flex-wrap items-center gap-x-1.5 gap-y-2">
              {flow.members.map((member, index) => (
                <li key={member} className="flex items-center gap-1.5">
                  {index > 0 ? (
                    <span aria-hidden="true" className="text-brand-400/70 font-mono text-2xs">
                      →
                    </span>
                  ) : null}
                  <span className="rounded-full border border-brand-400/35 bg-brand-500/12 px-2.5 py-1 font-mono text-2xs font-semibold text-brand-100">
                    {member}
                  </span>
                </li>
              ))}
            </ol>
            <p className="mt-2 font-mono text-2xs leading-relaxed text-text-faint">
              {flow.source}
              {flow.caveatKey ? (
                <span className="block text-text-muted/90">{t(flow.caveatKey)}</span>
              ) : null}
            </p>
          </div>
        ))}
      </div>
      <p className="mt-4 text-xs leading-relaxed text-text-muted">
        {t(
          "The application stores these members, colours them, and refuses every transition they do not name. Nothing on this page paraphrases them.",
        )}
      </p>
    </div>
  );
}

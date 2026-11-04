export type CustomerGlassKind = "SGU" | "DGU" | "LAM" | "DGU + LAM";

export type CustomerPiProcessingSpec = {
  id: string;
  specName: string;
  kind: CustomerGlassKind;
  panes: Array<{ colour: string; treatment?: string }>;
  spacerExtra: string;
  interlayerColour?: string;
  panelExtra: string;
  edgeFinish: string;
  fabricationNotes: string;
};

export type CustomerPiProcessingSuggestion = {
  key: string;
  label: string;
  reason: string;
};

/**
 * A customer-safe mirror of the WPF Step 6 auto-charge triggers. It carries
 * processing requirements only: no charge rate, quantity, measured area,
 * amount, wastage percentage, or financial total is calculated or returned.
 */
export function deriveCustomerPiProcessingSuggestions(
  spec: CustomerPiProcessingSpec,
): CustomerPiProcessingSuggestion[] {
  const panel = spec.panelExtra.toLocaleLowerCase();
  const spacer = spec.spacerExtra.toLocaleLowerCase();
  const edge = spec.edgeFinish.toLocaleLowerCase();
  const specDetails = [spec.specName, spec.fabricationNotes, spec.interlayerColour, ...spec.panes.map((pane) => pane.colour), ...spec.panes.map((pane) => pane.treatment)].join(" ").toLocaleLowerCase();
  const allDetails = `${panel} ${spacer} ${edge} ${specDetails}`;
  const isDgu = spec.kind === "DGU" || spec.kind === "DGU + LAM";
  const suggestions: CustomerPiProcessingSuggestion[] = [];
  const add = (key: string, label: string, reason: string) => {
    if (!suggestions.some((item) => item.key === key)) suggestions.push({ key, label, reason });
  };

  if (spacer.includes("u-insert") || specDetails.includes("u-insert")) {
    add("u-insert", "U-Insert fitting", "The Step 4 spacer details request a U-Insert.");
  }
  if (allDetails.includes("overlap")) {
    const areaBased = allDetails.includes("overlap (sqm)");
    add("overlap", areaBased ? "Overlap processing · area basis" : "Overlap processing · perimeter basis", "The Step 4 panel details specify overlap.");
    if (isDgu && !areaBased) {
      add("silicone-bed", "DGU secondary silicone bed", "WPF Step 6 pairs perimeter-based DGU overlap with a separate silicone-bed operation.");
    }
  }
  if (allDetails.includes("argon gas")) {
    add("argon", "Argon-gas cavity fill", "The Step 4 panel details request argon gas.");
  }
  if (allDetails.includes("shutter bonding")) {
    add("shutter-bonding", "Shutter bonding", "The Step 4 panel details request shutter bonding.");
  }
  if (allDetails.includes("fanhole")) {
    add("fan-holes", "Fan / ventilation holes", "The Step 4 panel details include a fanhole.");
  }
  if (allDetails.includes("cutout")) {
    add("cutouts", "Cut-outs", "The Step 4 panel details request a cut-out.");
  }
  if (allDetails.includes("holes")) {
    add("holes", "Drilled holes", "The Step 4 panel details request holes.");
  }
  if (allDetails.includes("polish")) {
    add("polish", "Edge polishing", "The Step 4 edge-finish selection requests polished edges.");
  }
  if (allDetails.includes("mitring") || allDetails.includes("bevel")) {
    add("mitring", "Mitring / beveling", "The Step 4 edge-finish selection requests a beveled edge.");
  }
  if (allDetails.includes("sandblast") || allDetails.includes("frost")) {
    add("sandblasting", "Surface frosting / sandblasting", "The glass colour, interlayer, finish, or fabrication details request a frosted surface.");
  }

  return suggestions;
}

import { describe, expect, it } from "vitest";
import { deriveCustomerPiProcessingSuggestions, type CustomerPiProcessingSpec } from "@/lib/customer-pi-processing";

const spec: CustomerPiProcessingSpec = {
  id: "spec-1",
  specName: "Facade unit",
  kind: "DGU",
  panes: [{ colour: "Clear" }, { colour: "Grey" }],
  spacerExtra: "With U-Insert As Per DWG",
  panelExtra: "With Overlap As Per DWG, With Argon Gas, With Shutter Bonding, With Fanhole, With Cutout And Holes",
  edgeFinish: "Mitring (45° Bevel) · Polished Edges",
  fabricationNotes: "",
};

describe("customer-safe WPF Step 6 processing suggestions", () => {
  it("derives technical operations from selected specification details without charge data", () => {
    const suggestions = deriveCustomerPiProcessingSuggestions(spec);
    const labels = suggestions.map((suggestion) => suggestion.label);
    expect(labels).toEqual(expect.arrayContaining([
      "U-Insert fitting",
      "Overlap processing · perimeter basis",
      "DGU secondary silicone bed",
      "Argon-gas cavity fill",
      "Shutter bonding",
      "Fan / ventilation holes",
      "Cut-outs",
      "Drilled holes",
      "Edge polishing",
      "Mitring / beveling",
    ]));
    expect(new Set(suggestions.map(({ key }) => key)).size).toBe(suggestions.length);
    expect(JSON.stringify(suggestions)).not.toMatch(/\b(rate|amount|price|cost|yield|margin|percent)\b/i);
  });

  it("selects area-basis wording for SQM overlap without exposing an area value", () => {
    const suggestions = deriveCustomerPiProcessingSuggestions({ ...spec, kind: "DGU + LAM", panelExtra: "With Overlap (SQM)" });
    expect(suggestions.map(({ label }) => label)).toContain("Overlap processing · area basis");
    expect(suggestions.map(({ label }) => label)).not.toContain("DGU secondary silicone bed");
  });

});

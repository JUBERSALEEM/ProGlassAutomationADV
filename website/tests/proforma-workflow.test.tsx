import { describe, expect, it } from "vitest";
import { defaultStep4Build, dguLamInterlayerDefaults, dguSpacerSuggestedPrice, emptyStep6, estimateStep4Rate, generatedStep4SpecName, applyAdvancedStep6Suggestions, applyStep4BuildToStep5, totalStep6, workflowLineMetrics, snapshotStep5Ownership, restoreStep5Ownership } from "../src/lib/proforma-workflow";
import { emptyLine, fromSavedLine } from "../src/lib/proforma-draft";
import { validateWorkflowSnapshot } from "../src/lib/proforma-api";

describe("WPF Step 4 build families", () => {
  it("uses the source defaults, generated names, and rate formulas for all four families", () => {
    const sgu = defaultStep4Build("SGU");
    const dgu = defaultStep4Build("DGU");
    const lam = defaultStep4Build("LAM");
    const hybrid = defaultStep4Build("DGU+LAM");
    expect(generatedStep4SpecName(sgu)).toBe("6mm Clear FT Glass");
    expect(generatedStep4SpecName(dgu)).toBe("6mm Clear FT Glass + 12mm ASP + 6mm Clear FT Glass");
    expect(generatedStep4SpecName(lam)).toBe("6mm Clear FT Glass + 1.52mm Clear PVB + 6mm Clear FT Glass");
    expect(generatedStep4SpecName(hybrid)).toBe("6mm HD Grey FT Glass + 12mm ASP + 6mm Clear FT Glass + 1.52mm Clear PVB + 6mm Clear FT Glass");
    expect(estimateStep4Rate(sgu).rate).toBe(108.24);
    expect(estimateStep4Rate(dgu).rate).toBe(176.22);
    expect(estimateStep4Rate(lam).rate).toBe(308.47);
    expect(estimateStep4Rate(hybrid).rate).toBe(344.75);
  });

  it("keeps WPF spacer and hybrid interlayer price defaults tied to their selectors", () => {
    expect(dguSpacerSuggestedPrice("12mm ASP", "Normal Mill Aluminum")).toBe(45);
    expect(dguSpacerSuggestedPrice("12mm ASP", "Black Aluminum")).toBe(50);
    expect(dguSpacerSuggestedPrice("14mm ASP", "Warm Edge (Super Spacer)")).toBe(62.5);
    expect(dguLamInterlayerDefaults("PVB")).toEqual({ buy: 100, sell: 110 });
    expect(dguLamInterlayerDefaults("EVA")).toEqual({ buy: 75, sell: 95 });
    expect(dguLamInterlayerDefaults("SGP")).toEqual({ buy: 0, sell: 0 });
  });

  it("hands the generated spec and rate into Step 5 while retaining manual references/rates and applying the oversize surcharge", () => {
    const build = defaultStep4Build("SGU");
    const generated = { ...emptyLine(1), glassRef: "Old generated name", w1: "2000", h1: "2000", qty: "1", rate: "80", glassRefOwner: "step4" as const, rateOwner: "step4" as const };
    const manual = { ...emptyLine(1), glassRef: "Operator label", w1: "1000", h1: "1000", qty: "1", rate: "123.45", glassRefOwner: "manual" as const, rateOwner: "manual" as const };
    const handoff = applyStep4BuildToStep5([generated, manual], 1, build, "20");
    expect(handoff).toMatchObject({ specName: "6mm Clear FT Glass", baseRate: "108.24", rate: 108.24 });
    expect(handoff.lines.map((line) => [line.glassRef, line.rate, line.glassRefOwner, line.rateOwner])).toEqual([
      ["6mm Clear FT Glass", "129.89", "step4", "step4"],
      ["Operator label", "123.45", "manual", "manual"],
    ]);
  });

  it("matches WPF DGU overlap/argon/U-Insert automation and preserves explicit operator overrides", () => {
    const dgu = defaultStep4Build("DGU");
    dgu.values.panelExtra = "With Argon Gas As Per DWG";
    dgu.values.spacerExtra = "With U-Insert As Per DWG";
    const suggested = applyAdvancedStep6Suggestions(emptyStep6(), { 2: dgu });
    expect(generatedStep4SpecName(dgu)).toContain("Argon Gas As Per DWG");
    expect(suggested.charges.find((row) => row.key === "ARGON_GAS")).toMatchObject({ isActive: true, metricKey: "DGU_CAVITY", groupNos: [2] });
    expect(suggested.charges.find((row) => row.key === "U_INSERT")).toMatchObject({ isActive: true, metricKey: "DGU_CAVITY", groupNos: [2] });
    expect(suggested.wastages.map((row) => row.side)).toEqual(["outer", "inner"]);

    const manual = { ...suggested, manualActiveKeys: ["ARGON_GAS"], charges: suggested.charges.map((row) => row.key === "ARGON_GAS" ? { ...row, isActive: false } : row) };
    const refreshed = applyAdvancedStep6Suggestions(manual, { 2: dgu });
    expect(refreshed.charges.find((row) => row.key === "ARGON_GAS")?.isActive).toBe(false);
  });

  it("uses the special overlap-by-area rule and same-glass laminate polish subtype", () => {
    const dgu = defaultStep4Build("DGU");
    dgu.values.panelExtra = "With Overlap (SQM) As Per DWG";
    const overlap = applyAdvancedStep6Suggestions(emptyStep6(), { 1: dgu });
    expect(overlap.charges.find((row) => row.key === "OVERLAP_SQM")?.isActive).toBe(true);
    expect(overlap.charges.find((row) => row.key === "SILICON_BED")?.isActive).toBe(false);

    const lam = defaultStep4Build("LAM");
    lam.values.polish = "All Side Polish As Per DWG";
    const polish = applyAdvancedStep6Suggestions(emptyStep6(), { 1: lam });
    expect(polish.charges.find((row) => row.key === "POLISH_MATCHED_LAM")).toMatchObject({ isActive: true, polishType: "AllSidePolishAsPerDwg" });
    expect(polish.charges.find((row) => row.key === "POLISH")?.isActive).toBe(false);
  });
});

describe("WPF Step 5 and Step 6 live metrics", () => {
  it("calculates the billable panel areas and perimeter totals with the LAM factor", () => {
    const line = { ...emptyLine(1), w1: "300", h1: "300", w2: "600", h2: "400", qty: "3", rate: "100" };
    expect(workflowLineMetrics(line, "LAM")).toMatchObject({ sqm1: 1.5, sqm2: 1.5, lm1: 7.2, lm2: 12, value: 150 });
  });

  it("totals selected predefined, manual wastage, and custom-charge rows", () => {
    const line = { ...emptyLine(1), w1: "1000", h1: "2000", qty: "2", rate: "50" };
    const step6 = emptyStep6();
    step6.charges = step6.charges.map((row) => row.key === "ARGON_GAS" ? { ...row, isActive: true, metricKey: "SQM1", groupNos: [1], rate: "45" } : row);
    step6.wastages = [{ id: "w1", isActive: true, color: "Clear", groupNos: [1], side: "outer", rate: "2", sheetQuantity: "1" }];
    step6.customCharges = [{ id: "c1", isActive: true, name: "Packing", qty: "3", unit: "Qty", rate: "10" }];
    expect(totalStep6(step6, [line], { 1: defaultStep4Build("SGU") })).toBe(218);
  });

  it("round-trips Step 5 manual versus builder-owned references and rates in the revision snapshot", () => {
    const working = [
      { ...emptyLine(1), glassRef: "Generated DGU", rate: "177.00", glassRefOwner: "step4" as const, rateOwner: "step4" as const },
      { ...emptyLine(1), glassRef: "Operator label", rate: "201.25", glassRefOwner: "manual" as const, rateOwner: "manual" as const },
    ];
    const step5_ownership = snapshotStep5Ownership(working);
    const savedRows = working.map((line, index) => fromSavedLine({ group_no: 1, line_no: index + 1, glass_ref: line.glassRef, w1: 100, h1: 100, w2: 0, h2: 0, qty: 1, approved_rate: Number(line.rate), sqm: 0.5, total_sqm: 0.5, line_value: 0 }));
    const snapshot = {
      step4_groups: { 1: defaultStep4Build("DGU") },
      step6: emptyStep6(),
      step5_ownership,
    };
    const reopened = validateWorkflowSnapshot(JSON.parse(JSON.stringify(snapshot)));
    const restored = restoreStep5Ownership(savedRows, reopened.step5_ownership);
    expect(restored.map((line) => [line.glassRef, line.rate, line.glassRefOwner, line.rateOwner])).toEqual([
      ["Generated DGU", "177", "step4", "step4"],
      ["Operator label", "201.25", "manual", "manual"],
    ]);
    expect(reopened.step4_groups[1]?.code).toBe("DGU");
    expect(reopened.step6.charges).toHaveLength(emptyStep6().charges.length);
  });

  it("rejects malformed or oversized persisted workflow snapshots", () => {
    expect(() => validateWorkflowSnapshot({ step4_groups: { 51: defaultStep4Build("SGU") }, step6: emptyStep6() })).toThrow();
    expect(() => validateWorkflowSnapshot({ step4_groups: {}, step6: { mode: "other", charges: [], wastages: [], customCharges: [] } })).toThrow();
  });
});

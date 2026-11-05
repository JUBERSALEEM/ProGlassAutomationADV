import { priceDraft, type DraftLine } from "@/lib/proforma-draft";
import { billablePanelSqm } from "@/data/pricingRules";
import { applyRatePlan, formatRate, planRates } from "@/lib/proforma-rates";

/** Step 4's four build families, as named by the WPF Step 4 selector. */
export type Step4BuildCode = "SGU" | "DGU" | "LAM" | "DGU+LAM";
export type Step4Build = { code: Step4BuildCode; values: Record<string, string> };
export type Step5Ownership = Record<string, { glassRef: "step4" | "manual"; rate: "step4" | "manual" }>;

export function snapshotStep5Ownership(lines: DraftLine[]): Step5Ownership {
  const counters = new Map<number, number>();
  const result: Step5Ownership = {};
  for (const line of lines) {
    const lineNo = (counters.get(line.groupNo) ?? 0) + 1;
    counters.set(line.groupNo, lineNo);
    result[`${line.groupNo}:${lineNo}`] = { glassRef: line.glassRefOwner ?? "manual", rate: line.rateOwner ?? "manual" };
  }
  return result;
}

export function restoreStep5Ownership(lines: DraftLine[], ownership?: Step5Ownership): DraftLine[] {
  const counters = new Map<number, number>();
  return lines.map((line) => {
    const lineNo = (counters.get(line.groupNo) ?? 0) + 1;
    counters.set(line.groupNo, lineNo);
    const marker = ownership?.[`${line.groupNo}:${lineNo}`];
    return marker ? { ...line, glassRefOwner: marker.glassRef, rateOwner: marker.rate } : line;
  });
}

const step4Defaults: Record<Step4BuildCode, Record<string, string>> = {
  SGU: {
    thickness: "6mm", colour: "Clear", treatment: "FT Glass", basePrice: "46",
    cutting: "15", tempering: "25", yield: "0.85", margin: "15",
    polish: "None (Standard)", panelExtra: "None", customPanelExtra: "", useCustomPanelExtra: "false",
  },
  DGU: {
    outerThickness: "6mm", outerColour: "Clear", outerTreatment: "FT", outerPrice: "46",
    spacerWidth: "12mm ASP", spacerMaterial: "Normal Mill Aluminum", spacerPrice: "45",
    spacerExtra: "None", panelExtra: "None", customPanelExtra: "", useCustomPanelExtra: "false", uInsertPrice: "10",
    innerThickness: "6mm", innerColour: "Clear", innerTreatment: "FT", innerPrice: "46",
    yield: "0.85", margin: "15",
  },
  LAM: {
    outerThickness: "6mm", outerColour: "Clear", outerTreatment: "FT", outerPrice: "46",
    innerThickness: "6mm", innerColour: "Clear", innerTreatment: "FT", innerPrice: "46",
    pvbThickness: "1.52mm", pvbColour: "Clear", pvbPrice: "80", polish: "None",
    outerCutting: "15", innerCutting: "15", outerTempering: "25", innerTempering: "25",
    yield: "0.85", margin: "15",
  },
  "DGU+LAM": {
    outerThickness: "6mm", outerColour: "HD Grey", outerTreatment: "FT", outerPrice: "48",
    spacerThickness: "12mm", spacerPrice: "0", spacerExtra: "None",
    lamOuterThickness: "6mm", lamOuterColour: "Clear", lamOuterTreatment: "FT", lamOuterPrice: "25",
    interlayerType: "PVB", interlayerThickness: "1.52mm", interlayerColour: "Clear", interlayerBuyPrice: "100", interlayerSellPrice: "110",
    lamInnerThickness: "6mm", lamInnerColour: "Clear", lamInnerTreatment: "FT", lamInnerPrice: "25",
    panelExtra: "None", customPanelExtra: "", useCustomPanelExtra: "false", edgePolish: "None",
    outerCutting: "10", outerTempering: "12", lamOuterCutting: "10", lamOuterTempering: "10", lamInnerCutting: "10", lamInnerTempering: "10",
    yield: "0.85", margin: "20",
  },
};

/** WPF panel options. Glass colours stay editable because the desktop sources them from Sheet Store inventory. */
export const step4Options = {
  thickness: ["3mm", "4mm", "5mm", "6mm", "8mm", "10mm", "12mm", "15mm", "19mm"],
  hybridThickness: ["3mm", "4mm", "5mm", "6mm", "8mm", "10mm", "12mm", "16mm", "19mm"],
  treatment: ["FT", "Annealed", "Heat Strengthened", "Heat Soaked"],
  sguTreatment: ["FT Glass", "Annealed", "Heat Strengthened", "Heat Soaked"],
  spacerWidth: ["6mm ASP", "8mm ASP", "10mm ASP", "12mm ASP", "14mm ASP", "16mm ASP", "18mm ASP", "20mm ASP", "22mm ASP", "24mm ASP", "26mm ASP", "28mm ASP", "30mm ASP", "32mm ASP"],
  hybridSpacer: ["6mm", "8mm", "9mm", "12mm", "14mm", "16mm", "18mm", "20mm"],
  spacerMaterial: ["Normal Mill Aluminum", "Black Aluminum", "Warm Edge (Super Spacer)", "Black Warm Edge", "Stainless Steel", "TGI Spacer"],
  spacerExtra: ["None", "With U-Insert", "With U-Insert As Per DWG"],
  panelExtra: ["None", "With Overlap", "With Overlap As Per DWG", "With Overlap As Per Details", "With Overlap (SQM)", "With Overlap (SQM) As Per DWG", "With Fanhole", "With Fanhole As Per DWG", "With Cutout", "With Cutout As Per DWG", "With Holes", "With Holes As Per DWG", "With Cutout And Holes", "With Cutout And Holes As Per DWG", "With Shutter Bonding", "With Shutter Bonding As Per DWG", "With Argon Gas", "With Argon Gas As Per DWG"],
  pvbThickness: ["0.38mm", "0.76mm", "1.14mm", "1.52mm", "2.28mm", "3.04mm"],
  pvbColour: ["Clear", "White", "Frosted", "Milky White", "Black", "Grey", "Bronze"],
  interlayerType: ["PVB", "SGP", "EVA"],
  interlayerThickness: ["0.38mm", "0.76mm", "1.14mm", "1.52mm", "2.28mm"],
  interlayerColour: ["Clear", "Frosted", "Milky White", "Grey", "Bronze", "Blue", "Green"],
  polish: ["None", "All Side Polish", "All Side Polish As Per DWG", "Both Height Side Polish", "Both Height Side Polish As Per DWG", "Both Width Side Polish", "Both Width Side Polish As Per DWG", "Both Height + One Width Side Polish", "Both Height + One Width Side Polish As Per DWG", "One Height + One Width Side Polish", "One Height + One Width Side Polish As Per DWG", "One Height Side Polish", "One Width Side Polish"],
  sguPolish: ["None (Standard)", "Polished Edges", "Beveled Edges", "Mitring (45° Bevel)", "Frosted", "Sandblasted", "With Fanhole", "With Fanhole As Per DWG", "With Holes", "With Holes As Per DWG", "With Holes And Cutouts", "With Holes And Cutouts As Per DWG", "With Cutouts", "With Cutouts As Per DWG", "With All Side Polish", "With Both Height Side Polish", "With Both Height Side Polish As Per DWG"],
} as const;

export function dguSpacerSuggestedPrice(widthText: string, material: string): number {
  const widthMatch = /^(\d+)mm/i.exec(widthText.trim());
  const widthMm = widthMatch ? Number(widthMatch[1]) : 12;
  const base = 37.5 + ((widthMm - 6) / 2) * 2.5;
  const surcharge: Record<string, number> = { "Normal Mill Aluminum": 0, "Black Aluminum": 5, "Warm Edge (Super Spacer)": 15, "Black Warm Edge": 20, "Stainless Steel": 10, "TGI Spacer": 12 };
  return money(base + (surcharge[material] ?? 0));
}

export function dguLamInterlayerDefaults(type: string): { buy: number; sell: number } {
  switch (type.toUpperCase()) {
    case "EVA": return { buy: 75, sell: 95 };
    case "SGP": return { buy: 0, sell: 0 };
    default: return { buy: 100, sell: 110 };
  }
}

export function defaultStep4Build(code: Step4BuildCode = "SGU"): Step4Build {
  return { code, values: { ...step4Defaults[code] } };
}

export function updateStep4Build(build: Step4Build, key: string, value: string): Step4Build {
  return { ...build, values: { ...build.values, [key]: value } };
}

function text(build: Step4Build, key: string, fallback = ""): string {
  return (build.values[key] ?? fallback).trim();
}
function number(build: Step4Build, key: string, fallback = 0): number {
  const parsed = Number(text(build, key));
  return Number.isFinite(parsed) ? parsed : fallback;
}
function has(value: string): boolean { return Boolean(value.trim()) && !value.trim().toLowerCase().startsWith("none"); }
function panel(thickness: string, colour: string, treatment: string): string {
  return `${thickness} ${colour} ${treatment} Glass`.trim();
}
function optionalExtra(value: string): string { return has(value) ? ` ${value}` : ""; }

/** The generated names follow each WPF Step 4 panel's GeneratedSpecName property. */
export function generatedStep4SpecName(build: Step4Build): string {
  switch (build.code) {
    case "SGU": {
      let name = `${text(build, "thickness")} ${text(build, "colour")} ${text(build, "treatment")}`.trim();
      const polish = text(build, "polish");
      if (polish && polish !== "None (Standard)") name += ` ${polish}`;
      const extra = text(build, "useCustomPanelExtra") === "true" ? text(build, "customPanelExtra") : text(build, "panelExtra");
      if (has(extra) && extra.toLowerCase() !== polish.toLowerCase()) name += ` ${extra}`;
      return name;
    }
    case "DGU": {
      const outer = panel(text(build, "outerThickness"), text(build, "outerColour"), text(build, "outerTreatment"));
      const spacer = `${text(build, "spacerWidth")}${optionalExtra(text(build, "spacerExtra"))}`;
      let inner = panel(text(build, "innerThickness"), text(build, "innerColour"), text(build, "innerTreatment"));
      const extra = text(build, "useCustomPanelExtra") === "true" ? text(build, "customPanelExtra") : text(build, "panelExtra");
      if (has(extra)) inner += optionalExtra(extra);
      return `${outer} + ${spacer} + ${inner}`;
    }
    case "LAM": {
      const outer = panel(text(build, "outerThickness"), text(build, "outerColour"), text(build, "outerTreatment"));
      const pvb = `${text(build, "pvbThickness")} ${text(build, "pvbColour")} PVB`;
      const inner = panel(text(build, "innerThickness"), text(build, "innerColour"), text(build, "innerTreatment"));
      const polish = text(build, "polish");
      return `${outer} + ${pvb} + ${inner}${has(polish) ? ` With ${polish}` : ""}`;
    }
    case "DGU+LAM": {
      const outer = panel(text(build, "outerThickness"), text(build, "outerColour"), text(build, "outerTreatment"));
      const spacer = `${text(build, "spacerThickness")} ASP${optionalExtra(text(build, "spacerExtra"))}`;
      const lamOuter = panel(text(build, "lamOuterThickness"), text(build, "lamOuterColour"), text(build, "lamOuterTreatment"));
      const interlayer = `${text(build, "interlayerThickness")} ${text(build, "interlayerColour")} ${text(build, "interlayerType")}`;
      const lamInner = panel(text(build, "lamInnerThickness"), text(build, "lamInnerColour"), text(build, "lamInnerTreatment"));
      const extra = text(build, "useCustomPanelExtra") === "true" ? text(build, "customPanelExtra") : text(build, "panelExtra");
      let name = `${outer} + ${spacer} + ${lamOuter} + ${interlayer} + ${lamInner}${optionalExtra(extra)}`;
      const polish = text(build, "edgePolish");
      if (has(polish)) name += ` With ${polish}`;
      return name;
    }
  }
}

function money(value: number): number { return Math.round((value + Number.EPSILON) * 100) / 100; }
function safeYield(value: number): number { return value <= 0 ? 1 : value; }

/** WPF Step 4 rate formulas, with its non-positive-yield fallback to 1. */
export function estimateStep4Rate(build: Step4Build): { rate: number; warning: string } {
  const yieldValue = safeYield(number(build, "yield", 1));
  const margin = number(build, "margin", 0) / 100;
  let basis = 0;
  let warning = number(build, "yield", 1) <= 0 ? "Yield factor must be greater than 0; the WPF calculation uses 1.0 as fallback." : "";
  switch (build.code) {
    case "SGU":
      basis = number(build, "basePrice") / yieldValue + number(build, "cutting") + number(build, "tempering");
      break;
    case "DGU":
      basis = (number(build, "outerPrice") + number(build, "innerPrice")) / yieldValue + number(build, "spacerPrice");
      break;
    case "LAM":
      basis = (number(build, "outerPrice") + number(build, "innerPrice")) / yieldValue
        + number(build, "pvbPrice") + number(build, "outerCutting") + number(build, "innerCutting")
        + number(build, "outerTempering") + number(build, "innerTempering");
      break;
    case "DGU+LAM": {
      const glass = number(build, "outerPrice") + number(build, "lamOuterPrice") + number(build, "lamInnerPrice");
      const cutting = number(build, "outerCutting") + number(build, "lamOuterCutting") + number(build, "lamInnerCutting");
      const tempering = number(build, "outerTempering") + number(build, "lamOuterTempering") + number(build, "lamInnerTempering");
      basis = glass / yieldValue + cutting + tempering + number(build, "interlayerSellPrice") + number(build, "spacerPrice");
      if (number(build, "interlayerSellPrice") <= 0) warning += `${warning ? " " : ""}No selling price is set for the ${text(build, "interlayerType", "interlayer")}; it is excluded from the rate.`;
      break;
    }
  }
  return { rate: money(basis * (1 + margin)), warning };
}

/** Apply the WPF Step 4 → Step 5 handoff while retaining operator-owned cells. */
export function applyStep4BuildToStep5(lines: DraftLine[], groupNo: number, build: Step4Build, surchargePercent = ""): { lines: DraftLine[]; specName: string; baseRate: string; rate: number } {
  const specName = generatedStep4SpecName(build);
  const estimate = estimateStep4Rate(build);
  const baseRate = formatRate(estimate.rate);
  const handedOff = lines.map((line) => line.groupNo !== groupNo ? line : {
    ...line,
    glassRef: line.glassRefOwner === "manual" && line.glassRef.trim() ? line.glassRef : specName,
    glassRefOwner: line.glassRefOwner === "manual" && line.glassRef.trim() ? "manual" as const : "step4" as const,
    rate: line.rateOwner === "manual" && line.rate.trim() ? line.rate : baseRate,
    rateOwner: line.rateOwner === "manual" && line.rate.trim() ? "manual" as const : "step4" as const,
  });
  const plan = planRates(priceDraft(handedOff), groupNo, baseRate, surchargePercent);
  return { lines: plan ? applyRatePlan(handedOff, plan) : handedOff, specName, baseRate, rate: estimate.rate };
}

export type WorkflowLineMetrics = { lm1: number; lm2: number; sqm1: number; sqm2: number; qty: number; value: number };
export function workflowLineMetrics(line: DraftLine, code: Step4BuildCode | undefined, rateOverride?: number): WorkflowLineMetrics {
  const w1 = Math.trunc(Number(line.w1) || 0), h1 = Math.trunc(Number(line.h1) || 0);
  const w2 = Math.trunc(Number(line.w2) || 0), h2 = Math.trunc(Number(line.h2) || 0);
  const qty = Math.trunc(Number(line.qty) || 0);
  const factor = code === "LAM" ? 4 : 2;
  const area = (w: number, h: number) => billablePanelSqm(w, h);
  const sqm1 = area(w1, h1), sqm2 = area(w2, h2);
  const rate = rateOverride ?? (Number(line.rate) || 0);
  return {
    lm1: w1 > 0 && h1 > 0 ? Math.round((w1 + h1) / 1000 * factor * qty * 1000) / 1000 : 0,
    lm2: w2 > 0 && h2 > 0 ? Math.round((w2 + h2) / 1000 * factor * qty * 1000) / 1000 : 0,
    sqm1: Math.round(sqm1 * qty * 1000) / 1000,
    sqm2: Math.round(sqm2 * qty * 1000) / 1000,
    qty,
    value: money(sqm1 * qty * rate),
  };
}

export const polishKinds = [
  ["AllSidePolish", "All Side Polish"], ["BothHeightSide", "Both Height Side"], ["BothWidthSide", "Both Width Side"],
  ["AllSidePolishAsPerDwg", "All Side Polish As Per DWG"], ["BothHeightSideAsPerDwg", "Both Height Side As Per DWG"], ["BothWidthSideAsPerDwg", "Both Width Side As Per DWG"],
  ["BothHeightOneWidth", "Both Height + One Width Side Polish"], ["BothHeightOneWidthAsPerDwg", "Both Height + One Width Side Polish As Per DWG"],
  ["OneHeightOneWidth", "One Height + One Width Side Polish"], ["OneHeightOneWidthAsPerDwg", "One Height + One Width Side Polish As Per DWG"],
  ["OneHeightSide", "One Height Side Polish"], ["OneWidthSide", "One Width Side Polish"], ["Custom", "Custom (Manual)"],
] as const;

export function detectPolishKind(specName: string): string | undefined {
  const name = specName.toLowerCase();
  const rules: Array<[string, string]> = [
    ["All Side Polish As Per DWG", "AllSidePolishAsPerDwg"], ["All Side Polish", "AllSidePolish"], ["Polished Edges", "AllSidePolish"],
    ["Both Height Side Polish As Per DWG", "BothHeightSideAsPerDwg"], ["Both Height Side Polish", "BothHeightSide"],
    ["Both Width Side Polish As Per DWG", "BothWidthSideAsPerDwg"], ["Both Width Side Polish", "BothWidthSide"],
    ["Both Height + One Width Side Polish As Per DWG", "BothHeightOneWidthAsPerDwg"], ["Both Height + One Width Side Polish", "BothHeightOneWidth"],
    ["One Height + One Width Side Polish As Per DWG", "OneHeightOneWidthAsPerDwg"], ["One Height + One Width Side Polish", "OneHeightOneWidth"],
    ["One Height Side Polish", "OneHeightSide"], ["One Width Side Polish", "OneWidthSide"],
  ];
  return rules.find(([token]) => name.includes(token.toLowerCase()))?.[1];
}

export function polishLength(width: number, height: number, kind: string): number {
  switch (kind) {
    case "AllSidePolish": case "AllSidePolishAsPerDwg": return (width + height) / 1000 * 2;
    case "BothHeightSide": case "BothHeightSideAsPerDwg": return height / 1000 * 2;
    case "BothWidthSide": case "BothWidthSideAsPerDwg": return width / 1000 * 2;
    case "BothHeightOneWidth": case "BothHeightOneWidthAsPerDwg": return (height * 2 + width) / 1000;
    case "OneHeightOneWidth": case "OneHeightOneWidthAsPerDwg": return (height + width) / 1000;
    case "OneHeightSide": return height / 1000;
    case "OneWidthSide": return width / 1000;
    default: return 0;
  }
}

export type Step6Charge = { key: string; isActive: boolean; rate: string; metricKey: string; groupNos: number[]; polishType?: string; customDescription?: string; customQty?: string };
export type Step6CustomCharge = { id: string; isActive: boolean; name: string; qty: string; unit: "Sqm" | "Lm" | "Qty"; rate: string };
export type Step6Wastage = { id: string; isActive: boolean; color: string; groupNos: number[]; side: "outer" | "inner" | "third"; rate: string; sheetQuantity: string };
export type Step6Draft = { mode: "advanced" | "manual"; charges: Step6Charge[]; wastages: Step6Wastage[]; customCharges: Step6CustomCharge[]; manualActiveKeys: string[]; manualTargetKeys: string[]; manualWastageIds: string[] };

export const chargeTemplates = [
  ["ARGON_GAS", "Argon Gas (Sqm)", "Sqm", 45, "SQM1"], ["SILICON_BED", "Silicon bed (lm)", "Lm", 6, "LM1"],
  ["SILICON_BED_SQM", "Silicon bed (Sqm)", "Sqm", 6, "SQM1"], ["OVERLAP", "Overlap (lm)", "Lm", 6, "LM1"],
  ["OVERLAP_SQM", "Overlap (Sqm)", "Sqm", 6, "SQM1"], ["SHUTTER_BONDING", "Shutter Bonding (lm)", "Lm", 25, "LM_COMBINED"],
  ["MITRING", "Mitring (lm)", "Lm", 30, "LM1"], ["POLISH", "Polish (lm)", "Lm", 6, "LM1"],
  ["POLISH_MATCHED_LAM", "Polish — same-glass laminate (2×)", "Lm", 6, "LM_COMBINED"], ["CUTOUT", "Cutout (Qty)", "Qty", 50, "QTY"],
  ["HOLES", "Holes (Qty)", "Qty", 15, "QTY"], ["SANDBLASTING", "Sandblasting (Sqm)", "Sqm", 45, "SQM1"],
  ["EDGE_GRINDING", "Edge Grinding (lm)", "Lm", 4, "LM1"], ["COUNTERSUNK_HOLES", "Countersunk Holes (Qty)", "Qty", 25, "QTY"],
  ["FAN_HOLES", "Fan Holes (Qty)", "Qty", 35, "QTY"], ["DELIVERY", "Delivery (per trip)", "Qty", 150, "QTY"],
  ["U_INSERT", "U-Insert (Sqm)", "Sqm", 10, "SQM1"],
] as const;

export function emptyStep6(): Step6Draft {
  return { mode: "advanced", charges: chargeTemplates.map(([key, , , rate, metricKey]) => ({ key, isActive: false, rate: String(rate), metricKey, groupNos: [], polishType: "AllSidePolish", customDescription: "", customQty: "" })), wastages: [], customCharges: [], manualActiveKeys: [], manualTargetKeys: [], manualWastageIds: [] };
}

/** Apply the WPF Step 6 text-driven suggestions without reclaiming operator edits. */
export function applyAdvancedStep6Suggestions(step6: Step6Draft, builds: Record<number, Step4Build>): Step6Draft {
  if (step6.mode !== "advanced") return step6;
  const desired = new Map<string, { groups: number[]; metric: string; polish?: string }>();
  const add = (key: string, groupNo: number, metric: string, polish?: string) => {
    const current = desired.get(key) ?? { groups: [], metric };
    if (!current.groups.includes(groupNo)) current.groups.push(groupNo);
    current.metric = metric;
    if (polish) current.polish = polish;
    desired.set(key, current);
  };
  for (const [groupNo, build] of Object.entries(builds)) {
    const n = Number(groupNo), name = generatedStep4SpecName(build).toLowerCase();
    const isDgu = build.code === "DGU" || build.code === "DGU+LAM";
    const isDouble = isDgu || build.code === "LAM";
    if (name.includes("u-insert")) add("U_INSERT", n, isDgu ? "DGU_CAVITY" : "SQM1");
    if (name.includes("overlap (sqm)")) add("OVERLAP_SQM", n, "SQM1");
    else if (name.includes("overlap")) {
      add("OVERLAP", n, "LM1");
      if (isDgu) add("SILICON_BED", n, "LM1");
    }
    if (name.includes("argon gas")) add("ARGON_GAS", n, "DGU_CAVITY");
    if (name.includes("shutter bonding")) add("SHUTTER_BONDING", n, "LM_COMBINED");
    if (name.includes("fanhole")) add("FAN_HOLES", n, "QTY");
    if (/cutout.{0,20}holes|holes.{0,20}cutout/.test(name)) { add("CUTOUT", n, "QTY"); add("HOLES", n, "QTY"); }
    else {
      if (name.includes("cutout")) add("CUTOUT", n, "QTY");
      if (name.includes("holes")) add("HOLES", n, "QTY");
    }
    if (name.includes("mitring") || name.includes("beveled")) add("MITRING", n, "LM1");
    if (name.includes("sandblasted") || name.includes("frosted")) add("SANDBLASTING", n, "SQM1");
    const polishKind = detectPolishKind(name);
    if (polishKind) {
      const outerColor = build.code === "DGU+LAM" ? text(build, "lamOuterColour") : text(build, "outerColour");
      const innerColor = build.code === "DGU+LAM" ? text(build, "lamInnerColour") : text(build, "innerColour");
      const matchedLaminate = (build.code === "LAM" || build.code === "DGU+LAM") && Boolean(outerColor && innerColor && outerColor.toLowerCase() === innerColor.toLowerCase());
      add(matchedLaminate ? "POLISH_MATCHED_LAM" : "POLISH", n, isDouble ? "LM_COMBINED" : "LM1", polishKind);
    }
  }
  const desiredWastage = new Map<string, Step6Wastage>();
  for (const [groupNo, build] of Object.entries(builds)) {
    const panes: Array<[Step6Wastage["side"], string]> = build.code === "SGU"
      ? [["outer", "colour"]]
      : build.code === "DGU+LAM"
        ? [["outer", "outerColour"], ["third", "lamOuterColour"], ["inner", "lamInnerColour"]]
        : [["outer", "outerColour"], ["inner", "innerColour"]];
    for (const [side, colorKey] of panes) {
      const color = text(build, colorKey);
      if (!color) continue;
      const id = `auto-w-${groupNo}-${side}`;
      desiredWastage.set(id, { id, isActive: true, color, groupNos: [Number(groupNo)], side, rate: "0", sheetQuantity: "1" });
    }
  }
  const manualWastageIds = step6.manualWastageIds ?? [];
  const priorAuto = new Map(step6.wastages.filter((row) => row.id.startsWith("auto-w-")).map((row) => [row.id, row]));
  const manualWastages = step6.wastages.filter((row) => !row.id.startsWith("auto-w-"));
  const suggestedWastages = [...desiredWastage.values()].flatMap((row) => {
    if (manualWastageIds.includes(row.id)) return [];
    const prior = priorAuto.get(row.id);
    return [{ ...row, ...(prior ? { ...prior, id: row.id, groupNos: prior.groupNos } : {}) }];
  });
  return {
    ...step6,
    wastages: [...manualWastages, ...suggestedWastages],
    charges: step6.charges.map((charge) => {
      const suggestion = desired.get(charge.key);
      const activeManual = step6.manualActiveKeys.includes(charge.key);
      const targetManual = step6.manualTargetKeys.includes(charge.key);
      return {
        ...charge,
        isActive: activeManual ? charge.isActive : Boolean(suggestion),
        groupNos: targetManual ? charge.groupNos : (suggestion?.groups ?? []),
        metricKey: targetManual ? charge.metricKey : (suggestion?.metric ?? charge.metricKey),
        polishType: activeManual ? charge.polishType : (suggestion?.polish ?? charge.polishType),
      };
    }),
  };
}

export function totalStep6(step6: Step6Draft, lines: DraftLine[], builds: Record<number, Step4Build>): number {
  const metrics = new Map<number, WorkflowLineMetrics>();
  for (const line of lines) {
    const build = builds[line.groupNo];
    const next = workflowLineMetrics(line, build?.code);
    const prev = metrics.get(line.groupNo) ?? { lm1: 0, lm2: 0, sqm1: 0, sqm2: 0, qty: 0, value: 0 };
    metrics.set(line.groupNo, { lm1: prev.lm1 + next.lm1, lm2: prev.lm2 + next.lm2, sqm1: prev.sqm1 + next.sqm1, sqm2: prev.sqm2 + next.sqm2, qty: prev.qty + next.qty, value: prev.value + next.value });
  }
  const aggregate = (nos: number[], metric: string) => nos.reduce((sum, no) => {
    const m = metrics.get(no); if (!m) return sum;
    if (metric === "LM2") return sum + m.lm2;
    if (metric === "LM_COMBINED") return sum + m.lm1 + m.lm2;
    if (metric === "SQM2") return sum + m.sqm2;
    if (metric === "SQM_COMBINED") return sum + m.sqm1 + m.sqm2;
    if (metric === "DGU_CAVITY") return sum + (["DGU", "LAM", "DGU+LAM"].includes(builds[no]?.code ?? "") ? m.sqm1 : 0);
    if (metric === "DOUBLE_GLASS") return sum + m.qty * (builds[no]?.code === "DGU+LAM" ? 3 : builds[no]?.code === "DGU" || builds[no]?.code === "LAM" ? 2 : 1);
    if (metric === "TOTAL_PIECES" || metric === "QTY") return sum + m.qty;
    if (metric === "SQM1") return sum + m.sqm1;
    return sum + m.lm1;
  }, 0);
  const predefined = step6.charges.reduce((sum, charge) => {
    if (!charge.isActive) return sum;
    const rate = Number(charge.rate) || 0;
    const qty = charge.key === "DELIVERY" ? 1 : charge.key.startsWith("POLISH")
      ? charge.polishType === "Custom" ? Number(charge.customQty) || 0 : charge.groupNos.reduce((n, groupNo) => {
          const groupLines = lines.filter((line) => line.groupNo === groupNo);
          const build = builds[groupNo];
          if (!build) return n;
          const isDouble = ["DGU", "LAM", "DGU+LAM"].includes(build.code);
          const matchedOnly = charge.key === "POLISH_MATCHED_LAM";
          const outerColor = build.code === "DGU+LAM" ? text(build, "lamOuterColour") : text(build, "outerColour");
          const innerColor = build.code === "DGU+LAM" ? text(build, "lamInnerColour") : text(build, "innerColour");
          const matched = (build.code === "LAM" || build.code === "DGU+LAM") && Boolean(outerColor && innerColor && outerColor.toLowerCase() === innerColor.toLowerCase());
          if (matchedOnly && !matched) return n;
          return n + groupLines.reduce((lineSum, line) => {
            const w1 = Number(line.w1) || 0, h1 = Number(line.h1) || 0, w2 = Number(line.w2) || 0, h2 = Number(line.h2) || 0, pieces = Number(line.qty) || 0;
            let lm = w1 > 0 && h1 > 0 ? polishLength(w1, h1, charge.polishType || "AllSidePolish") : 0;
            if (isDouble && w2 > 0 && h2 > 0) lm += polishLength(w2, h2, charge.polishType || "AllSidePolish");
            else if (matchedOnly && matched && w1 > 0 && h1 > 0) lm += polishLength(w1, h1, charge.polishType || "AllSidePolish");
            return lineSum + lm * pieces;
          }, 0);
        }, 0)
      : aggregate(charge.groupNos, charge.metricKey);
    return sum + Math.round(qty * rate * 100) / 100;
  }, 0);
  const wastage = step6.wastages.reduce((sum, row) => {
    if (!row.isActive) return sum;
    const area = row.groupNos.reduce((amount, groupNo) => {
      const m = metrics.get(groupNo); if (!m) return amount;
      return amount + (row.side === "outer" ? m.sqm1 : m.sqm2);
    }, 0);
    return sum + Math.round(area * (Number(row.rate) || 0) * 100) / 100;
  }, 0);
  const custom = step6.customCharges.reduce((sum, row) => row.isActive ? sum + Math.round((Number(row.qty) || 0) * (Number(row.rate) || 0) * 100) / 100 : sum, 0);
  return Math.round((predefined + wastage + custom) * 100) / 100;
}

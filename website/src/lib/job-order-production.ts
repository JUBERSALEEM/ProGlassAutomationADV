import type { JobOrderSourceLine } from "@/types/job-orders";

export const PRODUCTION_STAGE_DEFINITIONS = [
  { code: "cutting", label: "Cutting", order: 1, quantityUnit: "panes" },
  { code: "holes_cutouts", label: "Holes & Cutouts", order: 2, quantityUnit: "pieces" },
  { code: "polish", label: "Polish", order: 3, quantityUnit: "linear metres" },
  { code: "edge_deletion", label: "Edge Deletion", order: 4, quantityUnit: "linear metres" },
  { code: "mitring", label: "Mitring", order: 5, quantityUnit: "linear metres" },
  { code: "tempering", label: "Tempering", order: 6, quantityUnit: "panes" },
  { code: "pairing", label: "Pairing", order: 7, quantityUnit: "units" },
  { code: "doubling_lamination", label: "Doubling / Lamination", order: 8, quantityUnit: "units" },
] as const;

export type ProductionStageCode = (typeof PRODUCTION_STAGE_DEFINITIONS)[number]["code"];
export type ProductionQuantityUnit = (typeof PRODUCTION_STAGE_DEFINITIONS)[number]["quantityUnit"];
export type ProductionJobStatus = "Released" | "In Production" | "Completed";

export type PlannedProductionStage = {
  stage_code: ProductionStageCode;
  stage_label: string;
  stage_order: number;
  planned_qty: number;
  quantity_unit: ProductionQuantityUnit;
};

export type TrackedProductionStage = PlannedProductionStage & {
  id: string;
  completed_qty: number;
  status: "Pending" | "In Progress" | "Completed";
  updated_at: string;
};

export type TrackedProductionJobOrder = {
  id: string;
  job_order_number: string;
  status: ProductionJobStatus;
  source_file: string;
  released_at: string;
  ordered_qty: number;
  ordered_sqm: number;
  route_source: "records_summary";
  stages: TrackedProductionStage[];
};

export type ProductionRoutePreview = {
  build_types: string[];
  stages: PlannedProductionStage[];
  required_stage_codes: ProductionStageCode[];
  warnings: string[];
  requires_review: boolean;
};

type BuildFamily = "SGU" | "DGU" | "LAM" | "DGU+LAM";

function normalized(value: string): string {
  return value.normalize("NFKC").toLocaleUpperCase().replace(/[–—]/g, "-").replace(/\s+/g, " ").trim();
}

function buildFamily(value: string): BuildFamily | null {
  const type = normalized(value);
  if (/\bDGU\s*\+\s*LAM\b|\bDGU\s*\+\s*LAMINATION\b|\bHYBRID\b/.test(type)) return "DGU+LAM";
  if (/\bDGU\b|DOUBLE\s*GLAZ/.test(type)) return "DGU";
  if (/\bLAM\b|LAMINAT/.test(type)) return "LAM";
  if (/\bSGU\b|SINGLE\s*GLAZ/.test(type)) return "SGU";
  return null;
}

function requiresTempering(value: string): boolean {
  return /\bFT\b|FULLY\s*TEMPERED|\bTEMPERED\b|HEAT\s*STRENGTHENED|HEAT\s*SOAKED|\bHSO?\b/.test(normalized(value));
}

function quantity(value: number): number {
  return Number.isFinite(value) && value > 0 ? Math.round(value * 1000) / 1000 : 0;
}

function stageDefinition(code: ProductionStageCode) {
  return PRODUCTION_STAGE_DEFINITIONS.find((stage) => stage.code === code)!;
}

function stageQuantity(code: ProductionStageCode, value: number): number {
  if (!Number.isFinite(value) || value <= 0) return 0;
  return stageDefinition(code).quantityUnit === "linear metres"
    ? Math.round(value * 1000) / 1000
    : Math.round(value);
}

/**
 * Build a reviewable plan from the summary fields currently synchronized to
 * public.records. This deliberately does not pretend that the summary is a
 * complete production snapshot: pane treatment and Step 6 processing charges
 * are not present in that feed and must be checked by the releasing operator.
 */
export function buildProductionRoutePreview(lines: JobOrderSourceLine[]): ProductionRoutePreview {
  const totals = new Map<ProductionStageCode, number>();
  const buildTypes = new Set<string>();
  const warnings: string[] = [];
  const requiredStages = new Set<ProductionStageCode>(["cutting"]);
  let unknownTypeCount = 0;
  let knownLineCount = 0;

  const add = (code: ProductionStageCode, amount: number) => {
    const value = stageQuantity(code, amount);
    if (value <= 0) return;
    totals.set(code, stageQuantity(code, (totals.get(code) || 0) + value));
  };

  for (const line of lines) {
    const qty = quantity(line.qty);
    const family = buildFamily(line.type_of_work);
    if (family) {
      knownLineCount += 1;
      buildTypes.add(family);
      if (family !== "SGU") {
        requiredStages.add("pairing");
        requiredStages.add("doubling_lamination");
      }
    } else if (line.id && qty > 0) {
      unknownTypeCount += 1;
    }

    if (!line.id || qty <= 0 || !Number.isInteger(qty)) {
      warnings.push("At least one confirmed PI source line has no stable ID or a zero, invalid or fractional finished-unit quantity. Fix the source record before release.");
      continue;
    }
    if (!family) continue;

    const panesPerUnit = family === "SGU" ? 1 : family === "DGU+LAM" ? 3 : 2;
    add("cutting", qty * panesPerUnit);

    if (requiresTempering(line.type_of_work)) {
      add("tempering", qty * panesPerUnit);
    }

    if (family !== "SGU") {
      add("pairing", qty);
      add("doubling_lamination", qty);
    }

    const type = normalized(line.type_of_work);
    if (/HOLE|CUTOUT|COUNTERSUNK/.test(type)) add("holes_cutouts", qty * panesPerUnit);
    if (/POLISH|EDGE\s*GRIND|EDGE\s*POLISH/.test(type)) add("polish", 0);
    if (/MITR/.test(type)) add("mitring", 0);
    if (/EDGE\s*DELETION/.test(type)) add("edge_deletion", 0);
  }

  if (unknownTypeCount > 0) {
    warnings.push(`${unknownTypeCount} source line(s) have an unsupported or ambiguous work type. Verify the build family, then enter the cutting pane count and any required Pairing / Doubling quantities manually before release.`);
  }
  if (knownLineCount > 0) {
    warnings.push("The synced PI summary does not include pane treatment or complete Step 6 charge details. Review tempering, holes, polish, edge work and mitring before release; add any missing stages and quantities below.");
  }
  if (lines.length === 0) {
    warnings.push("No confirmed PI source lines were supplied. Refresh the PI queue before releasing.");
  }

  const stages = Array.from(totals.entries())
    .map(([stage_code, planned_qty]) => {
      const definition = stageDefinition(stage_code);
      return {
        stage_code,
        stage_label: definition.label,
        stage_order: definition.order,
        planned_qty,
        quantity_unit: definition.quantityUnit,
      };
    })
    .sort((a, b) => a.stage_order - b.stage_order);

  return {
    build_types: Array.from(buildTypes).sort(),
    stages,
    required_stage_codes: Array.from(requiredStages).sort((a, b) => stageDefinition(a).order - stageDefinition(b).order),
    warnings: Array.from(new Set(warnings)),
    requires_review: true,
  };
}

export function isProductionStageCode(value: string): value is ProductionStageCode {
  return PRODUCTION_STAGE_DEFINITIONS.some((stage) => stage.code === value);
}

export function plannedStage(code: ProductionStageCode, plannedQty: number): PlannedProductionStage {
  const definition = stageDefinition(code);
  return {
    stage_code: code,
    stage_label: definition.label,
    stage_order: definition.order,
    planned_qty: stageQuantity(code, plannedQty),
    quantity_unit: definition.quantityUnit,
  };
}

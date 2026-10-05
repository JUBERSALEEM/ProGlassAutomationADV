import assert from "node:assert/strict";
import test from "node:test";
import { buildProductionRoutePreview } from "../src/lib/job-order-production.ts";
import { isActiveConfirmedProforma } from "../src/lib/job-order-source-gate.ts";

const line = (id, type_of_work, qty = 1) => ({ id, type_of_work, qty, sqm: 1 });
const stagesByCode = (preview) => Object.fromEntries(preview.stages.map((stage) => [stage.stage_code, stage]));

test("release gate accepts only active rows with exact Confirmed status", () => {
  assert.equal(isActiveConfirmedProforma("Confirmed", true), true);
  assert.equal(isActiveConfirmedProforma("confirmed", true), false);
  assert.equal(isActiveConfirmedProforma("Confirmed ", true), false);
  assert.equal(isActiveConfirmedProforma("In Production", true), false);
  assert.equal(isActiveConfirmedProforma("Confirmed", false), false);
  assert.equal(isActiveConfirmedProforma("Confirmed", undefined), false);
});

test("SGU uses pane cutting and no multi-pane assembly stages", () => {
  const preview = buildProductionRoutePreview([line("1", "SGU", 10)]);
  const stages = stagesByCode(preview);
  assert.equal(stages.cutting.planned_qty, 10);
  assert.equal(stages.cutting.quantity_unit, "panes");
  assert.equal(stages.pairing, undefined);
  assert.equal(stages.doubling_lamination, undefined);
  assert.deepEqual(preview.required_stage_codes, ["cutting"]);
});

test("DGU and LAM use two panes and finished-unit assembly counts", () => {
  for (const build of ["DGU", "LAM"]) {
    const preview = buildProductionRoutePreview([line("1", build, 10)]);
    const stages = stagesByCode(preview);
    assert.equal(stages.cutting.planned_qty, 20, `${build} cutting panes`);
    assert.equal(stages.pairing.planned_qty, 10, `${build} paired finished units`);
    assert.equal(stages.pairing.quantity_unit, "units");
    assert.equal(stages.doubling_lamination.planned_qty, 10, `${build} assembled finished units`);
    assert.deepEqual(preview.required_stage_codes, ["cutting", "pairing", "doubling_lamination"]);
  }
});

test("DGU+LAM uses three cutting panes per finished unit", () => {
  const preview = buildProductionRoutePreview([line("1", "DGU+LAM", 4)]);
  const stages = stagesByCode(preview);
  assert.equal(stages.cutting.planned_qty, 12);
  assert.equal(stages.pairing.planned_qty, 4);
  assert.equal(stages.doubling_lamination.planned_qty, 4);
});

test("mixed SGU and DGU lines combine pane counts without assembly-count inflation", () => {
  const preview = buildProductionRoutePreview([
    line("1", "SGU", 5),
    line("2", "DGU", 2),
  ]);
  const stages = stagesByCode(preview);
  assert.equal(stages.cutting.planned_qty, 9);
  assert.equal(stages.pairing.planned_qty, 2);
  assert.equal(stages.doubling_lamination.planned_qty, 2);
});

test("pane-piece operations scale with the physical pane count and stay reviewable", () => {
  const preview = buildProductionRoutePreview([line("1", "DGU with holes", 3)]);
  const stages = stagesByCode(preview);
  assert.equal(stages.holes_cutouts.planned_qty, 6);
  assert.equal(stages.holes_cutouts.quantity_unit, "pieces");
  assert.equal(preview.requires_review, true);
  assert.ok(preview.warnings.some((warning) => /Step 6 charge details/i.test(warning)));
});

test("unknown builds do not invent pane counts or mark a route complete", () => {
  const preview = buildProductionRoutePreview([line("1", "Special fabrication", 2)]);
  assert.equal(preview.stages.length, 0);
  assert.deepEqual(preview.required_stage_codes, ["cutting"]);
  assert.equal(preview.requires_review, true);
  assert.ok(preview.warnings.some((warning) => /unsupported or ambiguous/i.test(warning)));
});

test("fractional source finished-unit counts are not rounded into pane instructions", () => {
  const preview = buildProductionRoutePreview([line("1", "DGU", 1.5)]);
  assert.equal(preview.stages.length, 0);
  assert.deepEqual(preview.required_stage_codes, ["cutting", "pairing", "doubling_lamination"]);
  assert.ok(preview.warnings.some((warning) => /fractional finished-unit quantity/i.test(warning)));
});

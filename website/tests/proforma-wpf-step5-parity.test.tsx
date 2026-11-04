import { readFileSync } from "node:fs";
import { describe, expect, it } from "vitest";
import { billablePanelSqm, physicalPanelSqm } from "../src/data/pricingRules";
import { draftIssues, emptyLine, priceLine } from "../src/lib/proforma-draft";
import { ProformaApiFailure, validateLines } from "../src/lib/proforma-api";

const migration = readFileSync("supabase-inquiry-proforma-phaseC8-wpf-step5-parity.sql", "utf8");

describe("WPF Step 5 calculation fixtures", () => {
  it("matches SqmCalculator.CalculatePanel midpoint-to-even fixtures before the 0.5 floor", () => {
    // WPF source: Utilities/SqmCalculator.cs, CalculatePanel; decimal
    // Math.Round(actual, 3) uses MidpointRounding.ToEven by default.
    expect(billablePanelSqm(500, 1001)).toBe(0.5); // 0.5005 -> 0.500, then floor is already met
    expect(billablePanelSqm(500, 1003)).toBe(0.502); // 0.5015 -> 0.502
    expect(billablePanelSqm(300, 300)).toBe(0.5); // physical 0.09 -> billable floor
    expect(billablePanelSqm(0, 300)).toBe(0); // invalid panel is not floored
  });

  it("keeps physical panel area raw and separate from billable area", () => {
    expect(physicalPanelSqm(500, 1001)).toBe(0.5005);
    expect(physicalPanelSqm(300, 300)).toBe(0.09);
    expect(billablePanelSqm(300, 300)).toBe(0.5);
  });

  it("matches GlassItemRow.RecalculateAll total area and half-away money rounding", () => {
    const line = {
      ...emptyLine(1), w1: "300", h1: "300", qty: "3", rate: "99.99",
    };
    expect(priceLine(line)).toMatchObject({ sqm: 0.5, totalSqm: 1.5, value: 149.99 });
  });

  it("matches SpecGroupService.ValidateRow primary dimensions and quantity boundaries in draft and API validation", () => {
    const valid = { group_no: 1, line_no: 1, glass_ref: "fixture", w1: 10000, h1: 10000, w2: 0, h2: 0, qty: 10000, rate: 1 };
    const draft = { ...emptyLine(1), w1: "10000", h1: "10000", qty: "10000", rate: "1" };
    expect(draftIssues([draft])).toEqual([]);
    expect(validateLines([valid])[0]).toMatchObject({ w1: 10000, h1: 10000, qty: 10000 });
    expect(draftIssues([{ ...draft, w1: "10001" }]).join(" ")).toContain("primary panel side cannot exceed 10000 mm");
    expect(draftIssues([{ ...draft, qty: "10001" }]).join(" ")).toContain("quantity cannot exceed 10000");
    expect(() => validateLines([{ ...valid, w1: 10001 }])).toThrowError(ProformaApiFailure);
    expect(() => validateLines([{ ...valid, qty: 10001 }])).toThrow("quantity between 1 and 10000");
  });
});

describe("Phase C8 persistence contract", () => {
  it("uses exact integer midpoint-to-even arithmetic in the database helper", () => {
    expect(migration).toContain("public.wpf_pi_billable_panel_sqm");
    expect(migration).toContain("v_remainder > 500 OR (v_remainder = 500 AND mod(v_milli_sqm, 2) = 1)");
    expect(migration).toContain("RETURN greatest(v_milli_sqm::numeric / 1000, 0.5)");
  });

  it("redefines both API RPC versions to use the same WPF bounds and calculation", () => {
    for (const functionName of ["save_inquiry_proforma_v1", "save_inquiry_proforma_v2"]) {
      const start = migration.indexOf(`FUNCTION public.${functionName}(`);
      expect(start).toBeGreaterThanOrEqual(0);
      const end = migration.indexOf("$$;", start);
      const body = migration.slice(start, end);
      expect(body).toContain("NOT BETWEEN 1 AND 10000");
      expect(body).toContain("NOT BETWEEN 1 AND 10000 THEN");
      expect(body).toContain("public.wpf_pi_billable_panel_sqm(v_line.w1, v_line.h1)");
      expect(body).toContain("round(v_sqm * v_line.qty, 3)");
      expect(body).toContain("round(v_sqm * v_line.qty * coalesce(v_line.rate, 0), 2)");
    }
  });
});

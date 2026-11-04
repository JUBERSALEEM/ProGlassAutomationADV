// @vitest-environment jsdom

import { describe, expect, it } from "vitest";
import { createXlsx } from "@/lib/export-formats";
import type { OperationsExportBundle } from "@/lib/operations-exports";
import { calculateWpfPanelSqm, calculateWpfTotalSqm, parseCustomerCutListFile } from "@/lib/customer-cutlist";

function asFile(name: string, bytes: Uint8Array | string): File {
  const file = new File([bytes], name);
  const content = typeof bytes === "string" ? new TextEncoder().encode(bytes) : bytes;
  return Object.assign(file, { arrayBuffer: async () => content.buffer.slice(content.byteOffset, content.byteOffset + content.byteLength) as ArrayBuffer });
}

function xlsxFixture() {
  const sheet = {
    name: "Customer Cut List",
    columns: [
      { key: "specNumber", header: "Spec #" }, { key: "spec", header: "Spec Name" }, { key: "kind", header: "Glass Type" },
      { key: "ref", header: "Glass Ref" }, { key: "w", header: "W1" },
      { key: "h", header: "H1 (mm)" }, { key: "qty", header: "Quantity" },
      { key: "rate", header: "Rate" },
    ],
    rows: [{ specNumber: 2, spec: "Premium laminated group", kind: "LAM", ref: "B-2", w: 900, h: 2100, qty: 3, rate: 999 }],
  };
  const bundle: OperationsExportBundle = {
    schemaVersion: 1,
    report: "proformas",
    title: "Technical cut list",
    generatedAt: new Date(0).toISOString(),
    rowCount: 1,
    filters: {},
    summary: {},
    sheets: [sheet],
    csvSheet: sheet,
  };
  return createXlsx(bundle);
}

describe("customer Step 5 cut-list tools", () => {
  it("matches WPF panel and total SQM rules, including the minimum billable area", () => {
    expect(calculateWpfPanelSqm(1200, 1800)).toBe(2.16);
    expect(calculateWpfTotalSqm(1200, 1800, 2)).toBe(4.32);
    expect(calculateWpfPanelSqm(400, 300)).toBe(0.5);
    expect(calculateWpfTotalSqm(400, 300, 2)).toBe(1);
    expect(calculateWpfPanelSqm("", 300)).toBeNull();
    expect(calculateWpfPanelSqm(1200.5, 1800)).toBeNull();
  });

  it("imports quoted CSV lines and ignores any price columns", async () => {
    const file = asFile("cut-list.csv", "Spec Name,Glass Type,Glass Ref,W1 (mm),H1 (mm),W2 (mm),H2 (mm),Quantity,Rate\r\n\"Spec Group 1\",DGU,\"A,1\",1200,1800,1100,1700,2,450.00\r\n");
    const result = await parseCustomerCutListFile(file);
    expect(result.rows).toEqual([{
      specNumber: "1", specName: "Spec Group 1", glassType: "DGU", reference: "A,1", width1: "1200", height1: "1800",
      width2: "1100", height2: "1700", quantity: "2",
    }]);
    expect(JSON.stringify(result)).not.toMatch(/rate|450/i);
  });

  it("imports an Excel workbook and keeps only technical cutting-list columns", async () => {
    const result = await parseCustomerCutListFile(asFile("cut-list.xlsx", xlsxFixture()));
    expect(result.rows).toEqual([{
      specNumber: "2", specName: "Premium laminated group", glassType: "LAM", reference: "B-2", width1: "900", height1: "2100",
      width2: "", height2: "", quantity: "3",
    }]);
  });

  it("imports cutting lists up to the 12-group WPF bulk-generation capacity", async () => {
    const lines = ["Glass Ref,W1 (mm),H1 (mm),Quantity", ...Array.from({ length: 120_001 }, (_, index) => `R${index + 1},800,1200,1`)];
    const result = await parseCustomerCutListFile(asFile("large-cut-list.csv", lines.join("\n")));
    expect(result.rows).toHaveLength(120_000);
  });

  it("imports WPF-style headerless TXT rows", async () => {
    const result = await parseCustomerCutListFile(asFile("loose-cut-list.txt", "A1 1200 1800 2\nA2 800x1200 1\n"));
    expect(result.rows).toEqual([
      { specNumber: "", specName: "", glassType: "", reference: "A1", width1: "1200", height1: "1800", width2: "", height2: "", quantity: "2" },
      { specNumber: "", specName: "", glassType: "", reference: "A2", width1: "800", height1: "1200", width2: "", height2: "", quantity: "1" },
    ]);
  });


});

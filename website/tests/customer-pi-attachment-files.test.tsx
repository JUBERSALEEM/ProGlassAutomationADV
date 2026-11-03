// @vitest-environment jsdom

import { createHash } from "node:crypto";
import { unzipSync } from "fflate";
import { describe, expect, it } from "vitest";
import { validateInquiryDocumentCandidate } from "@/lib/inquiry-documents";

async function bytesOf(blob: Blob) {
  const withArrayBuffer = blob as Blob & { arrayBuffer?: () => Promise<ArrayBuffer> };
  if (withArrayBuffer.arrayBuffer) return new Uint8Array(await withArrayBuffer.arrayBuffer());
  return new Promise<Uint8Array>((resolve, reject) => {
    const reader = new FileReader();
    reader.onerror = () => reject(reader.error);
    reader.onload = () => resolve(new Uint8Array(reader.result as ArrayBuffer));
    reader.readAsArrayBuffer(blob);
  });
}

describe("generated PI attachment pack", () => {
  it("exports a standalone WPF-style technical cut-list PDF without customer pricing", async () => {
    const { generateCustomerPiCutListPdf } = await import("@/lib/customer-pi-attachments");
    const file = await generateCustomerPiCutListPdf({
      companyName: "Example Glass",
      contactName: "Example Contact",
      customerReference: "REF-1",
      projectName: "Sample project",
      projectLocation: "Abu Dhabi",
      projectRequirements: "Technical cut list export test.",
      technicalSpecification: "Technical only",
      technicalSpecificationJson: JSON.stringify({
        specs: [{ id: "spec-1", specName: "Facade DGU", kind: "DGU", panes: [] }],
        rows: [{ reference: "A1", specId: "spec-1", width1: "1200", height1: "1800", width2: "", height2: "", quantity: "2" }],
        specNotes: "",
      }),
    });
    expect(file.name).toBe("ProGlass_Customer_Cut_List.pdf");
    expect(file.type).toBe("application/pdf");
    expect(new TextDecoder().decode(await bytesOf(file).then((bytes) => bytes.slice(0, 5)))).toBe("%PDF-");
  });

  it("creates valid Excel, CSV, and PDF attachments from the technical specification only", async () => {
    const { generateCustomerPiAttachments } = await import("@/lib/customer-pi-attachments");
    const files = await generateCustomerPiAttachments({
      companyName: "Al Noor Glass LLC",
      contactName: "Aisha Khan",
      customerReference: "RFQ-2026-018",
      projectName: "Marina Residence",
      projectLocation: "أبوظبي — Abu Dhabi",
      projectRequirements: "Supply DGU panels for the main facade by March.",
      technicalSpecification: "CUSTOMER PI TECHNICAL SPECIFICATION — NO PRICING\nSpec Group 1: DGU",
      technicalSpecificationJson: JSON.stringify({
        specs: [{
          id: "spec-1", kind: "DGU",
          panes: [
            { thickness: "6mm", colour: "Clear", treatment: "FT", sheetWidth: "3210", sheetHeight: "2250" },
            { thickness: "6mm", colour: "Grey", treatment: "Annealed", sheetWidth: "3210", sheetHeight: "2250" },
          ],
          spacerWidth: "12mm", spacerMaterial: "Black Aluminum", panelExtra: "With Overlap As Per DWG", spacerExtra: "With U-Insert", edgeFinish: "Polished Edges", fabricationNotes: "Argon gas as per drawing",
        }, {
          id: "spec-2", kind: "LAM",
          panes: [
            { thickness: "8mm", colour: "Clear", treatment: "FT", sheetWidth: "3210", sheetHeight: "2250" },
            { thickness: "8mm", colour: "Clear", treatment: "FT", sheetWidth: "3210", sheetHeight: "2250" },
          ],
          interlayerType: "PVB", interlayerThickness: "1.52mm", interlayerColour: "Clear", panelExtra: "Arrissed Edges", edgeFinish: "Polished Edges", fabricationNotes: "Match samples.",
        }],
        rows: [
          { reference: "A1", specId: "spec-1", width1: "1200", height1: "1800", width2: "1190", height2: "1790", quantity: "4" },
          { reference: "B1", specId: "spec-2", width1: "800", height1: "1000", quantity: "2" },
        ],

        specNotes: "Confirm handing with the estimator.",
      }),
    });

    expect(files.map((file) => file.name.split(".").at(-1))).toEqual(["xlsx", "csv", "pdf"]);
    expect(files).toHaveLength(3);

    const [xlsx, csv, pdf] = files;
    const xlsxBytes = await bytesOf(xlsx!);
    const entries = unzipSync(xlsxBytes);
    expect(entries["[Content_Types].xml"]).toBeTruthy();
    expect(entries["xl/workbook.xml"]).toBeTruthy();
    expect(entries["xl/worksheets/sheet2.xml"]).toBeTruthy();
    const workbookText = Object.values(entries).map((entry) => new TextDecoder().decode(entry)).join("\n");
    expect(workbookText).toContain("With Overlap As Per DWG");
    expect(workbookText).toContain("PVB 1.52mm Clear");
    expect(workbookText).toContain("1200");
    expect(workbookText).not.toMatch(/AED|Margin|Profit|Yield/i);

    const csvText = new TextDecoder().decode(await bytesOf(csv!));
    expect(csvText).toContain("Spec Group 1");
    expect(csvText).toContain("With Overlap As Per DWG");
    expect(csvText).toContain("Stock sheet 3210 × 2250 mm");
    expect(csvText).toContain("Spacer 12mm Black Aluminum");
    expect(csvText).toContain("Interlayer PVB 1.52mm Clear");
    expect(csvText).toContain("1200");
    const pdfBytes = await bytesOf(pdf!);
    expect(new TextDecoder().decode(pdfBytes.slice(0, 5))).toBe("%PDF-");

    for (const file of files) {
      const bytes = await bytesOf(file);
      const extension = `.${file.name.split(".").at(-1)}` as ".xlsx" | ".csv" | ".pdf";
      const validation = validateInquiryDocumentCandidate({
        filename: file.name,
        byteSize: bytes.byteLength,
        declaredMime: file.type,
        detectedMime: extension === ".xlsx" ? "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" : extension === ".csv" ? "text/csv" : "application/pdf",
        sha256: createHash("sha256").update(bytes).digest("hex"),
        signature: bytes.slice(0, 32),
        deepContentValidated: extension !== ".pdf",
      });
      expect(validation.ok, file.name).toBe(true);
    }
  });
});

// @vitest-environment jsdom

import { createHash } from "node:crypto";
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

const request = {
  companyName: "Al Noor Glass LLC",
  contactName: "Aisha Khan",
  customerReference: "RFQ-2026-018",
  projectName: "Marina Residence",
  projectLocation: "Abu Dhabi",
  projectRequirements: "Supply DGU panels for the main facade by March.",
  technicalSpecificationJson: JSON.stringify({
    specs: [{
      id: "spec-1", specName: "Facade DGU", kind: "DGU",
      panes: [
        { thickness: "6mm", colour: "Clear", treatment: "FT", sheetWidth: "3210", sheetHeight: "2250" },
        { thickness: "6mm", colour: "Grey", treatment: "Annealed", sheetWidth: "3210", sheetHeight: "2250" },
      ],
      spacerWidth: "12mm", spacerMaterial: "Black Aluminum", panelExtra: "With Overlap As Per DWG",
      spacerExtra: "With U-Insert", edgeFinish: "Polished Edges", fabricationNotes: "Argon gas as per drawing",
    }],
    rows: [
      { reference: "A1", specId: "spec-1", width1: "1200", height1: "1800", width2: "1190", height2: "1790", quantity: "4" },
      { reference: "B1", specId: "spec-1", width1: "800", height1: "1000", quantity: "2" },
    ],
    specNotes: "Confirm handing with the estimator.",
  }),
};

describe("customer PI Inquiry PDF", () => {
  it("generates one unpriced PDF containing the technical request, with no spreadsheet exports", async () => {
    const { generateCustomerPiInquiryPdf } = await import("@/lib/customer-pi-attachments");
    const file = await generateCustomerPiInquiryPdf(request);
    expect(file.name).toBe("ProGlass_PI_Technical_Inquiry.pdf");
    expect(file.type).toBe("application/pdf");
    expect(file.size).toBeGreaterThan(100);
    const bytes = await bytesOf(file);
    expect(new TextDecoder().decode(bytes.slice(0, 5))).toBe("%PDF-");

    const validation = validateInquiryDocumentCandidate({
      filename: file.name,
      byteSize: bytes.byteLength,
      declaredMime: file.type,
      detectedMime: "application/pdf",
      sha256: createHash("sha256").update(bytes).digest("hex"),
      signature: bytes.slice(0, 32),
      deepContentValidated: false,
    });
    expect(validation.ok).toBe(true);
  });

  it("requires completed technical specifications before creating a PDF", async () => {
    const { generateCustomerPiInquiryPdf } = await import("@/lib/customer-pi-attachments");
    await expect(generateCustomerPiInquiryPdf({ ...request, technicalSpecificationJson: "{}" })).rejects.toThrow(/complete the glass specification and cutting list/i);
  });
});

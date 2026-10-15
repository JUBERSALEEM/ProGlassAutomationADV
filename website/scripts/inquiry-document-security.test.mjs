import assert from "node:assert/strict";
import test from "node:test";
import {
  INQUIRY_DOCUMENT_MAX_BYTES,
  inquiryDocumentObjectKey,
  isInquiryDocumentObjectKey,
  sha256Hex,
  validateInquiryDocumentCandidate,
} from "../src/lib/inquiry-documents.ts";

const bytes = (...values) => Uint8Array.from(values);
const valid = (overrides = {}) => ({
  filename: "customer-drawing.pdf",
  byteSize: 512,
  declaredMime: "application/pdf",
  detectedMime: "application/pdf",
  sha256: "a".repeat(64),
  signature: bytes(0x25, 0x50, 0x44, 0x46, 0x2d, 0x31, 0x2e, 0x37),
  ...overrides,
});

test("accepts a bounded PDF only when MIME, signature, and hash agree", () => {
  assert.deepEqual(validateInquiryDocumentCandidate(valid()), {
    ok: true,
    extension: ".pdf",
    normalizedFilename: "customer-drawing.pdf",
  });
});

test("rejects path traversal, Windows device names, and control characters", () => {
  for (const filename of ["../secret.pdf", "folder\\secret.pdf", "CON.pdf", "bad\u0000.pdf"]) {
    const result = validateInquiryDocumentCandidate(valid({ filename }));
    assert.equal(result.ok, false, filename);
    assert.equal(result.code, "INVALID_FILENAME", filename);
  }
});

test("rejects empty and oversized files", () => {
  assert.equal(validateInquiryDocumentCandidate(valid({ byteSize: 0 })).code, "EMPTY_FILE");
  assert.equal(
    validateInquiryDocumentCandidate(valid({ byteSize: INQUIRY_DOCUMENT_MAX_BYTES + 1 })).code,
    "FILE_TOO_LARGE",
  );
});

test("cloud allowlist excludes desktop-only CAD, legacy XLS, and SVG formats", () => {
  for (const filename of ["drawing.dwg", "drawing.dxf", "prices.xls", "active.svg", "archive.zip"]) {
    assert.equal(validateInquiryDocumentCandidate(valid({ filename })).code, "UNSUPPORTED_EXTENSION");
  }
});

test("rejects extension/MIME mismatch and spoofed signatures", () => {
  assert.equal(validateInquiryDocumentCandidate(valid({ detectedMime: "image/jpeg" })).code, "MIME_MISMATCH");
  assert.equal(validateInquiryDocumentCandidate(valid({ signature: bytes(0xff, 0xd8, 0xff) })).code, "SIGNATURE_MISMATCH");
});

test("validates PNG, JPEG, GIF, WebP, BMP, XLSX and text CSV signatures", () => {
  const cases = [
    ["a.png", "image/png", bytes(0x89,0x50,0x4e,0x47,0x0d,0x0a,0x1a,0x0a)],
    ["a.jpeg", "image/jpeg", bytes(0xff,0xd8,0xff,0xe0)],
    ["a.gif", "image/gif", bytes(0x47,0x49,0x46,0x38,0x39,0x61)],
    ["a.webp", "image/webp", bytes(0x52,0x49,0x46,0x46,0,0,0,0,0x57,0x45,0x42,0x50)],
    ["a.bmp", "image/bmp", bytes(0x42,0x4d,0,0)],
    ["a.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", bytes(0x50,0x4b,0x03,0x04)],
    ["a.csv", "text/csv", new TextEncoder().encode("item,qty\nGlass,4\n")],
  ];
  for (const [filename, detectedMime, signature] of cases) {
    assert.equal(validateInquiryDocumentCandidate(valid({
      filename,
      declaredMime: detectedMime,
      detectedMime,
      signature,
      deepContentValidated: filename.endsWith(".xlsx") || filename.endsWith(".csv") ? true : undefined,
    })).ok, true, filename);
  }
});

test("requires complete safe parsing for XLSX containers and CSV text", () => {
  assert.equal(validateInquiryDocumentCandidate(valid({
    filename: "a.xlsx",
    declaredMime: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
    detectedMime: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
    signature: bytes(0x50,0x4b,0x03,0x04),
  })).code, "SIGNATURE_MISMATCH");
  assert.equal(validateInquiryDocumentCandidate(valid({
    filename: "a.csv",
    declaredMime: "text/csv",
    detectedMime: "text/csv",
    signature: new TextEncoder().encode("a,b\n1,2\n"),
  })).code, "SIGNATURE_MISMATCH");
});

test("rejects malformed SHA-256 values", () => {
  assert.equal(validateInquiryDocumentCandidate(valid({ sha256: "ABC" })).code, "INVALID_SHA256");
});

test("object keys are opaque canonical UUID paths with no business data", () => {
  const key = inquiryDocumentObjectKey({
    sourceDatasetId: "11111111-1111-4111-8111-111111111111",
    inquiryId: "22222222-2222-4222-8222-222222222222",
    documentId: "33333333-3333-4333-8333-333333333333",
    versionId: "44444444-4444-4444-8444-444444444444",
  });
  assert.equal(key, "inquiry-documents/11111111-1111-4111-8111-111111111111/22222222-2222-4222-8222-222222222222/33333333-3333-4333-8333-333333333333/44444444-4444-4444-8444-444444444444");
  assert.equal(isInquiryDocumentObjectKey(key), true);
  assert.equal(isInquiryDocumentObjectKey(`${key}/customer.pdf`), false);
  assert.throws(() => inquiryDocumentObjectKey({
    sourceDatasetId: "INQ-2026-0001",
    inquiryId: "22222222-2222-4222-8222-222222222222",
    documentId: "33333333-3333-4333-8333-333333333333",
    versionId: "44444444-4444-4444-8444-444444444444",
  }));
});

test("SHA-256 helper returns deterministic lowercase integrity hashes", () => {
  assert.equal(sha256Hex(new TextEncoder().encode("ProGlass")), "1bedd758a113d75c325744b35a4ece1bebb6270970365f8fe62f0f9b3f50fe90");
});

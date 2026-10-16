import { createHash } from "node:crypto";

/** Phase 3 cloud publication policy. Deliberately narrower than the desktop. */
export const INQUIRY_DOCUMENT_MAX_BYTES = 10 * 1024 * 1024;
export const INQUIRY_DOCUMENT_BUCKET = "inquiry-documents";

export const INQUIRY_DOCUMENT_TYPES = {
  ".pdf": ["application/pdf"],
  ".jpg": ["image/jpeg"],
  ".jpeg": ["image/jpeg"],
  ".png": ["image/png"],
  ".gif": ["image/gif"],
  ".webp": ["image/webp"],
  ".bmp": ["image/bmp", "image/x-ms-bmp"],
  ".csv": ["text/csv", "text/plain", "application/csv"],
  ".xlsx": ["application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "application/zip"],
} as const;

export type InquiryDocumentExtension = keyof typeof INQUIRY_DOCUMENT_TYPES;
export type InquiryDocumentValidationCode =
  | "EMPTY_FILE"
  | "FILE_TOO_LARGE"
  | "INVALID_FILENAME"
  | "UNSUPPORTED_EXTENSION"
  | "MIME_MISMATCH"
  | "SIGNATURE_MISMATCH"
  | "INVALID_SHA256"
  | "INVALID_ID"
  | "INVALID_OBJECT_KEY";

export type InquiryDocumentValidationResult =
  | { ok: true; extension: InquiryDocumentExtension; normalizedFilename: string }
  | { ok: false; code: InquiryDocumentValidationCode; message: string };

export type InquiryDocumentCandidate = {
  filename: string;
  byteSize: number;
  declaredMime: string;
  detectedMime: string;
  sha256: string;
  /** Optional workflow-specific limit; desktop publication remains 10 MB by default. */
  maxByteSize?: number;
  /** Initial bytes read locally by the trusted bridge, never supplied by a browser. */
  signature: Uint8Array;
  /** Required for containers/text: XLSX entries or the complete CSV were safely parsed. */
  deepContentValidated?: boolean;
};

const CONTROL_OR_PATH = /[\u0000-\u001f\u007f/\\]/;
const WINDOWS_DEVICE = /^(con|prn|aux|nul|com[1-9]|lpt[1-9])(?:\.|$)/i;
const UUID = "[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}";
const OBJECT_KEY = new RegExp(`^inquiry-documents/${UUID}/${UUID}/${UUID}/${UUID}$`);

function fail(code: InquiryDocumentValidationCode, message: string): InquiryDocumentValidationResult {
  return { ok: false, code, message };
}

function extensionOf(filename: string): string {
  const index = filename.lastIndexOf(".");
  return index > 0 ? filename.slice(index).toLocaleLowerCase("en-US") : "";
}

function startsWith(bytes: Uint8Array, expected: number[]): boolean {
  return expected.every((value, index) => bytes[index] === value);
}

function hasSafeSignature(extension: InquiryDocumentExtension, bytes: Uint8Array): boolean {
  switch (extension) {
    case ".pdf": return startsWith(bytes, [0x25, 0x50, 0x44, 0x46, 0x2d]); // %PDF-
    case ".jpg":
    case ".jpeg": return startsWith(bytes, [0xff, 0xd8, 0xff]);
    case ".png": return startsWith(bytes, [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]);
    case ".gif": return startsWith(bytes, [0x47, 0x49, 0x46, 0x38]) && (bytes[4] === 0x37 || bytes[4] === 0x39) && bytes[5] === 0x61;
    case ".webp": return startsWith(bytes, [0x52, 0x49, 0x46, 0x46]) && bytes[8] === 0x57 && bytes[9] === 0x45 && bytes[10] === 0x42 && bytes[11] === 0x50;
    case ".bmp": return startsWith(bytes, [0x42, 0x4d]);
    case ".xlsx": return startsWith(bytes, [0x50, 0x4b, 0x03, 0x04]);
    case ".csv": {
      // CSV has no magic bytes. Reject binary controls except tab/newline/CR;
      // the bridge must also parse the complete file as bounded UTF-8 text.
      return bytes.length > 0 && [...bytes].every((byte) => byte === 0x09 || byte === 0x0a || byte === 0x0d || byte >= 0x20);
    }
  }
}

export function validateInquiryDocumentCandidate(candidate: InquiryDocumentCandidate): InquiryDocumentValidationResult {
  const filename = candidate.filename.normalize("NFKC").trim();
  if (!filename || filename.length > 260 || filename === "." || filename === ".." || CONTROL_OR_PATH.test(filename) || WINDOWS_DEVICE.test(filename)) {
    return fail("INVALID_FILENAME", "The document filename is unsafe or exceeds 260 characters.");
  }
  if (!Number.isSafeInteger(candidate.byteSize) || candidate.byteSize < 1) {
    return fail("EMPTY_FILE", "Empty documents cannot be published.");
  }
  const maxByteSize = candidate.maxByteSize ?? INQUIRY_DOCUMENT_MAX_BYTES;
  if (!Number.isSafeInteger(maxByteSize) || maxByteSize < 1 || candidate.byteSize > maxByteSize) {
    return fail("FILE_TOO_LARGE", `The document exceeds the ${Math.floor(maxByteSize / (1024 * 1024))} MB publication limit.`);
  }
  const extension = extensionOf(filename);
  if (!(extension in INQUIRY_DOCUMENT_TYPES)) {
    return fail("UNSUPPORTED_EXTENSION", "This document type is not approved for cloud publication.");
  }
  const typedExtension = extension as InquiryDocumentExtension;
  const detectedMime = candidate.detectedMime.trim().toLocaleLowerCase("en-US").split(";", 1)[0] ?? "";
  const declaredMime = candidate.declaredMime.trim().toLocaleLowerCase("en-US").split(";", 1)[0] ?? "";
  const acceptedMimes = INQUIRY_DOCUMENT_TYPES[typedExtension] as readonly string[];
  if (!acceptedMimes.includes(detectedMime) || !acceptedMimes.includes(declaredMime)) {
    return fail("MIME_MISMATCH", "Declared or detected content type does not match the approved extension policy.");
  }
  if (!hasSafeSignature(typedExtension, candidate.signature)) {
    return fail("SIGNATURE_MISMATCH", "Document signature does not match its extension.");
  }
  if ((typedExtension === ".xlsx" || typedExtension === ".csv") && candidate.deepContentValidated !== true) {
    return fail("SIGNATURE_MISMATCH", "Container and text documents require complete safe parsing before publication.");
  }
  if (!/^[a-f0-9]{64}$/.test(candidate.sha256)) {
    return fail("INVALID_SHA256", "Document integrity hash must be lowercase SHA-256.");
  }
  return { ok: true, extension: typedExtension, normalizedFilename: filename };
}

export function sha256Hex(bytes: Uint8Array): string {
  return createHash("sha256").update(bytes).digest("hex");
}

export function inquiryDocumentObjectKey(ids: {
  sourceDatasetId: string;
  inquiryId: string;
  documentId: string;
  versionId: string;
}): string {
  const key = `inquiry-documents/${ids.sourceDatasetId}/${ids.inquiryId}/${ids.documentId}/${ids.versionId}`.toLocaleLowerCase("en-US");
  if (!OBJECT_KEY.test(key)) throw new Error("Inquiry document object IDs must be canonical UUIDs.");
  return key;
}

export function isInquiryDocumentObjectKey(value: string): boolean {
  return OBJECT_KEY.test(value);
}

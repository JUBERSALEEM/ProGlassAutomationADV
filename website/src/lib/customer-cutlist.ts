import { unzipSync } from "fflate";

export type ImportedCutListRow = {
  specNumber: string;
  specName: string;
  glassType: string;
  reference: string;
  width1: string;
  height1: string;
  width2: string;
  height2: string;
  quantity: string;
};

export type CutListImportResult = {
  rows: ImportedCutListRow[];
  skipped: number;
};

type Matrix = string[][];

const MAX_IMPORT_BYTES = 10 * 1024 * 1024;
const MAX_IMPORT_ROWS = 120_000;

/** WPF Step 5 billable SQM: rounded to 3 dp using midpoint-to-even, then floored at 0.5 m². */
export function calculateWpfPanelSqm(widthMm: string | number, heightMm: string | number): number | null {
  const width = Number(widthMm);
  const height = Number(heightMm);
  if (!Number.isInteger(width) || !Number.isInteger(height) || width <= 0 || height <= 0 || width > 100_000 || height > 100_000) return null;
  const product = width * height;
  const lowerMilliSqm = Math.floor(product / 1_000);
  const remainder = product - lowerMilliSqm * 1_000;
  const roundedMilliSqm = remainder > 500 || (remainder === 500 && lowerMilliSqm % 2 === 1)
    ? lowerMilliSqm + 1
    : lowerMilliSqm;
  return Math.max(500, roundedMilliSqm) / 1_000;
}

/** WPF Step 5 Total SQM: billable panel SQM multiplied by the line quantity. */
export function calculateWpfTotalSqm(widthMm: string | number, heightMm: string | number, quantity: string | number): number | null {
  const panelSqm = calculateWpfPanelSqm(widthMm, heightMm);
  const count = Number(quantity);
  if (panelSqm === null || !Number.isInteger(count) || count <= 0 || count > 10_000) return null;
  return Math.round(panelSqm * count * 1_000) / 1_000;
}

function parseDelimited(text: string): Matrix {
  const firstLine = text.split(/\r?\n/, 1)[0] ?? "";
  const delimiters = [",", ";", "\t"];
  const detected = delimiters.reduce((best, candidate) => {
    const count = firstLine.split(candidate).length - 1;
    return count > best.count ? { value: candidate, count } : best;
  }, { value: ",", count: -1 });
  const source = text.replace(/^\uFEFF/, "");
  if (detected.count <= 0) {
    return source.split(/\r?\n/).map((line) => line.trim()).filter(Boolean).map((line) => line.split(/\s+/));
  }
  const delimiter = detected.value;
  const rows: Matrix = [];
  let row: string[] = [];
  let cell = "";
  let quoted = false;
  for (let index = 0; index < source.length; index += 1) {
    const character = source[index]!;
    if (character === '"') {
      if (quoted && source[index + 1] === '"') {
        cell += '"';
        index += 1;
      } else {
        quoted = !quoted;
      }
    } else if (!quoted && character === delimiter) {
      row.push(cell.trim());
      cell = "";
    } else if (!quoted && (character === "\n" || character === "\r")) {
      if (character === "\r" && source[index + 1] === "\n") index += 1;
      row.push(cell.trim());
      if (row.some((value) => value !== "")) rows.push(row);
      row = [];
      cell = "";
    } else {
      cell += character;
    }
  }
  row.push(cell.trim());
  if (row.some((value) => value !== "")) rows.push(row);
  return rows;
}

function xmlDocument(source: string): XMLDocument {
  const document = new DOMParser().parseFromString(source, "application/xml");
  if (document.getElementsByTagName("parsererror").length) throw new Error("The Excel file contains invalid workbook data.");
  return document;
}

function descendants(element: Document | Element, localName: string): Element[] {
  const namespaced = Array.from(element.getElementsByTagNameNS("*", localName));
  return namespaced.length ? namespaced : Array.from(element.getElementsByTagName(localName));
}

function firstText(element: Element | undefined, localName: string): string {
  if (!element) return "";
  const child = descendants(element, localName)[0];
  return child?.textContent?.trim() ?? "";
}

function columnNumber(reference: string): number {
  const letters = reference.match(/^[A-Z]+/i)?.[0]?.toUpperCase() ?? "A";
  return [...letters].reduce((value, letter) => value * 26 + letter.charCodeAt(0) - 64, 0) - 1;
}

function parseXlsx(bytes: Uint8Array): Matrix {
  const entries = unzipSync(bytes);
  const inflatedBytes = Object.values(entries).reduce((total, entry) => total + entry.byteLength, 0);
  if (inflatedBytes > 30 * 1024 * 1024) throw new Error("The uncompressed Excel workbook is too large to import safely.");
  const decode = (path: string) => {
    const entry = entries[path];
    return entry ? new TextDecoder().decode(entry) : "";
  };
  const workbookXml = decode("xl/workbook.xml");
  const workbookRelsXml = decode("xl/_rels/workbook.xml.rels");
  if (!workbookXml || !workbookRelsXml) throw new Error("The Excel workbook is missing its sheet index.");
  const workbook = xmlDocument(workbookXml);
  const relationships = xmlDocument(workbookRelsXml);
  const firstSheet = descendants(workbook, "sheet")[0];
  const relationshipId = firstSheet?.getAttributeNS("http://schemas.openxmlformats.org/officeDocument/2006/relationships", "id")
    || firstSheet?.getAttribute("r:id")
    || "";
  const relation = descendants(relationships, "Relationship").find((item) => item.getAttribute("Id") === relationshipId);
  const target = relation?.getAttribute("Target") ?? "worksheets/sheet1.xml";
  const normalizedTarget = target.replace(/^\/+/, "");
  if (normalizedTarget.split("/").some((part) => part === "..")) throw new Error("The Excel workbook contains an unsafe sheet path.");
  const sheetPath = normalizedTarget.startsWith("xl/") ? normalizedTarget : `xl/${normalizedTarget}`;
  const sheetXml = decode(sheetPath);
  if (!sheetXml) throw new Error("The Excel workbook does not contain a readable first sheet.");

  const sharedXml = decode("xl/sharedStrings.xml");
  const sharedStrings = sharedXml
    ? descendants(xmlDocument(sharedXml), "si").map((item) => descendants(item, "t").map((node) => node.textContent ?? "").join(""))
    : [];
  const worksheet = xmlDocument(sheetXml);
  return descendants(worksheet, "row").slice(0, MAX_IMPORT_ROWS + 1).map((row) => {
    const cells: string[] = [];
    for (const cell of descendants(row, "c")) {
      const reference = cell.getAttribute("r") ?? "A1";
      const index = columnNumber(reference);
      const type = cell.getAttribute("t");
      const value = type === "inlineStr"
        ? descendants(cell, "t").map((node) => node.textContent ?? "").join("")
        : firstText(cell, "v");
      cells[index] = type === "s" ? sharedStrings[Number(value)] ?? "" : value;
    }
    return cells.map((value) => value ?? "");
  });
}

function normalizeHeader(value: string): string {
  return value.toLocaleLowerCase().replace(/[^a-z0-9]/g, "");
}

function findColumn(headers: string[], aliases: string[]): number {
  return headers.findIndex((header) => aliases.includes(normalizeHeader(header)));
}

function get(row: string[], column: number): string {
  return column < 0 ? "" : (row[column] ?? "").trim();
}

function positiveDimension(value: string): boolean {
  const parsed = Number(value.replace(/,/g, ""));
  return Number.isFinite(parsed) && parsed > 0 && parsed <= 100_000;
}

function parseRows(matrix: Matrix): CutListImportResult {
  if (matrix.length < 1) throw new Error("The file has no cutting-list rows to import.");
  const headers = matrix[0] ?? [];
  const columns = {
    reference: findColumn(headers, ["glassref", "reference", "ref", "item", "itemref", "mark", "code", "panel"]),
    specName: findColumn(headers, ["specname", "specgroup", "group", "specification", "glassspec"]),
    specNumber: findColumn(headers, ["spec", "specnumber", "specno", "groupnumber"]),
    glassType: findColumn(headers, ["glasstype", "construction", "glassconstruction", "builduptype", "type", "glass"]),
    width1: findColumn(headers, ["w1", "w1mm", "width1", "width1mm", "width", "widthmm", "primarywidth"]),
    height1: findColumn(headers, ["h1", "h1mm", "height1", "height1mm", "height", "heightmm", "primaryheight"]),
    width2: findColumn(headers, ["w2", "w2mm", "width2", "width2mm", "secondarywidth"]),
    height2: findColumn(headers, ["h2", "h2mm", "height2", "height2mm", "secondaryheight"]),
    dimensions: findColumn(headers, ["size", "dim", "dimension", "dimensions", "wxh"]),
    quantity: findColumn(headers, ["qty", "quantity", "count", "pieces", "pcs"]),
  };
  const hasDimensionPairs = columns.width1 >= 0 && columns.height1 >= 0;
  const positional = !hasDimensionPairs && columns.dimensions < 0;
  if (positional && matrix[0]!.length < 2) throw new Error("Add W1/Width and H1/Height columns (or a W×H/Size column) to the cutting-list file, then try again.");
  const rows: ImportedCutListRow[] = [];
  let skipped = 0;
  const sourceRows = positional ? matrix.slice(0, MAX_IMPORT_ROWS) : matrix.slice(1, MAX_IMPORT_ROWS + 1);
  for (const source of sourceRows) {
    let reference = get(source, columns.reference);
    const specName = get(source, columns.specName) || (get(source, columns.specNumber) ? `Spec Group ${get(source, columns.specNumber)}` : "");
    const glassType = get(source, columns.glassType);
    let width1 = "";
    let height1 = "";
    let width2 = get(source, columns.width2).replace(/,/g, "");
    let height2 = get(source, columns.height2).replace(/,/g, "");
    let quantityValue = get(source, columns.quantity) || "1";

    if (positional) {
      const values = source.map((value) => value.trim()).filter(Boolean);
      let cursor = 0;
      const dimensionRegex = /^\s*(\d[\d,.]*)\s*[xX×*]\s*(\d[\d,.]*)\s*$/;
      const firstValue = values[cursor] ?? "";
      if (firstValue && !positiveDimension(firstValue) && !dimensionRegex.test(firstValue)) reference = values[cursor++]!;
      const pair = values[cursor]?.match(dimensionRegex);
      if (pair) {
        width1 = pair[1]!.replace(/,/g, "");
        height1 = pair[2]!.replace(/,/g, "");
        cursor += 1;
      } else {
        width1 = (values[cursor++] ?? "").replace(/,/g, "");
        height1 = (values[cursor++] ?? "").replace(/,/g, "");
        if (positiveDimension(values[cursor] ?? "") && positiveDimension(values[cursor + 1] ?? "") && values.length - cursor >= 3) {
          width2 = values[cursor++]!;
          height2 = values[cursor++]!;
        }
      }
      quantityValue = values[cursor] || "1";
    } else {
      const dimensionPair = get(source, columns.dimensions).match(/^\s*(\d[\d,.]*)\s*[xX×*]\s*(\d[\d,.]*)\s*$/);
      width1 = (hasDimensionPairs ? get(source, columns.width1) : dimensionPair?.[1] ?? "").replace(/,/g, "");
      height1 = (hasDimensionPairs ? get(source, columns.height1) : dimensionPair?.[2] ?? "").replace(/,/g, "");
    }

    if (!positiveDimension(width1) || !positiveDimension(height1)) {
      skipped += 1;
      continue;
    }
    const quantity = Number(quantityValue.replace(/,/g, ""));
    if (!Number.isInteger(quantity) || quantity < 1 || quantity > 10_000) {
      skipped += 1;
      continue;
    }
    const explicitSpecNumber = get(source, columns.specNumber);
    const inferredSpecNumber = specName.match(/^spec\s*group\s*(\d+)$/i)?.[1] ?? "";
    rows.push({
      specNumber: explicitSpecNumber || inferredSpecNumber,
      specName: specName.slice(0, 80),
      glassType: glassType.slice(0, 32),
      reference: reference.slice(0, 60) || `Imported ${rows.length + 1}`,
      width1,
      height1,
      width2: positiveDimension(width2) ? width2 : "",
      height2: positiveDimension(height2) ? height2 : "",
      quantity: String(quantity),
    });
  }
  if (rows.length === 0) throw new Error("No valid cutting-list rows were found. Check the dimensions and quantities.");
  return { rows, skipped: skipped + Math.max(0, matrix.length - sourceRows.length - (positional ? 0 : 1)) };
}

export async function parseCustomerCutListFile(file: File): Promise<CutListImportResult> {
  if (file.size <= 0 || file.size > MAX_IMPORT_BYTES) throw new Error("Choose a non-empty CSV, TXT, or Excel file up to 10 MB.");
  const extension = file.name.toLocaleLowerCase().split(".").pop();
  const bytes = new Uint8Array(await file.arrayBuffer());
  if (extension === "xlsx") return parseRows(parseXlsx(bytes));
  if (extension !== "csv" && extension !== "txt") throw new Error("Choose a CSV, TXT, or Excel (.xlsx) cutting-list file.");
  return parseRows(parseDelimited(new TextDecoder().decode(bytes)));
}

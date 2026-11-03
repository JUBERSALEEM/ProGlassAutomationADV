import {
  Document,
  Page,
  StyleSheet,
  Text,
  View,
  pdf,
} from "@react-pdf/renderer";
import { createCsv, createXlsx } from "@/lib/export-formats";
import type { ExportSheet, OperationsExportBundle } from "@/lib/operations-exports";

export type CustomerPiAttachmentInput = {
  companyName: string;
  contactName: string;
  customerReference: string;
  projectName: string;
  projectLocation: string;
  projectRequirements: string;
  technicalSpecification: string;
  technicalSpecificationJson: string;
};

type Pane = { thickness?: string; colour?: string; treatment?: string; sheetWidth?: string; sheetHeight?: string };
type SpecGroup = {
  id?: string;
  specName?: string;
  kind?: string;
  panes?: Pane[];
  spacerWidth?: string;
  spacerMaterial?: string;
  interlayerType?: string;
  interlayerThickness?: string;
  interlayerColour?: string;
  panelExtra?: string;
  spacerExtra?: string;
  edgeFinish?: string;
  fabricationNotes?: string;
};
type CuttingRow = { reference?: string; specId?: string; width1?: string; height1?: string; width2?: string; height2?: string; quantity?: string };
type ParsedSpec = { specs: SpecGroup[]; rows: CuttingRow[]; specNotes: string };
type FlatRow = Record<string, unknown>;

function asText(value: unknown): string { return typeof value === "string" || typeof value === "number" ? String(value) : ""; }
function parseSpec(value: string): ParsedSpec {
  try {
    const parsed = JSON.parse(value) as Partial<ParsedSpec>;
    return {
      specs: Array.isArray(parsed.specs) ? parsed.specs.slice(0, 12) as SpecGroup[] : [],
      rows: Array.isArray(parsed.rows) ? parsed.rows.slice(0, 250) as CuttingRow[] : [],
      specNotes: asText(parsed.specNotes).slice(0, 1000),
    };
  } catch {
    return { specs: [], rows: [], specNotes: "" };
  }
}

function groupNumber(specs: SpecGroup[], id: string | undefined) {
  const index = specs.findIndex((spec) => spec.id === id);
  return index < 0 ? 1 : index + 1;
}
function buildRows(input: CustomerPiAttachmentInput, parsed: ParsedSpec) {
  const customerRows: FlatRow[] = [
    { field: "Request", value: "Customer proforma invoice inquiry — technical specification only" },
    { field: "Company", value: input.companyName },
    { field: "Contact", value: input.contactName },
    { field: "Customer reference", value: input.customerReference },
    { field: "Project", value: input.projectName },
    { field: "Project location", value: input.projectLocation },
    { field: "Project requirements", value: input.projectRequirements },
    { field: "Additional technical notes", value: parsed.specNotes },
  ];
  const glassRows: FlatRow[] = [];
  parsed.specs.forEach((spec, index) => {
    const group = `Spec Group ${index + 1}`;
    const panes = Array.isArray(spec.panes) ? spec.panes.slice(0, 3) : [];
    const paneLabels = spec.kind === "SGU" ? ["Glass pane"] : spec.kind === "DGU + LAM" ? ["Outer DGU pane", "Laminate outer glass", "Laminate inner glass"] : spec.kind === "LAM" ? ["Outer glass", "Inner glass"] : ["Outer pane", "Inner pane"];
    panes.slice(0, paneLabels.length).forEach((pane, paneIndex) => {
      glassRows.push({
        "Spec group": group,
        "Specification name": asText(spec.specName),
        Construction: asText(spec.kind),
        Pane: paneLabels[paneIndex] || `Pane ${paneIndex + 1}`,
        "Thickness (mm)": asText(pane.thickness),
        Colour: asText(pane.colour),
        Treatment: asText(pane.treatment),
        "Stock sheet width (mm)": asText(pane.sheetWidth),
        "Stock sheet height (mm)": asText(pane.sheetHeight),
        "Spacer width": (spec.kind === "DGU" || spec.kind === "DGU + LAM") ? asText(spec.spacerWidth) : "",
        "Spacer material": (spec.kind === "DGU" || spec.kind === "DGU + LAM") ? asText(spec.spacerMaterial) : "",
        Interlayer: (spec.kind === "LAM" || spec.kind === "DGU + LAM") ? `${asText(spec.interlayerType)} ${asText(spec.interlayerThickness)} ${asText(spec.interlayerColour)}`.trim() : "",
        "Panel extra / overlap": asText(spec.panelExtra),
        "Spacer extra": (spec.kind === "DGU" || spec.kind === "DGU + LAM") ? asText(spec.spacerExtra) : "",
        "Edge finish": asText(spec.edgeFinish),
        "Fabrication details": asText(spec.fabricationNotes),
      });
    });
  });
  const cuttingRows = parsed.rows.map((row, index) => {
    const groupIndex = groupNumber(parsed.specs, row.specId) - 1;
    return {
      Line: index + 1,
      "Glass reference": asText(row.reference) || `Item ${index + 1}`,
      "Spec group": `Spec Group ${groupIndex + 1}`,
      "Specification name": asText(parsed.specs[groupIndex]?.specName),
      Construction: asText(parsed.specs[groupIndex]?.kind),
      "W1 (mm)": asText(row.width1),
      "H1 (mm)": asText(row.height1),
      "W2 (mm)": asText(row.width2),
      "H2 (mm)": asText(row.height2),
      Quantity: asText(row.quantity),
    };
  });
  return { customerRows, glassRows, cuttingRows };
}

function describeGlassRow(row: FlatRow) {
  const stockSize = [row["Stock sheet width (mm)"], row["Stock sheet height (mm)"]].map(asText).filter(Boolean).join(" × ");
  const spacer = [row["Spacer width"], row["Spacer material"]].map(asText).filter(Boolean).join(" ");
  return [
    asText(row["Specification name"]) ? `Specification ${asText(row["Specification name"])}` : "",
    asText(row["Thickness (mm)"]),
    asText(row.Colour),
    asText(row.Treatment),
    stockSize ? `Stock sheet ${stockSize} mm` : "",
    spacer ? `Spacer ${spacer}` : "",
    row.Interlayer ? `Interlayer ${asText(row.Interlayer)}` : "",
    asText(row["Panel extra / overlap"]),
    asText(row["Spacer extra"]),
    asText(row["Edge finish"]),
    asText(row["Fabrication details"]),
  ].filter(Boolean).join(" · ");
}

function sheet(name: string, rows: FlatRow[], columns?: string[]): ExportSheet {
  const keys = columns || [...new Set(rows.flatMap((row) => Object.keys(row)))];
  const normalized = keys.length ? keys : ["Field", "Value"];
  return {
    name,
    columns: normalized.map((key) => ({ key, header: key })),
    rows: rows.length ? rows : [Object.fromEntries(normalized.map((key) => [key, ""]))],
  };
}

function makeBundle(input: CustomerPiAttachmentInput, parsed: ParsedSpec): OperationsExportBundle {
  const rows = buildRows(input, parsed);
  const customerSheet = sheet("Customer & Project", rows.customerRows, ["field", "value"]);
  const glassSheet = sheet("Glass Specifications", rows.glassRows, ["Spec group", "Specification name", "Construction", "Pane", "Thickness (mm)", "Colour", "Treatment", "Stock sheet width (mm)", "Stock sheet height (mm)", "Spacer width", "Spacer material", "Interlayer", "Panel extra / overlap", "Spacer extra", "Edge finish", "Fabrication details"]);
  const cuttingSheet = sheet("Cutting List", rows.cuttingRows, ["Line", "Glass reference", "Spec group", "Specification name", "Construction", "W1 (mm)", "H1 (mm)", "W2 (mm)", "H2 (mm)", "Quantity"]);
  const flatRows: FlatRow[] = [
    ...rows.customerRows.map((row) => ({ "Record type": "Customer / project", "Spec group": "", "Specification name": "", Reference: "", Construction: "", Pane: "", Field: row.field, Value: row.value, "W1 (mm)": "", "H1 (mm)": "", "W2 (mm)": "", "H2 (mm)": "", Quantity: "" })),
    ...rows.glassRows.map((row) => ({ "Record type": "Glass specification", "Spec group": row["Spec group"], "Specification name": row["Specification name"], Reference: "", Construction: row.Construction, Pane: row.Pane, Field: "", Value: describeGlassRow(row), "W1 (mm)": "", "H1 (mm)": "", "W2 (mm)": "", "H2 (mm)": "", Quantity: "" })),
    ...rows.cuttingRows.map((row) => ({ "Record type": "Cutting list", "Spec group": row["Spec group"], "Specification name": row["Specification name"], Reference: row["Glass reference"], Construction: row.Construction, Pane: "", Field: "", Value: "", "W1 (mm)": row["W1 (mm)"], "H1 (mm)": row["H1 (mm)"], "W2 (mm)": row["W2 (mm)"], "H2 (mm)": row["H2 (mm)"], Quantity: row.Quantity })),
  ];
  const csvSheet = sheet("PI Technical Request", flatRows, ["Record type", "Spec group", "Specification name", "Reference", "Construction", "Pane", "Field", "Value", "W1 (mm)", "H1 (mm)", "W2 (mm)", "H2 (mm)", "Quantity"]);
  const generatedAt = new Date().toISOString();
  return {
    schemaVersion: 1,
    report: "proformas",
    title: "Customer PI Technical Request — No Pricing",
    generatedAt,
    rowCount: rows.glassRows.length + rows.cuttingRows.length,
    filters: {},
    summary: { "Spec groups": parsed.specs.length, "Cutting list lines": parsed.rows.length },
    sheets: [customerSheet, glassSheet, cuttingSheet],
    csvSheet,
  };
}

const styles = StyleSheet.create({
  page: { padding: 34, fontFamily: "Helvetica", fontSize: 9, color: "#213547" },
  title: { fontSize: 18, fontWeight: "bold", color: "#102a43", marginBottom: 4 },
  subtitle: { color: "#52687a", fontSize: 8, marginBottom: 12 },
  notice: { backgroundColor: "#e9f5f6", color: "#14515b", padding: 8, marginBottom: 12, borderRadius: 3, fontWeight: "bold" },
  heading: { fontSize: 12, fontWeight: "bold", color: "#102a43", marginTop: 8, marginBottom: 5 },
  block: { borderWidth: 1, borderColor: "#d7e0e8", borderRadius: 3, padding: 7, marginBottom: 7 },
  row: { flexDirection: "row", paddingVertical: 3, borderBottomWidth: 0.5, borderBottomColor: "#e3e9ee" },
  label: { width: "34%", fontWeight: "bold", color: "#334e68" },
  value: { width: "66%", lineHeight: 1.35 },
  line: { marginBottom: 4, lineHeight: 1.35 },
  tableHeader: { flexDirection: "row", backgroundColor: "#16324f", paddingVertical: 5, paddingHorizontal: 3 },
  tableHeaderCell: { width: "14.3%", color: "#ffffff", fontSize: 7, fontWeight: "bold" },
  tableRow: { flexDirection: "row", paddingVertical: 4, paddingHorizontal: 3, borderBottomWidth: 0.5, borderBottomColor: "#d7e0e8" },
  tableCell: { width: "14.3%", fontSize: 8, lineHeight: 1.25 },
  tableCellNarrow: { width: "7%", fontSize: 8, lineHeight: 1.25 },
  footer: { position: "absolute", bottom: 18, left: 34, right: 34, color: "#607789", fontSize: 7, borderTopWidth: 0.5, borderTopColor: "#d7e0e8", paddingTop: 5 },
});

function PiRequestPdf({ parsed, bundle }: { parsed: ParsedSpec; bundle: OperationsExportBundle }) {
  const customer = bundle.sheets[0]!;
  const glass = bundle.sheets[1]!;
  const cutting = bundle.sheets[2]!;
  return <Document title="Customer PI Technical Request — No Pricing" author="ProGlass Automation" subject="Customer-submitted technical specification">
    <Page size="A4" style={styles.page} wrap>
      <Text style={styles.title}>PI Technical Specification Request</Text>
      <Text style={styles.subtitle}>Customer-generated inquiry summary · for estimator preparation · no pricing</Text>
      <Text style={styles.notice}>TECHNICAL REQUEST ONLY — NO RATE, CHARGE, MARGIN, PROFIT, OR PI VALUE</Text>
      <Text style={styles.heading}>Customer &amp; Project</Text>
      <View style={styles.block}>{customer.rows.map((row, index) => <View key={index} style={styles.row} wrap={false}><Text style={styles.label}>{String(row.field)}</Text><Text style={styles.value}>{String(row.value || "—")}</Text></View>)}</View>
      <Text style={styles.heading}>Glass Specifications</Text>
      {parsed.specs.map((spec, specIndex) => <View key={spec.id || specIndex} style={styles.block} wrap>
        <Text style={styles.line}>Spec Group {specIndex + 1} · {asText(spec.kind)}{spec.specName ? ` · ${asText(spec.specName)}` : ""}</Text>
        {glass.rows.filter((row) => row["Spec group"] === `Spec Group ${specIndex + 1}`).map((row, index) => <Text key={index} style={styles.line}>• {String(row.Pane)}: {String(row["Thickness (mm)"])} · {String(row.Colour)} · {String(row.Treatment)} · sheet {String(row["Stock sheet width (mm)"] || "—")} × {String(row["Stock sheet height (mm)"] || "—")} mm</Text>)}
        {(spec.kind === "DGU" || spec.kind === "DGU + LAM") && <Text style={styles.line}>Spacer: {asText(spec.spacerWidth)} · {asText(spec.spacerMaterial)} · {asText(spec.spacerExtra)}</Text>}
        {(spec.kind === "LAM" || spec.kind === "DGU + LAM") && <Text style={styles.line}>Interlayer: {asText(spec.interlayerType)} · {asText(spec.interlayerThickness)} · {asText(spec.interlayerColour)}</Text>}
        <Text style={styles.line}>Fabrication: {asText(spec.panelExtra)} · {asText(spec.edgeFinish)} · {asText(spec.fabricationNotes)}</Text>
      </View>)}
      <Text style={styles.heading}>Cutting List · Dimensions in mm</Text>
      <View style={styles.block}>{cutting.rows.map((row, index) => <Text key={index} style={styles.line}>{String(row.Line)}. {String(row["Glass reference"])} · {String(row["Spec group"])}{row["Specification name"] ? ` · ${String(row["Specification name"])}` : ""} · {String(row["W1 (mm)"])} × {String(row["H1 (mm)"])} mm{row["W2 (mm)"] || row["H2 (mm)"] ? ` · secondary ${String(row["W2 (mm)"] || "—")} × ${String(row["H2 (mm)"] || "—")} mm` : ""} · Qty {String(row.Quantity)}</Text>)}</View>
      <Text style={styles.footer}>Generated from the customer PI inquiry. Estimate, verify technical suitability, and prepare any PI in the ProGlass desktop workflow.</Text>
    </Page>
  </Document>;
}

function CustomerCutListPdf({ parsed, bundle }: { parsed: ParsedSpec; bundle: OperationsExportBundle }) {
  const cutting = bundle.sheets[2]!;
  return <Document title="Customer Technical Cutting List — No Pricing" author="ProGlass Automation" subject="Customer-provided cut-list dimensions">
    <Page size="A4" orientation="landscape" style={styles.page} wrap>
      <Text style={styles.title}>Customer Technical Cutting List</Text>
      <Text style={styles.subtitle}>Dimensions in millimetres · planning input for estimator verification · {parsed.specs.length} specification groups</Text>
      <Text style={styles.notice}>TECHNICAL CUT LIST ONLY — NO PRICES, COSTS, YIELD, MARGINS, OR P&amp;L</Text>
      <View style={styles.block}>
        <View style={styles.tableHeader} wrap={false}>
          {["Line", "Glass reference", "Specification", "Type", "W1 × H1 (mm)", "W2 × H2 (mm)", "Qty"].map((label) => <Text key={label} style={styles.tableHeaderCell}>{label}</Text>)}
        </View>
        {cutting.rows.map((row, index) => <View key={index} style={styles.tableRow} wrap={false}>
          <Text style={styles.tableCellNarrow}>{String(row.Line)}</Text>
          <Text style={styles.tableCell}>{String(row["Glass reference"] || "—")}</Text>
          <Text style={styles.tableCell}>{String(row["Specification name"] || row["Spec group"] || "—")}</Text>
          <Text style={styles.tableCellNarrow}>{String(row.Construction || "—")}</Text>
          <Text style={styles.tableCell}>{String(row["W1 (mm)"])} × {String(row["H1 (mm)"])}</Text>
          <Text style={styles.tableCell}>{row["W2 (mm)"] || row["H2 (mm)"] ? `${String(row["W2 (mm)"] || "—")} × ${String(row["H2 (mm)"] || "—")}` : "—"}</Text>
          <Text style={styles.tableCellNarrow}>{String(row.Quantity)}</Text>
        </View>)}
      </View>
      <Text style={styles.footer}>Customer planning layout only. ProGlass confirms dimensions, edge allowances, glass suitability and the final production cutting plan in the WPF workflow.</Text>
    </Page>
  </Document>;
}

export async function generateCustomerPiCutListPdf(input: CustomerPiAttachmentInput): Promise<File> {
  const parsed = parseSpec(input.technicalSpecificationJson);
  if (!parsed.specs.length || !parsed.rows.length) throw new Error("Add a valid technical cutting-list row before exporting the PDF.");
  const bundle = makeBundle(input, parsed);
  const pdfBlob = await pdf(<CustomerCutListPdf parsed={parsed} bundle={bundle} />).toBlob();
  if (!pdfBlob.size || pdfBlob.size > 10 * 1024 * 1024) throw new Error("The technical cut-list PDF is empty or exceeds 10 MB.");
  return new File([pdfBlob], "ProGlass_Customer_Cut_List.pdf", { type: "application/pdf", lastModified: Date.now() });
}

function blobFile(bytes: Uint8Array, name: string, type: string) {
  const buffer = bytes.buffer.slice(bytes.byteOffset, bytes.byteOffset + bytes.byteLength) as ArrayBuffer;
  return new File([buffer], name, { type, lastModified: Date.now() });
}

export async function generateCustomerPiAttachments(input: CustomerPiAttachmentInput): Promise<File[]> {
  const parsed = parseSpec(input.technicalSpecificationJson);
  if (!parsed.specs.length || !parsed.rows.length) throw new Error("The PI technical specification is incomplete; finish Steps 4–7 before generating attachments.");
  const bundle = makeBundle(input, parsed);
  const filenameBase = "ProGlass_PI_Technical_Specification_Auto";
  const csv = createCsv(bundle.csvSheet);
  const xlsx = createXlsx(bundle);
  const pdfBlob = await pdf(<PiRequestPdf parsed={parsed} bundle={bundle} />).toBlob();
  if (!pdfBlob.size || pdfBlob.size > 10 * 1024 * 1024) throw new Error("The generated PI PDF is empty or exceeds 10 MB.");
  return [
    blobFile(xlsx, `${filenameBase}.xlsx`, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"),
    blobFile(csv, `${filenameBase}.csv`, "text/csv;charset=utf-8"),
    new File([pdfBlob], `${filenameBase}.pdf`, { type: "application/pdf", lastModified: Date.now() }),
  ];
}

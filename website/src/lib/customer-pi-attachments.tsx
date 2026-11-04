import { Document, Page, StyleSheet, Text, View, pdf } from "@react-pdf/renderer";

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

export type CustomerPiInquiryPdfInput = {
  companyName: string;
  contactName: string;
  customerReference: string;
  projectName: string;
  projectLocation: string;
  projectRequirements: string;
  technicalSpecificationJson: string;
};

function asText(value: unknown): string {
  return typeof value === "string" || typeof value === "number" ? String(value) : "";
}

function parseSpec(value: string): ParsedSpec {
  try {
    const parsed = JSON.parse(value) as Partial<ParsedSpec>;
    return {
      specs: Array.isArray(parsed.specs) ? parsed.specs.slice(0, 12) as SpecGroup[] : [],
      rows: Array.isArray(parsed.rows) ? parsed.rows.slice(0, 120_000) as CuttingRow[] : [],
      specNotes: asText(parsed.specNotes).slice(0, 1000),
    };
  } catch {
    return { specs: [], rows: [], specNotes: "" };
  }
}

const paneNames: Record<string, string[]> = {
  SGU: ["Glass pane"],
  DGU: ["Outer pane", "Inner pane"],
  LAM: ["Outer glass", "Inner glass"],
  "DGU + LAM": ["Outer DGU pane", "Laminate outer glass", "Laminate inner glass"],
};

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
  tableHeaderCell: { color: "#ffffff", fontSize: 7, fontWeight: "bold" },
  tableRow: { flexDirection: "row", paddingVertical: 4, paddingHorizontal: 3, borderBottomWidth: 0.5, borderBottomColor: "#d7e0e8" },
  tableCell: { fontSize: 7, lineHeight: 1.25 },
  referenceCell: { width: "17%" },
  groupCell: { width: "15%" },
  dimensionsCell: { width: "22%" },
  quantityCell: { width: "10%" },
  footer: { position: "absolute", bottom: 18, left: 34, right: 34, color: "#607789", fontSize: 7, borderTopWidth: 0.5, borderTopColor: "#d7e0e8", paddingTop: 5 },
});

function PiInquiryPdf({ input, parsed }: { input: CustomerPiInquiryPdfInput; parsed: ParsedSpec }) {
  return <Document title="Customer PI Inquiry — Technical Only" author="ProGlass Automation" subject="Unpriced customer inquiry">
    <Page size="A4" style={styles.page} wrap>
      <Text style={styles.title}>Customer Inquiry · Technical Specification</Text>
      <Text style={styles.subtitle}>Submitted project and glass requirements for estimator review</Text>
      <Text style={styles.notice}>UNPRICED INQUIRY — NO RATES, COSTS, MARGINS, PROFIT OR PI VALUE</Text>
      <Text style={styles.heading}>Customer &amp; Project</Text>
      <View style={styles.block}>
        {[
          ["Company", input.companyName],
          ["Contact", input.contactName],
          ["Customer reference", input.customerReference],
          ["Project", input.projectName],
          ["Project location", input.projectLocation],
          ["Project requirements", input.projectRequirements],
          ["Additional technical notes", parsed.specNotes],
        ].map(([label, value], index) => <View key={index} style={styles.row} wrap={false}><Text style={styles.label}>{label}</Text><Text style={styles.value}>{value || "—"}</Text></View>)}
      </View>
      <Text style={styles.heading}>Glass Specifications</Text>
      {parsed.specs.map((spec, specIndex) => {
        const names = paneNames[asText(spec.kind)] ?? ["Pane"];
        const panes = Array.isArray(spec.panes) ? spec.panes.slice(0, names.length) : [];
        return <View key={spec.id || specIndex} style={styles.block} wrap>
          <Text style={styles.line}>Spec Group {specIndex + 1} · {asText(spec.kind)}{spec.specName ? ` · ${asText(spec.specName)}` : ""}</Text>
          {panes.map((pane, paneIndex) => <Text key={paneIndex} style={styles.line}>• {names[paneIndex]}: {asText(pane.thickness)} · {asText(pane.colour)} · {asText(pane.treatment)} · stock sheet {asText(pane.sheetWidth) || "—"} × {asText(pane.sheetHeight) || "—"} mm</Text>)}
          {(spec.kind === "DGU" || spec.kind === "DGU + LAM") && <Text style={styles.line}>Spacer: {asText(spec.spacerWidth)} · {asText(spec.spacerMaterial)} · {asText(spec.spacerExtra)}</Text>}
          {(spec.kind === "LAM" || spec.kind === "DGU + LAM") && <Text style={styles.line}>Interlayer: {asText(spec.interlayerType)} · {asText(spec.interlayerThickness)} · {asText(spec.interlayerColour)}</Text>}
          <Text style={styles.line}>Fabrication: {asText(spec.panelExtra)} · {asText(spec.edgeFinish)} · {asText(spec.fabricationNotes)}</Text>
        </View>;
      })}
      <Text style={styles.heading}>Cutting List · Dimensions in mm</Text>
      <View style={styles.block}>
        <View style={styles.tableHeader} wrap={false}>
          <Text style={[styles.tableHeaderCell, styles.referenceCell]}>Glass reference</Text>
          <Text style={[styles.tableHeaderCell, styles.groupCell]}>Specification</Text>
          <Text style={[styles.tableHeaderCell, styles.dimensionsCell]}>W1 × H1</Text>
          <Text style={[styles.tableHeaderCell, styles.dimensionsCell]}>W2 × H2</Text>
          <Text style={[styles.tableHeaderCell, styles.quantityCell]}>Qty</Text>
        </View>
        {parsed.rows.map((row, index) => {
          const groupIndex = Math.max(0, parsed.specs.findIndex((spec) => spec.id === row.specId));
          const spec = parsed.specs[groupIndex];
          return <View key={index} style={styles.tableRow} wrap={false}>
            <Text style={[styles.tableCell, styles.referenceCell]}>{asText(row.reference) || `Item ${index + 1}`}</Text>
            <Text style={[styles.tableCell, styles.groupCell]}>{asText(spec?.specName) || `Group ${groupIndex + 1}`}</Text>
            <Text style={[styles.tableCell, styles.dimensionsCell]}>{asText(row.width1)} × {asText(row.height1)}</Text>
            <Text style={[styles.tableCell, styles.dimensionsCell]}>{row.width2 || row.height2 ? `${asText(row.width2) || "—"} × ${asText(row.height2) || "—"}` : "—"}</Text>
            <Text style={[styles.tableCell, styles.quantityCell]}>{asText(row.quantity)}</Text>
          </View>;
        })}
      </View>
      <Text style={styles.footer}>This unpriced inquiry records customer requirements only. ProGlass staff review technical suitability and prepare any priced PI in the authorized internal workflow.</Text>
    </Page>
  </Document>;
}

export async function generateCustomerPiInquiryPdf(input: CustomerPiInquiryPdfInput): Promise<File> {
  const parsed = parseSpec(input.technicalSpecificationJson);
  if (!parsed.specs.length || !parsed.rows.length) throw new Error("Complete the glass specification and cutting list before generating the inquiry PDF.");
  const pdfBlob = await pdf(<PiInquiryPdf input={input} parsed={parsed} />).toBlob();
  if (!pdfBlob.size || pdfBlob.size > 10 * 1024 * 1024) throw new Error("The generated inquiry PDF is empty or exceeds 10 MB.");
  return new File([pdfBlob], "ProGlass_PI_Technical_Inquiry.pdf", { type: "application/pdf", lastModified: Date.now() });
}

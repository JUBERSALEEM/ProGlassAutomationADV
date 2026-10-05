import {
  Document,
  Page,
  StyleSheet,
  Text,
  View,
  renderToBuffer,
} from "@react-pdf/renderer";
import type { ExportSheet, OperationsExportBundle } from "@/lib/operations-exports";

const colors = {
  ink: "#102a43",
  brand: "#159eaa",
  pale: "#e9f5f6",
  line: "#d7e0e8",
  text: "#243746",
  muted: "#5d6d7e",
  white: "#ffffff",
};

const styles = StyleSheet.create({
  page: {
    paddingTop: 46,
    paddingBottom: 38,
    paddingHorizontal: 28,
    fontFamily: "Helvetica",
    fontSize: 7,
    color: colors.text,
    backgroundColor: colors.white,
  },
  runningHeader: {
    position: "absolute",
    top: 16,
    left: 28,
    right: 28,
    flexDirection: "row",
    justifyContent: "space-between",
    borderBottomWidth: 1,
    borderBottomColor: colors.line,
    paddingBottom: 5,
  },
  brandName: { fontSize: 8, fontWeight: "bold", color: colors.ink, letterSpacing: 0.6 },
  reportLabel: { fontSize: 7, color: colors.muted },
  footer: {
    position: "absolute",
    bottom: 15,
    left: 28,
    right: 28,
    flexDirection: "row",
    justifyContent: "space-between",
    borderTopWidth: 1,
    borderTopColor: colors.line,
    paddingTop: 5,
    color: colors.muted,
    fontSize: 6,
  },
  title: { fontSize: 17, fontWeight: "bold", color: colors.ink, marginBottom: 4 },
  subtitle: { fontSize: 7, color: colors.muted, marginBottom: 3 },
  gate: { fontSize: 7, color: colors.brand, fontWeight: "bold", marginBottom: 9 },
  block: { marginBottom: 12 },
  sectionTitle: { fontSize: 9, fontWeight: "bold", color: colors.ink, marginBottom: 5 },
  summaryGrid: { flexDirection: "row", flexWrap: "wrap", gap: 5 },
  summaryCard: {
    minWidth: 105,
    flexGrow: 1,
    borderWidth: 1,
    borderColor: colors.line,
    borderRadius: 4,
    backgroundColor: "#f7fafc",
    paddingVertical: 5,
    paddingHorizontal: 7,
  },
  summaryLabel: { fontSize: 5.8, color: colors.muted, marginBottom: 2 },
  summaryValue: { fontSize: 8, fontWeight: "bold", color: colors.ink },
  filters: { flexDirection: "row", flexWrap: "wrap", gap: 3 },
  filterItem: { fontSize: 6, color: colors.text, marginRight: 10, marginBottom: 3 },
  table: { width: "100%", borderWidth: 1, borderColor: colors.line, borderBottomWidth: 0 },
  tableHeader: { flexDirection: "row", backgroundColor: colors.ink, minHeight: 22 },
  tableRow: { flexDirection: "row", borderBottomWidth: 1, borderBottomColor: colors.line, minHeight: 18 },
  tableRowAlt: { backgroundColor: "#f5f8fa" },
  cell: { paddingVertical: 4, paddingHorizontal: 3, justifyContent: "center" },
  headerCell: { color: colors.white, fontSize: 5.4, fontWeight: "bold" },
  bodyCell: { color: colors.text, fontSize: 5.5 },
  empty: { padding: 12, textAlign: "center", color: colors.muted, fontSize: 8 },
});

const PDF_COLUMNS: Record<string, string[]> = {
  proformas: ["date", "pi_number", "customer_ref", "company", "qty", "sqm", "status", "pi_status"],
  "job-orders": ["order_date", "pi_number", "customer_ref", "company", "type_of_work", "ordered_qty", "ordered_sqm", "record_count", "confirmation_status", "job_order_number", "production_status", "stage_progress", "stage_totals"],
  deliveries: [
    "pi_number", "company", "order_date", "ordered_qty", "delivered_qty", "balance_qty",
    "ordered_sqm", "delivered_sqm", "balance_sqm", "delivery_status", "delivery_count",
  ],
};

const MOVEMENT_COLUMNS = [
  "pi_number", "delivery_note_ref", "delivery_date", "sequence", "delivered_qty", "delivered_sqm",
  "delivery_type", "status", "driver_name", "vehicle_number", "counts_toward_balance", "source_file",
];

const WIDTHS: Record<string, number> = {
  date: 8, order_date: 8, latest_order_date: 8,
  pi_number: 10, customer_ref: 11, company: 17, salesman: 10, prepared_by: 10,
  type_of_work: 8, qty: 6, sqm: 7, status: 9, pi_status: 8, zoho_status: 8,
  ordered_qty: 7, delivered_qty: 7, balance_qty: 7,
  ordered_sqm: 7, delivered_sqm: 7, balance_sqm: 7,
  record_count: 6, confirmation_status: 9, job_order_number: 12, production_status: 9, stage_progress: 10, stage_totals: 28,
  delivery_status: 10, delivery_count: 7,
  delivery_note_ref: 11, sequence: 5, delivery_date: 8, delivery_time: 7,
  delivery_type: 8, driver_name: 9, vehicle_number: 8, counts_toward_balance: 8, source_file: 11,
};

function titleCase(value: string): string {
  return value.replaceAll("_", " ").replace(/\b\w/g, (letter) => letter.toUpperCase());
}

function textValue(value: unknown): string {
  if (value === null || value === undefined || value === "") return "—";
  if (typeof value === "boolean") return value ? "Yes" : "No";
  if (Array.isArray(value)) return value.join(", ");
  return String(value);
}

function visibleColumns(bundle: OperationsExportBundle, sheet: ExportSheet): string[] {
  if (bundle.report === "deliveries" && sheet.name === "Delivery Movements") return MOVEMENT_COLUMNS;
  return PDF_COLUMNS[bundle.report] || sheet.columns.map((column) => column.key);
}

function tableForSheet(bundle: OperationsExportBundle, sheet: ExportSheet) {
  const keys = visibleColumns(bundle, sheet);
  const byKey = new Map(sheet.columns.map((column) => [column.key, column]));
  const columns = keys
    .filter((key) => byKey.has(key))
    .map((key) => ({ key, header: byKey.get(key)?.header || titleCase(key), width: WIDTHS[key] ?? 8 }));
  const totalWidth = columns.reduce((sum, column) => sum + column.width, 0) || 1;

  return (
    <View style={styles.table}>
      <View style={styles.tableHeader} wrap={false} fixed>
        {columns.map((column) => (
          <View key={column.key} style={[styles.cell, { width: `${(column.width / totalWidth) * 100}%` }]}>
            <Text style={styles.headerCell}>{column.header}</Text>
          </View>
        ))}
      </View>
      {sheet.rows.length === 0 ? (
        <Text style={styles.empty}>No rows match the current filters.</Text>
      ) : sheet.rows.map((row, rowIndex) => (
        <View
          key={`${sheet.name}-${rowIndex}`}
          style={[styles.tableRow, ...(rowIndex % 2 === 1 ? [styles.tableRowAlt] : [])]}
          wrap={false}
        >
          {columns.map((column) => (
            <View key={column.key} style={[styles.cell, { width: `${(column.width / totalWidth) * 100}%` }]}>
              <Text style={styles.bodyCell}>{textValue(row[column.key])}</Text>
            </View>
          ))}
        </View>
      ))}
    </View>
  );
}

function reportGate(report: OperationsExportBundle["report"]): string {
  if (report === "job-orders") return "Source gate: active Proforma Invoice records with exact status Confirmed only.";
  if (report === "deliveries") return "Delivery balances are based on the filtered confirmed-PI tracker and its movement ledger.";
  return "Source: filtered active Proforma Invoice records from Excel Sync.";
}

function PageChrome({ title }: { title: string }) {
  return (
    <>
      <View fixed style={styles.runningHeader}>
        <Text style={styles.brandName}>PROGLASS AUTOMATION</Text>
        <Text style={styles.reportLabel}>{title}</Text>
      </View>
      <View fixed style={styles.footer}>
        <Text>ProGlass Automation · filtered operational export</Text>
        <Text render={({ pageNumber, totalPages }) => `Page ${pageNumber} of ${totalPages}`} />
      </View>
    </>
  );
}

function OperationsPdfDocument({ bundle }: { bundle: OperationsExportBundle }) {
  const [primarySheet, ...additionalSheets] = bundle.sheets;

  return (
    <Document title={bundle.title} author="ProGlass Automation" subject="Filtered operations export">
      <Page size="A4" orientation="landscape" style={styles.page} wrap>
        <PageChrome title={bundle.title} />

        <View style={styles.block} wrap={false}>
          <Text style={styles.title}>{bundle.title}</Text>
          <Text style={styles.subtitle}>Generated {bundle.generatedAt.replace("T", " ").replace(/\.\d+Z$/, " UTC")}</Text>
          <Text style={styles.subtitle}>{bundle.rowCount.toLocaleString()} matching rows · all pages included</Text>
          <Text style={styles.gate}>{reportGate(bundle.report)}</Text>
        </View>

        {Object.keys(bundle.summary).length > 0 && (
          <View style={styles.block} wrap={false}>
            <Text style={styles.sectionTitle}>Summary</Text>
            <View style={styles.summaryGrid}>
              {Object.entries(bundle.summary).map(([label, value]) => (
                <View key={label} style={styles.summaryCard}>
                  <Text style={styles.summaryLabel}>{label}</Text>
                  <Text style={styles.summaryValue}>{typeof value === "number" ? value.toLocaleString() : value}</Text>
                </View>
              ))}
            </View>
          </View>
        )}

        {Object.keys(bundle.filters).length > 0 && (
          <View style={styles.block}>
            <Text style={styles.sectionTitle}>Applied filters</Text>
            <View style={styles.filters}>
              {Object.entries(bundle.filters).map(([key, value]) => (
                <Text key={key} style={styles.filterItem}>{titleCase(key)}: {value}</Text>
              ))}
            </View>
          </View>
        )}

        {primarySheet && (
          <View style={styles.block}>
            <Text style={styles.sectionTitle}>{primarySheet.name}</Text>
            {tableForSheet(bundle, primarySheet)}
          </View>
        )}
      </Page>

      {additionalSheets.map((sheet) => (
        <Page key={sheet.name} size="A4" orientation="landscape" style={styles.page} wrap>
          <PageChrome title={bundle.title} />
          <View style={styles.block}>
            <Text style={styles.sectionTitle}>{sheet.name}</Text>
            {tableForSheet(bundle, sheet)}
          </View>
        </Page>
      ))}
    </Document>
  );
}

export async function createOperationsPdf(bundle: OperationsExportBundle): Promise<Buffer> {
  return renderToBuffer(<OperationsPdfDocument bundle={bundle} />);
}

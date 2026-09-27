/**
 * API view types (Phase 28) — hand-mapped from the C# contracts:
 * JobOrderContracts.cs, ProductionContracts.cs, StockContracts.cs,
 * MasterDataContracts.cs. The server is the source of truth; these
 * cover exactly the fields the SPA renders.
 */

/** MasterData.PagedResult<T> — { items, page, pageSize, totalCount }. */
export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface JobOrder {
  id: string;
  jobOrderNo: string;
  sourceProformaNo: string;
  customerName: string;
  projectName: string;
  grandTotalVatInclusive: number;
  status: string;
  statusDisplay: string;
  priority: string;
  deliveryDate: string | null;
  assignedTo: string;
  siteDeliveryContact: string;
  cuttingInstructions: string;
  notes: string;
  isVoid: boolean;
  voidReason: string;
  createdAt: string;
}

/** ProductionWorkItemDto — the plan row and the released piece row. */
export interface WorkItem {
  id: string;
  uniqueKey: string;
  specGroupNumber: number;
  specName: string;
  buildType: string;
  serialNumber: number;
  glassRef: string;
  position: string;
  stage: string;
  stageLabel: string;
  status: string;
  widthMm: number;
  heightMm: number;
  qty: number;
  qtyDone: number;
  unitSqm: number;
  totalSqm: number;
  totalLm: number;
  treatment: string;
  isFoc: boolean;
  isCancelled: boolean;
  notes: string;
}

export interface ProductionPlan {
  jobOrderNo: string;
  customerName: string;
  projectName: string;
  items: WorkItem[];
  warnings: string[];
}

export interface ReleaseResult {
  jobOrderNo: string;
  plannedItems: number;
  insertedItems: number;
  existingItems: number;
  warnings: string[];
  projectedExecutions: number;
}

export interface GlassSheet {
  id: string;
  category: string;
  thicknessMm: number;
  color: string;
  widthMm: number;
  heightMm: number;
  supplier: string;
  purchasePricePerSqm: number;
  sellPricePerSqm: number;
  stock: number;
  used: number;
  balance: number;
  totalSqmInStock: number;
  totalSqmBalance: number;
}

// ── proforma (Phase 29) — ProformaContracts/Step6/7/9Contracts ────────

export interface Proforma {
  id: string;
  proformaNo: string;
  rootProformaId: string | null;
  revisionNumber: number;
  inquiryId: string | null;
  companyId: string | null;
  clientId: string | null;
  projectId: string | null;
  salesmanId: string | null;
  status: string;
  lifecycleState: string;
  issueDate: string | null;
  validUntil: string | null;
  glassMaterialsSubtotal: number;
  otherChargesSubtotal: number;
  discountAmount: number;
  vatAmount: number;
  grandTotalVatInclusive: number;
  isVoid: boolean;
  voidReason: string;
  furthestStep: number;
  createdAt: string;
  updatedAt: string;
  isEditable: boolean;
  validTransitions: string[];
}

export interface SpecGroupItem {
  id: string;
  rowSerial: number;
  position: string;
  w1: number;
  h1: number;
  w2: number;
  h2: number;
  qty: number;
  sqm: number;
  totalSqm: number;
  sqm2: number;
  totalSqm2: number;
  lm1: number;
  lm2: number;
  unitPrice: number;
  total: number;
  notes: string;
}

export interface SpecGroup {
  id: string;
  groupNumber: number;
  glassType: string;
  specName: string;
  ratePerSqm: number;
  priceSource: string;
  outerGlassColor: string;
  innerGlassColor: string;
  thirdGlassColor: string;
  outerSheetWidthMm: number;
  outerSheetHeightMm: number;
  innerSheetWidthMm: number;
  innerSheetHeightMm: number;
  totalQty: number;
  totalSqm: number;
  items: SpecGroupItem[];
}

export interface OtherCharge {
  id: string;
  templateKey: string;
  chargeName: string;
  description: string;
  unit: string;
  isActive: boolean;
  metricKey: string;
  checkedSpecGroupNumbers: number[];
  rate: number;
  metricQty: number;
  amount: number;
  polishSubType: string;
  customPolishDescription: string;
  customPolishQty: number;
  sortOrder: number;
}

export interface WastageRow {
  id: string;
  isActive: boolean;
  colorName: string;
  checkedSourceKeys: string[];
  totalSqm: number;
  isManuallyAdded: boolean;
  useOptimizationFormula: boolean;
  formulaPricePerSqm: number;
  optimizationPercent: number;
  sheetQuantity: number;
  rate: number;
  amount: number;
  sortOrder: number;
}

export interface CustomCharge {
  id: string;
  isActive: boolean;
  chargeName: string;
  qty: number;
  unit: string;
  rate: number;
  amount: number;
  sortOrder: number;
}

export interface Step6 {
  charges: OtherCharge[];
  wastages: WastageRow[];
  customs: CustomCharge[];
  subtotal: number;
}

export interface Step7 {
  discountType: string;
  discountValue: number;
  isVatApplicable: boolean;
  vatPercent: number;
  glassMaterialsSubtotal: number;
  otherChargesSubtotal: number;
  netSubtotal: number;
  discountAmount: number;
  subtotalBeforeVat: number;
  vatAmount: number;
  grandTotalVatInclusive: number;
}

export interface PnlRow {
  id: string;
  kind: string;
  specNumber: number;
  specName: string;
  description: string;
  sheetWidthMm: number;
  sheetHeightMm: number;
  sheetSqm: number;
  quantity: number;
  pricePerUnit: number;
  unitLabel: string;
  amount: number;
  sheetSource: string;
  sortOrder: number;
}

export interface PnlKpis {
  grossVatInclusive: number;
  netExVat: number;
  totalGlassCost: number;
  totalProductionCost: number;
  profit: number;
  isLoss: boolean;
  marginPercent: number;
  overallMarginPercent: number;
  netMarginPercent: number;
  uInsertCost: number;
  basisCost: number;
}

export interface Step9 {
  wastagePercent: number;
  costAtSellingPrice: boolean;
  hasBeenCalculated: boolean;
  kpis: PnlKpis | null;
  rows: PnlRow[];
}

// ── master data pickers (Phase 29 create form) ────────────────────────

export interface Company {
  id: string;
  name: string;
  trn: string;
  location: string;
  representative: string;
  representativeMobile: string;
  email: string;
  accountNumber: string;
  bankName: string;
  logoStorageKey: string;
  logoLayout: string;
  logoWidthMm: number;
  logoHeightMm: number;
  isDefault: boolean;
  usageCount: number;
  lastUsedAtUtc: string | null;
  createdAtUtc: string;
}

export interface Client {
  id: string;
  customerName: string;
  customerTrn: string;
  referenceCode: string;
  salesRepresentative: string;
  documentPreparedBy: string;
  billingAddress: string;
  attentionExecutiveName: string;
  phone: string;
  usageCount: number;
  lastUsedAtUtc: string | null;
  createdAtUtc: string;
}

export interface Project {
  id: string;
  identifierName: string;
  contractNo: string;
  reference: string;
  siteDeliveryLocation: string;
  lpoNumber: string;
  customerId: string | null;
  usageCount: number;
  lastUsedAtUtc: string | null;
  createdAtUtc: string;
}

export interface Salesman {
  id: string;
  name: string;
  department: string;
  usageCount: number;
  lastUsedAtUtc: string | null;
  createdAtUtc: string;
}

// ── delivery notes (Phase 30) — DeliveryContracts ─────────────────────

export interface DeliveryNoteItem {
  id: string;
  specGroupNumber: number;
  serialNumber: number;
  specGroupName: string;
  glassRef: string;
  w1: number;
  h1: number;
  w2: number;
  h2: number;
  orderedQty: number;
  previouslyDeliveredQty: number;
  deliveredQty: number;
  balanceQty: number;
  reworkQty: number;
  returnedQty: number;
}

export interface DeliveryNote {
  id: string;
  deliveryNo: string;
  jobOrderId: string;
  jobOrderNo: string;
  customerName: string;
  projectName: string;
  status: string;
  deliveryType: string;
  isFoc: boolean;
  isMasterDn: boolean;
  sequenceNumber: number;
  driver: string;
  vehicle: string;
  receiverName: string;
  deliveryDate: string | null;
  deliveryTime: string;
  notes: string;
  totalOrderedQty: number;
  totalDeliveredQty: number;
  totalBalanceQty: number;
  isVoid: boolean;
  voidReason: string;
  rowVersion: number;
  items: DeliveryNoteItem[];
}

export interface CreateOrOpenDn {
  outcome: string;
  note: DeliveryNote | null;
  message: string;
}

// ── fiscal (Phase 30) — FiscalService contracts ───────────────────────

export interface TaxInvoice {
  id: string;
  invoiceNo: string;
  proformaId: string | null;
  jobOrderId: string | null;
  deliveryNoteReference: string;
  customerName: string;
  projectName: string;
  status: string;
  paymentTerms: string;
  issueDate: string | null;
  dueDate: string | null;
  subtotal: number;
  discountPercent: number;
  discountAmount: number;
  vatAmount: number;
  grandTotal: number;
  paidAmount: number;
  balanceAmount: number;
  isVoid: boolean;
  voidReason: string;
  companyName: string;
  purchaseOrderNumber: string;
}

export interface CreditNote {
  id: string;
  creditNoteNo: string;
  taxInvoiceId: string;
  taxInvoiceNo: string;
  reason: string;
  status: string;
  subtotal: number;
  vatAmount: number;
  grandTotal: number;
  issuedAt: string | null;
}

export interface VatReport {
  from: string;
  to: string;
  invoiceCount: number;
  standardRatedSupplies: number;
  outputVat: number;
  creditNoteCount: number;
  creditNoteSupplies: number;
  creditNoteVat: number;
  netVatPayable: number;
}

export interface StatementLine {
  lineType: string;
  documentNo: string;
  documentDate: string | null;
  description: string;
  debit: number;
  credit: number;
  runningBalance: number;
}

export interface Statement {
  id: string;
  statementNo: string;
  title: string;
  customer: string;
  from: string | null;
  to: string | null;
  totalDebit: number;
  totalCredit: number;
  balance: number;
  lines: StatementLine[];
  company: string;
  project: string;
  salesman: string;
}

// ── reporting (Phase 31) — ReportingContracts / RowTraceability ───────

export interface ReportingFilterQuery {
  dateFrom?: string;
  dateTo?: string;
  company?: string;
  customer?: string;
  salesman?: string;
  project?: string;
  status?: string;
  searchText?: string;
  sortField?: string;
  sortDescending?: boolean;
  page?: number;
  pageSize?: number;
}

export interface FunnelStage {
  label: string;
  count: number;
  value: number;
  sharePct: number;
  stepConversionPct: number;
  firstStageCount: number;
  isBottleneck: boolean;
}

export interface MixRow {
  rank: number;
  label: string;
  count: number;
  qty: number;
  sqm: number;
  value: number;
  sharePct: number;
  isOtherBucket: boolean;
}

export interface ExecutiveSummary {
  generatedAt: string;
  periodDescription: string;
  funnel: FunnelStage[];
  quotedValue: number;
  invoicedValue: number;
  outstandingValue: number;
  revenue: number;
  cost: number;
  profit: number;
  marginPct: number;
  unconvertedQuotes: number;
  uninvoicedConfirmed: number;
  overdueInvoices: number;
  deliveryCompletionPct: number;
  criticalPendingQty: number;
  topSpecifications: MixRow[];
  topCustomers: MixRow[];
  dominantSpecification: string;
  dominantColour: string;
  invoicesWithoutCost: number;
  hasCostGap: boolean;
  hasData: boolean;
}

export interface ProfitabilityRow {
  key: string;
  name: string;
  category: string;
  revenue: number;
  cost: number;
  glassCost: number;
  productionCost: number;
  profit: number;
  margin: number;
  qty: number;
  sqm: number;
  documentCount: number;
  hasCostData: boolean;
  isStaleCost: boolean;
  costStalenessNote: string;
  costSource: string;
}

export interface ProfitabilitySummary {
  totalRevenue: number;
  totalCost: number;
  totalProfit: number;
  avgMargin: number;
  invoicesWithCost: number;
  invoicesWithoutCost: number;
  missingCostSources: string[];
}

export interface ThroughputPoint {
  weekStart: string;
  count: number;
  sqm: number;
}

export interface JoAnalytics {
  totalJobOrders: number;
  totalQty: number;
  totalSqm: number;
  totalValue: number;
  topCustomers: MixRow[];
  topSpecifications: MixRow[];
  topColours: MixRow[];
  glassTypeMix: MixRow[];
  throughput: ThroughputPoint[];
  dominantSpecification: string;
  topSpecSharePct: number;
  dominantColour: string;
}

export interface AgeingBucket {
  label: string;
  minDays: number;
  maxDays: number;
  count: number;
  qty: number;
  sqm: number;
  sharePct: number;
  isCritical: boolean;
}

export interface DeliveryAnalytics {
  totalRows: number;
  orderedQty: number;
  deliveredQty: number;
  pendingQty: number;
  notStartedRows: number;
  completedRows: number;
  partialRows: number;
  pendingAgeing: AgeingBucket[];
  criticalPendingQty: number;
  pendingByCustomer: MixRow[];
  pendingBySpecification: MixRow[];
  deliveredBySpecification: MixRow[];
}

export interface TraceSourceRow {
  rowId: string;
  proformaNo: string;
  specGroupNumber: number;
  serialNumber: number;
  specification: string;
  glassType: string;
  glassRef: string;
  w1: number;
  h1: number;
  w2: number;
  h2: number;
  qty: number;
  rate: number;
  amount: number;
}

export interface TraceStep {
  stage: string;
  documentNumber: string;
  status: string;
  qty: number;
  value: number;
  date: string | null;
  notes: string;
}

export interface QuantityReconciliation {
  orderedQty: number;
  jobOrderQty: number;
  deliveredQty: number;
  remainingQty: number;
  invoicedQty: number;
  deliveryState: string;
  invoiceState: string;
}

export interface RowTrace {
  sourceRow: TraceSourceRow;
  steps: TraceStep[];
  reconciliation: QuantityReconciliation;
}

// ── specifications (Phase 33) — the Step 4 glass library ──────────────

export interface Specification {
  id: string;
  specName: string;
  glassType: string;
  ratePerSqm: number;
  costBasisPerSqm: number;
  usageCount: number;
  lastUsedAt: string | null;
  isActive: boolean;
  category: string | null;
  color: string | null;
  thicknessMm: number;
  sheetWidthMm: number;
  sheetHeightMm: number;
  sheetId: string | null;
  panesJson: string | null;
  lastProformaNo: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface SpecificationUsage {
  id: string;
  specId: string;
  proformaId: string;
  proformaNo: string;
  specName: string;
  glassType: string;
  companyName: string;
  customerName: string;
  projectName: string;
  pricePerSqm: number;
  sheetWidthMm: number;
  sheetHeightMm: number;
  usedAt: string;
}

export interface ReconcileResult {
  usageRows: number;
  purged: number;
  healed: number;
}

// ── cutting floor (Phase 34 — the Phase 26b surfaces) ─────────────────

export interface CuttingRequirement {
  pieceId: string;
  pieceLabel: string;
  pieceIndex: number;
  pieceSequence: number;
  jobRef: string;
  specGroupNumber: number;
  serialNumber: number;
  specName: string;
  glassRef: string;
  position: string;
  widthMm: number;
  heightMm: number;
  glassFamily: string;
  color: string;
  thicknessMm: number;
  coating: string;
  treatment: string;
  requiresTempering: boolean;
  isFoc: boolean;
  replacementSequence: number;
  materialKey: string;
}

export interface MaterialGroup {
  groupId: string;
  displayLabel: string;
  glassFamily: string;
  color: string;
  thicknessMm: number;
  coating: string;
  treatment: string;
  totalPieces: number;
  totalAreaSqm: number;
  requirements: CuttingRequirement[];
}

export interface CuttingSession {
  id: string;
  cuttingSessionId: string;
  jobOrderId: string | null;
  jobRef: string;
  sourceProformaId: string;
  optimizationSessionId: string | null;
  materialGroupId: string;
  totalPieces: number;
  sheetsUsed: number;
  utilizationPct: number;
  wastagePct: number;
  status: string;
  rotationPolicy: string;
  splitMode: string;
  effortLevel: number;
  isVerified: boolean;
  verificationResult: string;
  completedAt: string | null;
  createdAt: string;
  notes: string;
}

export interface CuttingSheet {
  id: string;
  cuttingSheetId: string;
  cuttingSessionId: string;
  jobRef: string;
  sheetNo: number;
  stockSheetId: string | null;
  stockSizeLabel: string;
  stockWidthMm: number;
  stockHeightMm: number;
  thicknessMm: number;
  color: string;
  usableX: number;
  usableY: number;
  usableWidthMm: number;
  usableHeightMm: number;
  usedSqm: number;
  stockSqm: number;
  utilizationPct: number;
  wastagePct: number;
  isVerified: boolean;
  placedPieceCount: number;
  createdAt: string;
}

export interface CuttingPlacedPiece {
  id: string;
  placedPieceId: string;
  cuttingSheetId: string;
  cuttingSessionId: string;
  jobRef: string;
  specGroupNumber: number;
  serialNumber: number;
  glassRef: string;
  specName: string;
  position: string;
  pieceId: string;
  pieceLabel: string;
  widthMm: number;
  heightMm: number;
  x: number;
  y: number;
  isRotated: boolean;
  cutSequence: number;
  status: string;
  sourcePieceId: string | null;
  reservationKey: string;
  allocationId: string;
  isFoc: boolean;
  notes: string;
}

export interface CuttingGroupResult {
  groupId: string;
  ok: boolean;
  message: string;
  cuttingSessionId: string | null;
  sheets: number;
  pieces: number;
  warnings: string[];
}

export interface CuttingRunResult {
  success: boolean;
  message: string;
  jobRef: string;
  groups: CuttingGroupResult[];
  sessions: number;
  sheets: number;
  pieces: number;
  reservations: number;
  allocations: number;
  warnings: string[];
  errors: string[];
}

export interface CuttingProgress {
  required: number;
  completed: number;
  remaining: number;
  breakage: number;
  foc: number;
  rework: number;
  qcPending: number;
}

// ── production control room (Phase 35 — the Phase 26a surfaces) ───────

export interface QueueItem {
  workItemId: string;
  uniqueKey: string;
  jobRef: string;
  jobOrderId: string | null;
  customerName: string;
  projectName: string;
  specGroupNumber: number;
  specName: string;
  buildType: string;
  serialNumber: number;
  glassRef: string;
  position: string;
  widthMm: number;
  heightMm: number;
  qty: number;
  qtyDone: number;
  remaining: number;
  stage: string;
  stageLabel: string;
  nextStage: string | null;
  status: string;
  readiness: string;
  blockReason: string;
  priority: string;
  dueDate: string | null;
  dueRisk: string;
  qcResult: string;
  isRework: boolean;
  isFoc: boolean;
  isCancelled: boolean;
  replacementSequence: number;
  operator: string;
  workstationId: string;
  workstationName: string;
  isOnHold: boolean;
  holdReason: string;
  materialAvailable: number;
  materialShortage: number;
  hasMaterialShortage: boolean;
  queueWaitHours: number | null;
  traceabilityLabel: string;
  notes: string;
}

export interface ProductionQueue {
  items: QueueItem[];
  total: number;
}

export interface DashboardKpis {
  jobsInProduction: number;
  readyToStart: number;
  inProgress: number;
  onHold: number;
  blocked: number;
  qcPending: number;
  qcFailed: number;
  rework: number;
  completedToday: number;
  readyForDelivery: number;
  overdueJobs: number;
  dueSoon: number;
  materialShortages: number;
  bottleneckProcess: string;
  bottleneckCount: number;
  processLoad: Record<string, number>;
}

export interface QueuePressure {
  processName: string;
  processType: number;
  readyQueue: number;
  blockedQueue: number;
  overdueQueue: number;
  qcBacklog: number;
  reworkBacklog: number;
  avgWaitHours: number;
  total: number;
  pressureLabel: string;
}

export interface WorkstationLoad {
  workstationId: string;
  code: string;
  name: string;
  processType: number;
  processName: string;
  assignedCount: number;
  readyCount: number;
  inProgressCount: number;
  blockedCount: number;
}

export interface JobProgress {
  jobRef: string;
  specName: string;
  buildType: string;
  specGroupNumber: number;
  serialNumber: number;
  required: number;
  completed: number;
  inProgress: number;
  blocked: number;
  paused: number;
  rework: number;
  qcPending: number;
  qcFailed: number;
  onHold: number;
  cancelled: number;
  remaining: number;
  progressPct: number;
  label: string;
}

export interface Workstation {
  id: string;
  workstationId: string;
  code: string;
  name: string;
  processType: number;
  processName: string;
  isActive: boolean;
  description: string;
}

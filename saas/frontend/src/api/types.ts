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
  isDefault: boolean;
}

export interface Client {
  id: string;
  customerName: string;
}

export interface Project {
  id: string;
  identifierName: string;
  customerId: string | null;
}

export interface Salesman {
  id: string;
  name: string;
  department: string;
}

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

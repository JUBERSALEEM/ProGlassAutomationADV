/**
 * Endpoint wrappers — one function per API call the SPA makes, with the
 * query-string builders kept next to their usage.
 */
import { del, get, post, put } from "./client";
import type {
  GlassSheet,
  JobOrder,
  PagedResult,
  ProductionPlan,
  ReleaseResult,
  WorkItem,
} from "./types";

// ── job orders ────────────────────────────────────────────────────────

export interface JobOrderListQuery {
  search?: string;
  status?: string;
  includeVoid?: boolean;
  page?: number;
  pageSize?: number;
}

export function listJobOrders(query: JobOrderListQuery = {}) {
  const params = new URLSearchParams();
  if (query.search) params.set("search", query.search);
  if (query.status) params.set("status", query.status);
  if (query.includeVoid) params.set("includeVoid", "true");
  if (query.page) params.set("page", String(query.page));
  if (query.pageSize) params.set("pageSize", String(query.pageSize));
  const qs = params.toString();
  return get<PagedResult<JobOrder>>(`/api/v1/job-orders${qs ? `?${qs}` : ""}`);
}

export function getJobOrder(id: string): Promise<JobOrder> {
  return get<JobOrder>(`/api/v1/job-orders/${id}`);
}

// ── production ────────────────────────────────────────────────────────

export function getProductionPlan(jobOrderId: string): Promise<ProductionPlan> {
  return get<ProductionPlan>(`/api/v1/production/job-orders/${jobOrderId}/plan`);
}

export function releaseProduction(jobOrderId: string): Promise<ReleaseResult> {
  return post<ReleaseResult>(`/api/v1/production/job-orders/${jobOrderId}/release`);
}

export function listPiecesForJob(jobOrderId: string): Promise<WorkItem[]> {
  return get<WorkItem[]>(`/api/v1/production/job-orders/${jobOrderId}`);
}

/** StageProgressRequest — floor progress (auto-status from QtyDone). */
export function recordStage(
  pieceId: string,
  qtyDone: number,
  reason?: string,
): Promise<unknown> {
  return post(`/api/v1/production/${pieceId}/stage`, {
    qtyDone,
    ...(reason ? { reason } : {}),
  });
}

/** QcRequest — Passed | Failed | Rework (WPF QcResult). */
export function recordQc(
  pieceId: string,
  result: "Passed" | "Failed" | "Rework",
  reason?: string,
): Promise<unknown> {
  return post(`/api/v1/production/${pieceId}/qc`, {
    result,
    ...(reason ? { reason } : {}),
  });
}

/** BreakageRequest — qty + mandatory reason (WPF core rules). */
export function recordBreakage(
  pieceId: string,
  qtyBroken: number,
  reason: string,
): Promise<unknown> {
  return post(`/api/v1/production/${pieceId}/breakage`, { qtyBroken, reason });
}

/** CancelPieceRequest — idempotent cancel with optional reason. */
export function cancelPiece(pieceId: string, reason?: string): Promise<unknown> {
  return post(`/api/v1/production/${pieceId}/cancel`, reason ? { reason } : {});
}

// ── stock ─────────────────────────────────────────────────────────────

export interface SheetQuery {
  category?: string;
  color?: string;
  thicknessMm?: number;
  search?: string;
  page?: number;
  pageSize?: number;
}

export function listSheets(query: SheetQuery = {}) {
  const params = new URLSearchParams();
  if (query.category) params.set("category", query.category);
  if (query.color) params.set("color", query.color);
  if (query.thicknessMm) params.set("thicknessMm", String(query.thicknessMm));
  if (query.search) params.set("search", query.search);
  if (query.page) params.set("page", String(query.page));
  if (query.pageSize) params.set("pageSize", String(query.pageSize));
  const qs = params.toString();
  return get<PagedResult<GlassSheet>>(`/api/v1/stock/sheets${qs ? `?${qs}` : ""}`);
}

export function listLowStockSheets(): Promise<GlassSheet[]> {
  return get<GlassSheet[]>("/api/v1/stock/sheets/low-stock");
}

// ── proforma invoices (Phase 29) ──────────────────────────────────────

import type {
  Company,
  Client,
  Proforma,
  Project,
  Salesman,
  SpecGroup,
  Step6,
  Step7,
  Step9,
} from "./types";

export interface ProformaListQuery {
  search?: string;
  status?: string;
  page?: number;
  pageSize?: number;
}

export function listProformas(query: ProformaListQuery = {}) {
  const params = new URLSearchParams();
  if (query.search) params.set("search", query.search);
  if (query.status) params.set("status", query.status);
  if (query.page) params.set("page", String(query.page));
  if (query.pageSize) params.set("pageSize", String(query.pageSize));
  const qs = params.toString();
  return get<PagedResult<Proforma>>(`/api/v1/proforma-invoices${qs ? `?${qs}` : ""}`);
}

export function getProforma(id: string): Promise<Proforma> {
  return get<Proforma>(`/api/v1/proforma-invoices/${id}`);
}

export function getProformaRevisions(id: string): Promise<Proforma[]> {
  return get<Proforma[]>(`/api/v1/proforma-invoices/${id}/revisions`);
}

/** TransitionRequest — machine target state, WPF names. */
export function transitionProforma(
  id: string,
  toState: string,
  reason?: string,
): Promise<Proforma> {
  return post<Proforma>(`/api/v1/proforma-invoices/${id}/transitions`, {
    toState,
    ...(reason ? { reason } : {}),
  });
}

export function voidProforma(id: string, reason?: string): Promise<Proforma> {
  return post<Proforma>(`/api/v1/proforma-invoices/${id}/void`, reason ? { reason } : {});
}

export function duplicateProforma(id: string): Promise<Proforma> {
  return post<Proforma>(`/api/v1/proforma-invoices/${id}/duplicate`);
}

export function createProformaRevision(id: string): Promise<Proforma> {
  return post<Proforma>(`/api/v1/proforma-invoices/${id}/revisions`);
}

export interface CreateProformaBody {
  companyId?: string;
  clientId?: string;
  projectId?: string;
  salesmanId?: string;
  inquiryId?: string;
  issueDate?: string;
  validUntil?: string;
}

export function createProformaDraft(body: CreateProformaBody): Promise<Proforma> {
  return post<Proforma>("/api/v1/proforma-invoices", body);
}

export function getSpecGroups(id: string): Promise<SpecGroup[]> {
  return get<SpecGroup[]>(`/api/v1/proforma-invoices/${id}/spec-groups`);
}

export function getStep6(id: string): Promise<Step6> {
  return get<Step6>(`/api/v1/proforma-invoices/${id}/step6`);
}

export function getStep7(id: string): Promise<Step7> {
  return get<Step7>(`/api/v1/proforma-invoices/${id}/step7`);
}

export function getStep9(id: string): Promise<Step9> {
  return get<Step9>(`/api/v1/proforma-invoices/${id}/step9`);
}

// ── master data pickers (create form) ─────────────────────────────────

export interface MasterDataQuery {
  search?: string;
  page?: number;
  pageSize?: number;
}

function mdParams(query: MasterDataQuery = {}): string {
  const params = new URLSearchParams();
  if (query.search) params.set("search", query.search);
  if (query.page) params.set("page", String(query.page));
  params.set("pageSize", String(query.pageSize ?? 100));
  const qs = params.toString();
  return `?${qs}`;
}

export function listCompanies(query: MasterDataQuery = {}): Promise<PagedResult<Company>> {
  return get<PagedResult<Company>>(`/api/v1/companies${mdParams(query)}`);
}

export function listClients(query: MasterDataQuery = {}): Promise<PagedResult<Client>> {
  return get<PagedResult<Client>>(`/api/v1/clients${mdParams(query)}`);
}

export function listProjects(query: MasterDataQuery = {}): Promise<PagedResult<Project>> {
  return get<PagedResult<Project>>(`/api/v1/projects${mdParams(query)}`);
}

export function listSalesmen(query: MasterDataQuery = {}): Promise<PagedResult<Salesman>> {
  return get<PagedResult<Salesman>>(`/api/v1/salesmen${mdParams(query)}`);
}

// ── master data CRUD (Phase 32 — requests are the MasterDataContracts
//    records; POST is the WPF natural-key upsert, PUT edits by id) ─────

export function saveCompany(body: Record<string, unknown>): Promise<Company> {
  return post<Company>("/api/v1/companies", body);
}
export function updateCompany(id: string, body: Record<string, unknown>): Promise<Company> {
  return put<Company>(`/api/v1/companies/${id}`, body);
}
export function deleteCompany(id: string): Promise<unknown> {
  return del(`/api/v1/companies/${id}`);
}

export function saveClient(body: Record<string, unknown>): Promise<Client> {
  return post<Client>("/api/v1/clients", body);
}
export function updateClient(id: string, body: Record<string, unknown>): Promise<Client> {
  return put<Client>(`/api/v1/clients/${id}`, body);
}
export function deleteClient(id: string): Promise<unknown> {
  return del(`/api/v1/clients/${id}`);
}

export function saveProject(body: Record<string, unknown>): Promise<Project> {
  return post<Project>("/api/v1/projects", body);
}
export function updateProject(id: string, body: Record<string, unknown>): Promise<Project> {
  return put<Project>(`/api/v1/projects/${id}`, body);
}
export function deleteProject(id: string): Promise<unknown> {
  return del(`/api/v1/projects/${id}`);
}

export function saveSalesman(body: Record<string, unknown>): Promise<Salesman> {
  return post<Salesman>("/api/v1/salesmen", body);
}
export function updateSalesman(id: string, body: Record<string, unknown>): Promise<Salesman> {
  return put<Salesman>(`/api/v1/salesmen/${id}`, body);
}
export function deleteSalesman(id: string): Promise<unknown> {
  return del(`/api/v1/salesmen/${id}`);
}

// ── delivery notes (Phase 30) ─────────────────────────────────────────

import type {
  CreateOrOpenDn,
  CreditNote,
  DeliveryNote,
  PagedResult as _Paged,
  Statement,
  TaxInvoice,
  VatReport,
} from "./types";

export interface DeliveryListQuery {
  search?: string;
  status?: string;
  includeVoid?: boolean;
  mastersOnly?: boolean;
  page?: number;
  pageSize?: number;
}

export function listDeliveryNotes(query: DeliveryListQuery = {}) {
  const params = new URLSearchParams();
  if (query.search) params.set("search", query.search);
  if (query.status) params.set("status", query.status);
  if (query.includeVoid) params.set("includeVoid", "true");
  if (query.mastersOnly) params.set("mastersOnly", "true");
  if (query.page) params.set("page", String(query.page));
  if (query.pageSize) params.set("pageSize", String(query.pageSize));
  const qs = params.toString();
  return get<_Paged<DeliveryNote>>(`/api/v1/delivery-notes${qs ? `?${qs}` : ""}`);
}

export function getDeliveryNote(id: string): Promise<DeliveryNote> {
  return get<DeliveryNote>(`/api/v1/delivery-notes/${id}`);
}

/** The WPF's ONE path: open the existing open note or create the next. */
export function createOrOpenDn(jobOrderId: string): Promise<CreateOrOpenDn> {
  return post<CreateOrOpenDn>(`/api/v1/delivery-notes/job-orders/${jobOrderId}`);
}

export function getMasterDn(jobOrderId: string): Promise<DeliveryNote> {
  return get<DeliveryNote>(`/api/v1/delivery-notes/job-orders/${jobOrderId}/master`);
}

export interface DispatchUpdateBody {
  driver?: string;
  vehicle?: string;
  receiverName?: string;
  deliveryDate?: string;
  deliveryTime?: string;
  notes?: string;
  isFoc?: boolean;
  items?: { itemId: string; deliveredQty: number }[];
  expectedRowVersion?: number;
}

/** UpdateDispatchRequest — dispatch edit on OPEN notes only. */
export function updateDispatch(
  id: string,
  body: DispatchUpdateBody,
): Promise<DeliveryNote> {
  return put<DeliveryNote>(`/api/v1/delivery-notes/${id}`, body);
}

export function transitionDn(
  id: string,
  toStatus: string,
  reason?: string,
): Promise<DeliveryNote> {
  return post<DeliveryNote>(`/api/v1/delivery-notes/${id}/transition`, {
    toStatus,
    ...(reason ? { reason } : {}),
  });
}

export function voidDn(id: string, reason?: string): Promise<DeliveryNote> {
  return post<DeliveryNote>(`/api/v1/delivery-notes/${id}/void`, reason ? { reason } : {});
}

// ── fiscal (Phase 30) ─────────────────────────────────────────────────

export function listTaxInvoices(query: {
  search?: string;
  status?: string;
  includeVoid?: boolean;
  page?: number;
  pageSize?: number;
} = {}): Promise<TaxInvoice[]> {
  const params = new URLSearchParams();
  if (query.search) params.set("search", query.search);
  if (query.status) params.set("status", query.status);
  if (query.includeVoid) params.set("includeVoid", "true");
  if (query.page) params.set("page", String(query.page));
  if (query.pageSize) params.set("pageSize", String(query.pageSize));
  const qs = params.toString();
  return get<TaxInvoice[]>(`/api/v1/fiscal/tax-invoices${qs ? `?${qs}` : ""}`);
}

export function transitionTaxInvoice(
  id: string,
  toStatus: string,
  reason?: string,
): Promise<TaxInvoice> {
  return post<TaxInvoice>(`/api/v1/fiscal/tax-invoices/${id}/status`, {
    toStatus,
    ...(reason ? { reason } : {}),
  });
}

export function recordPayment(
  id: string,
  amount: number,
  notes?: string,
): Promise<TaxInvoice> {
  return post<TaxInvoice>(`/api/v1/fiscal/tax-invoices/${id}/payments`, {
    amount,
    ...(notes ? { notes } : {}),
  });
}

export function listCreditNotes(): Promise<CreditNote[]> {
  return get<CreditNote[]>("/api/v1/fiscal/credit-notes");
}

export function getVatReport(from?: string, to?: string): Promise<VatReport> {
  const params = new URLSearchParams();
  if (from) params.set("from", from);
  if (to) params.set("to", to);
  const qs = params.toString();
  return get<VatReport>(`/api/v1/fiscal/vat-report${qs ? `?${qs}` : ""}`);
}

export interface StatementFilters {
  customer?: string;
  company?: string;
  project?: string;
  salesman?: string;
  from?: string;
  to?: string;
}

export function generateStatement(filters: StatementFilters): Promise<Statement> {
  return post<Statement>("/api/v1/fiscal/statements", filters);
}

// ── reporting (Phase 31) ──────────────────────────────────────────────

import type {
  DeliveryAnalytics as _DeliveryAnalytics,
  ExecutiveSummary as _ExecutiveSummary,
  JoAnalytics as _JoAnalytics,
  ProfitabilityRow as _ProfitabilityRow,
  ProfitabilitySummary as _ProfitabilitySummary,
  ReportingFilterQuery as _ReportingFilterQuery,
  RowTrace as _RowTrace,
} from "./types";

function reportParams(
  query: _ReportingFilterQuery,
  extra: Record<string, string> = {},
): string {
  const params = new URLSearchParams(extra);
  if (query.dateFrom) params.set("dateFrom", query.dateFrom);
  if (query.dateTo) params.set("dateTo", query.dateTo);
  if (query.company) params.set("company", query.company);
  if (query.customer) params.set("customer", query.customer);
  if (query.salesman) params.set("salesman", query.salesman);
  if (query.project) params.set("project", query.project);
  if (query.status) params.set("status", query.status);
  if (query.searchText) params.set("searchText", query.searchText);
  if (query.sortField) params.set("sortField", query.sortField);
  if (!query.sortDescending) params.set("sortDescending", "false");
  if (query.page) params.set("page", String(query.page));
  if (query.pageSize) params.set("pageSize", String(query.pageSize));
  const qs = params.toString();
  return qs ? `?${qs}` : "";
}

export function getExecutiveSummary(query: _ReportingFilterQuery = {}) {
  return get<_ExecutiveSummary>(
    `/api/v1/reports/executive-summary${reportParams(query)}`,
  );
}

export function getProfitability(query: _ReportingFilterQuery = {}) {
  return get<_Paged<_ProfitabilityRow>>(`/api/v1/reports/profitability${reportParams(query)}`);
}

export function getProfitabilitySummary(query: _ReportingFilterQuery = {}) {
  return get<_ProfitabilitySummary>(
    `/api/v1/reports/profitability/summary${reportParams(query)}`,
  );
}

export function getJoAnalytics(query: _ReportingFilterQuery = {}) {
  return get<_JoAnalytics>(`/api/v1/reports/analytics/job-orders${reportParams(query)}`);
}

export function getDeliveryAnalytics(query: _ReportingFilterQuery = {}) {
  return get<_DeliveryAnalytics>(`/api/v1/reports/analytics/delivery${reportParams(query)}`);
}

/** Row trace — the composite row id "PI-…-G1-S1" or a bare proforma number. */
export function traceRow(rowId: string): Promise<_RowTrace> {
  return get<_RowTrace>(`/api/v1/reports/trace/${encodeURIComponent(rowId)}`);
}

// ── specifications (Phase 33 — the Step 4 glass library) ──────────────

import type {
  ReconcileResult as _ReconcileResult,
  Specification as _Specification,
  SpecificationUsage as _SpecificationUsage,
} from "./types";

export function listSpecifications(glassType?: string): Promise<_Specification[]> {
  const qs = glassType ? `?glassType=${encodeURIComponent(glassType)}` : "";
  return get<_Specification[]>(`/api/v1/specifications${qs}`);
}

/** Step 4 APPLY — upsert by (glassType, specName); rate always refreshes,
 *  catalogue details only when supplied (preserve-on-blank). */
export function applySpecification(
  body: Record<string, unknown>,
): Promise<_Specification> {
  return post<_Specification>("/api/v1/specifications", body);
}

export function deleteSpecification(id: string): Promise<unknown> {
  return del(`/api/v1/specifications/${id}`);
}

export function getSpecificationUsage(
  id: string,
): Promise<_SpecificationUsage[]> {
  return get<_SpecificationUsage[]>(`/api/v1/specifications/${id}/usage-history`);
}

export function getRecentSpecificationUsage(
  limit?: number,
): Promise<_SpecificationUsage[]> {
  const qs = limit ? `?limit=${limit}` : "";
  return get<_SpecificationUsage[]>(`/api/v1/specifications/usage/recent${qs}`);
}

/** Re-derive counts/history/provenance from saved invoices (the ledger Refresh). */
export function reconcileSpecifications(): Promise<_ReconcileResult> {
  return post<_ReconcileResult>("/api/v1/specifications/reconcile");
}

// ── cutting floor (Phase 34 — Phase 26b surfaces) ─────────────────────

import type {
  CuttingPlacedPiece as _Piece,
  CuttingProgress as _Progress,
  CuttingRunResult as _RunResult,
  CuttingSession as _Session,
  CuttingSheet as _Sheet,
  MaterialGroup as _MaterialGroup,
} from "./types";

export function getCuttingRequirements(jobOrderId: string): Promise<_MaterialGroup[]> {
  return get<_MaterialGroup[]>(`/api/v1/cutting/requirements?jobOrderId=${jobOrderId}`);
}

/** The full per-group workflow: reserve → pack → verify → persist → allocate. */
export function runCutting(
  jobOrderId: string,
  options: { rotation?: string; split?: string; effortLevel?: number; notes?: string } = {},
): Promise<_RunResult> {
  return post<_RunResult>("/api/v1/cutting/run", {
    jobOrderId,
    ...(options.rotation ? { rotation: options.rotation } : {}),
    ...(options.split ? { split: options.split } : {}),
    ...(options.effortLevel ? { effortLevel: options.effortLevel } : {}),
    ...(options.notes ? { notes: options.notes } : {}),
  });
}

export function listCuttingSessions(jobOrderId: string): Promise<_Session[]> {
  return get<_Session[]>(`/api/v1/cutting/sessions?jobOrderId=${jobOrderId}`);
}

export function getCuttingSession(cuttingSessionId: string): Promise<_Session> {
  return get<_Session>(`/api/v1/cutting/sessions/${cuttingSessionId}`);
}

export function listCuttingSheets(cuttingSessionId: string): Promise<_Sheet[]> {
  return get<_Sheet[]>(`/api/v1/cutting/sessions/${cuttingSessionId}/sheets`);
}

export function listCuttingPieces(cuttingSessionId: string): Promise<_Piece[]> {
  return get<_Piece[]>(`/api/v1/cutting/sessions/${cuttingSessionId}/pieces`);
}

export function completeCuttingSession(cuttingSessionId: string): Promise<_Session> {
  return post<_Session>(`/api/v1/cutting/sessions/${cuttingSessionId}/complete`);
}

export function completeCuttingPiece(
  placedPieceId: string,
  notes?: string,
): Promise<_Piece> {
  return post<_Piece>(`/api/v1/cutting/pieces/${placedPieceId}/complete`, notes ? { notes } : {});
}

export function cuttingBreakage(
  placedPieceId: string,
  reason: string,
  isFoc: boolean,
  notes?: string,
): Promise<_Piece> {
  return post<_Piece>(`/api/v1/cutting/pieces/${placedPieceId}/breakage`, {
    reason,
    isFoc,
    ...(notes ? { notes } : {}),
  });
}

export function getCuttingProgress(jobOrderId: string): Promise<_Progress> {
  return get<_Progress>(`/api/v1/cutting/progress?jobOrderId=${jobOrderId}`);
}

// ── production control room (Phase 35 — Phase 26a surfaces) ───────────

import type {
  DashboardKpis as _Kpis,
  JobProgress as _JobProgress,
  ProductionQueue as _Queue,
  QueuePressure as _Pressure,
  Workstation as _Workstation,
  WorkstationLoad as _Load,
} from "./types";

export interface QueueQuery {
  jobOrderId?: string;
  stage?: string;
  readiness?: string;
  priority?: string;
  workstation?: string;
  search?: string;
  customer?: string;
  includeCompleted?: boolean;
  includeCancelled?: boolean;
  take?: number;
}

export function getProductionQueue(query: QueueQuery = {}): Promise<_Queue> {
  const params = new URLSearchParams();
  if (query.jobOrderId) params.set("jobOrderId", query.jobOrderId);
  if (query.stage) params.set("stage", query.stage);
  if (query.readiness) params.set("readiness", query.readiness);
  if (query.priority) params.set("priority", query.priority);
  if (query.workstation) params.set("workstation", query.workstation);
  if (query.search) params.set("search", query.search);
  if (query.customer) params.set("customer", query.customer);
  if (query.includeCompleted) params.set("includeCompleted", "true");
  if (query.includeCancelled) params.set("includeCancelled", "true");
  if (query.take) params.set("take", String(query.take));
  const qs = params.toString();
  return get<_Queue>(`/api/v1/production/queue${qs ? `?${qs}` : ""}`);
}

export function getDashboardKpis(): Promise<_Kpis> {
  return get<_Kpis>("/api/v1/production/dashboard");
}

export function getQueuePressure(): Promise<_Pressure[]> {
  return get<_Pressure[]>("/api/v1/production/dashboard/pressure");
}

export function getWorkstationLoad(): Promise<_Load[]> {
  return get<_Load[]>("/api/v1/production/dashboard/workstation-load");
}

export function getJobProgress(jobOrderId?: string): Promise<_JobProgress[]> {
  const qs = jobOrderId ? `?jobOrderId=${jobOrderId}` : "";
  return get<_JobProgress[]>(`/api/v1/production/progress${qs}`);
}

export function listWorkstations(activeOnly?: boolean): Promise<_Workstation[]> {
  const qs = activeOnly ? "?activeOnly=true" : "";
  return get<_Workstation[]>(`/api/v1/production/workstations${qs}`);
}

/** Create a station (idempotent on the code). */
export function createWorkstation(body: {
  code: string;
  name?: string;
  processType: number;
  description?: string;
}): Promise<_Workstation> {
  return post<_Workstation>("/api/v1/production/workstations", body);
}

/** Seed the ten default stations (idempotent — only when none exist). */
export function seedWorkstations(): Promise<unknown> {
  return post("/api/v1/production/workstations/seed");
}

// ── production back-office (Phase 36 — 26b costs/integrity/reports) ───

import type {
  ActualCost as _ActualCost,
  Allocation as _Allocation,
  ConsistencyScan as _Scan,
  CostSummary as _CostSummary,
  PairingAssemblyRow as _Pairing,
  ProcessStatusRow as _Process,
  QueueItem as _QueueItem,
  Replacement as _Replacement,
  RouteReportRow as _Route,
  SnapshotIntegrity as _Snapshot,
} from "./types";

export function listAllocations(jobOrderId: string): Promise<_Allocation[]> {
  return get<_Allocation[]>(`/api/v1/production/allocations?jobOrderId=${jobOrderId}`);
}

export function consumeAllocation(allocationId: string): Promise<_Allocation> {
  return post<_Allocation>(`/api/v1/production/allocations/${allocationId}/consume`);
}

export function listActualCosts(jobOrderId: string): Promise<_ActualCost[]> {
  return get<_ActualCost[]>(`/api/v1/production/costs?jobOrderId=${jobOrderId}`);
}

export function getCostSummary(jobOrderId: string): Promise<_CostSummary> {
  return get<_CostSummary>(`/api/v1/production/costs/summary?jobOrderId=${jobOrderId}`);
}

/** Derive ActualMaterial costs from the job's allocations (piece sqm ×
 *  rack purchase price per sqm). */
export function deriveCosts(jobOrderId: string): Promise<unknown> {
  return post(`/api/v1/production/costs/derive?jobOrderId=${jobOrderId}`);
}

export function listReplacements(jobOrderId: string): Promise<_Replacement[]> {
  return get<_Replacement[]>(`/api/v1/production/replacements?jobOrderId=${jobOrderId}`);
}

/** Read-only Production ↔ Sheet Store consistency scan (never writes). */
export function runConsistencyScan(): Promise<_Scan> {
  return get<_Scan>("/api/v1/production/consistency-scan");
}

export function checkSnapshotIntegrity(jobOrderId: string): Promise<_Snapshot> {
  return get<_Snapshot>(`/api/v1/production/snapshot-integrity?jobOrderId=${jobOrderId}`);
}

export function getProcessStatusReport(jobOrderId: string): Promise<_Process[]> {
  return get<_Process[]>(
    `/api/v1/production/reports/process-status?jobOrderId=${jobOrderId}`,
  );
}

export function getPairingAssemblyReport(
  jobOrderId: string,
): Promise<_Pairing[]> {
  return get<_Pairing[]>(
    `/api/v1/production/reports/pairing-assembly?jobOrderId=${jobOrderId}`,
  );
}

export function getRouteReport(jobOrderId: string): Promise<_Route[]> {
  return get<_Route[]>(`/api/v1/production/reports/route?jobOrderId=${jobOrderId}`);
}

export function getReworkQueueReport(): Promise<_QueueItem[]> {
  return get<_QueueItem[]>("/api/v1/production/reports/rework-queue");
}

export function getQcQueueReport(): Promise<_QueueItem[]> {
  return get<_QueueItem[]>("/api/v1/production/reports/qc-queue");
}

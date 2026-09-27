/**
 * Endpoint wrappers — one function per API call the SPA makes, with the
 * query-string builders kept next to their usage.
 */
import { get, post, put } from "./client";
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

export function listCompanies(): Promise<PagedResult<Company>> {
  return get<PagedResult<Company>>("/api/v1/companies?pageSize=100");
}

export function listClients(): Promise<PagedResult<Client>> {
  return get<PagedResult<Client>>("/api/v1/clients?pageSize=100");
}

export function listProjects(): Promise<PagedResult<Project>> {
  return get<PagedResult<Project>>("/api/v1/projects?pageSize=100");
}

export function listSalesmen(): Promise<PagedResult<Salesman>> {
  return get<PagedResult<Salesman>>("/api/v1/salesmen?pageSize=100");
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

/**
 * Endpoint wrappers — one function per API call the SPA makes, with the
 * query-string builders kept next to their usage.
 */
import { get, post } from "./client";
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

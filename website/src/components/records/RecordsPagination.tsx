"use client";

import { useState } from "react";
import type { PaginationInfo } from "@/types/records";

type RecordsPaginationProps = {
  pagination: PaginationInfo;
  onPageChange: (newPage: number) => void;
  onPageSizeChange: (newSize: number) => void;
  loading: boolean;
};

export function RecordsPagination({
  pagination,
  onPageChange,
  onPageSizeChange,
  loading,
}: RecordsPaginationProps) {
  const { page, pageSize, totalEntries, totalPages } = pagination;
  const [jumpInput, setJumpInput] = useState("");

  const startEntry = totalEntries === 0 ? 0 : (page - 1) * pageSize + 1;
  const endEntry = Math.min(page * pageSize, totalEntries);

  const handleJump = (e: React.FormEvent) => {
    e.preventDefault();
    const target = parseInt(jumpInput, 10);
    if (!isNaN(target) && target >= 1 && target <= totalPages) {
      onPageChange(target);
      setJumpInput("");
    }
  };

  // Generate page numbers with ellipses
  const getPageNumbers = () => {
    const pages: (number | string)[] = [];
    const delta = 2; // numbers around current page

    for (let i = 1; i <= totalPages; i++) {
      if (i === 1 || i === totalPages || (i >= page - delta && i <= page + delta)) {
        pages.push(i);
      } else if (pages[pages.length - 1] !== "...") {
        pages.push("...");
      }
    }
    return pages;
  };

  return (
    <div className="flex flex-col gap-3.5 sm:flex-row sm:items-center sm:justify-between border-t border-line-strong/60 bg-ink-950/70 p-3.5 rounded-2xl">
      {/* Left: Summary statement & Page Size selector */}
      <div className="flex flex-wrap items-center gap-3.5 text-xs text-text-muted">
        <div>
          Showing <span className="font-mono font-semibold text-text-primary">{startEntry.toLocaleString()}</span> to{" "}
          <span className="font-mono font-semibold text-text-primary">{endEntry.toLocaleString()}</span> of{" "}
          <span className="font-mono font-semibold text-text-primary">{totalEntries.toLocaleString()}</span> entries
        </div>

        <div className="flex items-center gap-1.5 border-l border-line/60 pl-4">
          <label className="text-3xs uppercase tracking-wider text-text-faint font-semibold">
            Rows per page:
          </label>
          <select
            value={pageSize}
            onChange={(e) => onPageSizeChange(Number(e.target.value))}
            disabled={loading}
            className="rounded-lg border border-line-strong bg-ink-900 px-2 py-1 font-mono text-xs text-text-primary focus:border-brand-400 focus:outline-none"
          >
            <option value={50}>50</option>
            <option value={100}>100</option>
            <option value={250}>250</option>
          </select>
        </div>
      </div>

      {/* Right: Prev / Next / Number buttons & Jump */}
      <div className="flex flex-wrap items-center gap-2">
        {/* Previous Button */}
        <button
          type="button"
          onClick={() => onPageChange(page - 1)}
          disabled={page <= 1 || loading}
          className="rounded-lg border border-line-strong bg-ink-850 px-3 py-1.5 text-xs font-semibold text-text-secondary transition-colors hover:bg-ink-800 hover:text-text-primary disabled:opacity-40 disabled:cursor-not-allowed"
        >
          Previous
        </button>

        {/* Page numbers */}
        <div className="hidden sm:flex items-center gap-1">
          {getPageNumbers().map((p, idx) => {
            if (p === "...") {
              return (
                <span key={`dots-${idx}`} className="px-2 py-1 text-xs text-text-faint font-mono">
                  …
                </span>
              );
            }
            const isCurrent = p === page;
            return (
              <button
                key={p}
                type="button"
                onClick={() => onPageChange(Number(p))}
                disabled={loading}
                className={`h-8 w-8 rounded-lg font-mono text-xs font-semibold transition-colors ${
                  isCurrent
                    ? "bg-brand-500 text-white shadow-sm"
                    : "border border-line-strong/60 bg-ink-900 text-text-muted hover:border-brand-400 hover:text-text-primary"
                }`}
              >
                {p}
              </button>
            );
          })}
        </div>

        {/* Next Button */}
        <button
          type="button"
          onClick={() => onPageChange(page + 1)}
          disabled={page >= totalPages || loading}
          className="rounded-lg border border-line-strong bg-ink-850 px-3 py-1.5 text-xs font-semibold text-text-secondary transition-colors hover:bg-ink-800 hover:text-text-primary disabled:opacity-40 disabled:cursor-not-allowed"
        >
          Next
        </button>

        {/* Quick Jump Input */}
        {totalPages > 3 && (
          <form onSubmit={handleJump} className="ml-2 flex items-center gap-1 text-xs text-text-faint">
            <span className="text-3xs uppercase">Go:</span>
            <input
              type="number"
              min={1}
              max={totalPages}
              value={jumpInput}
              onChange={(e) => setJumpInput(e.target.value)}
              placeholder={String(page)}
              className="w-12 rounded-lg border border-line-strong bg-ink-900 px-1.5 py-1 text-center font-mono text-xs text-text-primary focus:border-brand-400 focus:outline-none"
            />
          </form>
        )}
      </div>
    </div>
  );
}

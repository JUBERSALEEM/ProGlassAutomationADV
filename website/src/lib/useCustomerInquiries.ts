"use client";

import { useCallback, useEffect, useState } from "react";
import type { CustomerInquiryListItem } from "@/types/inquiry-submissions";

type UseCustomerInquiriesOptions = {
  enabled?: boolean;
  onUnauthorized?: () => void;
};

export function useCustomerInquiries(
  authToken: string | null,
  { enabled = true, onUnauthorized }: UseCustomerInquiriesOptions = {},
) {
  const [inquiries, setInquiries] = useState<CustomerInquiryListItem[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");

  const refresh = useCallback(
    async (signal?: AbortSignal) => {
      if (!enabled || !authToken) return;
      setLoading(true);
      setError("");
      try {
        const response = await fetch("/api/inquiry-submissions", {
          headers: { Authorization: `Bearer ${authToken}` },
          cache: "no-store",
          signal,
        });
        const body = await response.json().catch(() => null);
        if (response.status === 401) {
          setInquiries([]);
          onUnauthorized?.();
          return;
        }
        if (!response.ok || !body?.ok || !Array.isArray(body.data)) {
          throw new Error(body?.error?.message || "Unable to load your inquiries.");
        }
        if (!signal?.aborted) setInquiries(body.data as CustomerInquiryListItem[]);
      } catch (reason) {
        if (reason instanceof Error && reason.name === "AbortError") return;
        if (!signal?.aborted) {
          setError(reason instanceof Error ? reason.message : "Unable to load your inquiries.");
        }
      } finally {
        if (!signal?.aborted) setLoading(false);
      }
    },
    [authToken, enabled, onUnauthorized],
  );

  useEffect(() => {
    if (!enabled || !authToken) return;
    const controller = new AbortController();
    // Loading the caller's inquiry list is the external synchronization this effect owns.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    void refresh(controller.signal);
    return () => controller.abort();
  }, [authToken, enabled, refresh]);

  return { inquiries, loading, error, refresh: () => void refresh() };
}

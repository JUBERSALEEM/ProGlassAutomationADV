"use client";

import { useCallback, useEffect, useState } from "react";
import type { CustomerInquiryListItem } from "@/types/inquiry-submissions";

type UseCustomerInquiriesOptions = {
  enabled?: boolean;
  /** Recover the shared Supabase session and return its latest access token. */
  onUnauthorized?: () => Promise<string | null | void> | string | null | void;
};

const LIST_URL = "/api/inquiry-submissions";
const UNAVAILABLE_MESSAGE = "Your customer account is still available, but the inquiry service rejected its access token after shared-session recovery was attempted. Your inquiry data has not been treated as empty. Retry, or open Account to check the saved session.";

async function unauthorizedReason(response: Response): Promise<string> {
  const body = await response.clone().json().catch(() => null);
  const code = typeof body?.error?.code === "string" ? body.error.code : "UNAUTHORIZED";
  return `Server response: HTTP ${response.status} (${code}).`;
}

export function useCustomerInquiries(
  authToken: string | null,
  { enabled = true, onUnauthorized }: UseCustomerInquiriesOptions = {},
) {
  const [inquiries, setInquiries] = useState<CustomerInquiryListItem[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  const [hasSuccessfulLoad, setHasSuccessfulLoad] = useState(false);

  const refresh = useCallback(
    async (signal?: AbortSignal) => {
      if (!enabled || !authToken) return;
      setLoading(true);
      setError("");
      try {
        let response = await fetch(LIST_URL, {
          headers: { Authorization: `Bearer ${authToken}` },
          cache: "no-store",
          signal,
        });

        if (response.status === 401) {
          // A list request can outlive the token that was current on render.
          // Recover through the single shared session store and retry this
          // exact list request once with the newly issued bearer token.
          let recoveredToken: string | null = null;
          try {
            recoveredToken = (await onUnauthorized?.()) || null;
          } catch {
            // Preserve the existing account view and show a retryable error.
          }
          if (signal?.aborted) return;
          if (recoveredToken) {
            response = await fetch(LIST_URL, {
              headers: { Authorization: `Bearer ${recoveredToken}` },
              cache: "no-store",
              signal,
            });
          }
          if (response.status === 401) {
            const reason = await unauthorizedReason(response);
            if (!signal?.aborted) setError(`${UNAVAILABLE_MESSAGE} Server response: ${reason}`);
            return;
          }
        }

        const body = await response.json().catch(() => null);
        if (!response.ok || !body?.ok || !Array.isArray(body.data)) {
          throw new Error(body?.error?.message || "Unable to load your inquiries. Your saved inquiry data has not been treated as empty.");
        }
        if (!signal?.aborted) {
          setInquiries(body.data as CustomerInquiryListItem[]);
          setHasSuccessfulLoad(true);
        }
      } catch (reason) {
        if (reason instanceof Error && reason.name === "AbortError") return;
        if (!signal?.aborted) {
          setError(reason instanceof Error ? reason.message : "Unable to load your inquiries. Your saved inquiry data has not been treated as empty.");
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

  return { inquiries, loading, error, refresh, hasSuccessfulLoad };
}

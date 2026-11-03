// @vitest-environment jsdom

import { afterEach, describe, expect, it, vi } from "vitest";
import { act, cleanup, renderHook, waitFor } from "@testing-library/react";
import { useCustomerInquiries } from "@/lib/useCustomerInquiries";
import type { CustomerInquiryListItem } from "@/types/inquiry-submissions";

const inquiry = {
  id: "submission-1",
  submission_reference: "PG-2026-001",
  project_name: "Office glazing",
  inquiry_type: "quotation",
} as CustomerInquiryListItem;
const piInquiry = {
  ...inquiry,
  id: "submission-pi-1",
  submission_reference: "PG-2026-PI-001",
  project_name: "Marina Residence PI",
  inquiry_type: "proforma_invoice",
} as CustomerInquiryListItem;

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

describe("useCustomerInquiries", () => {
  it("preserves loaded rows and does not imply an empty account when refresh is unauthorized", async () => {
    const onUnauthorized = vi.fn();
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(new Response(JSON.stringify({ ok: true, data: [inquiry] }), { status: 200 }))
      .mockResolvedValueOnce(new Response(JSON.stringify({ ok: false, error: { code: "UNAUTHORIZED", message: "Sign in before submitting an inquiry." } }), { status: 401 }));
    vi.stubGlobal("fetch", fetchMock);

    const { result } = renderHook(() => useCustomerInquiries("saved-access-token", { onUnauthorized }));
    await waitFor(() => expect(result.current.inquiries).toHaveLength(1));

    await act(async () => {
      await result.current.refresh();
    });

    await waitFor(() => expect(result.current.error).toMatch(/not been treated as empty/i));
    expect(result.current.error).toContain("HTTP 401 (UNAUTHORIZED)");
    expect(result.current.error).not.toMatch(/sign in before submitting|sign in again/i);
    expect(result.current.inquiries).toEqual([inquiry]);
    expect(onUnauthorized).toHaveBeenCalledTimes(1);
  });

  it("recovers the bearer token and retries the same list request, including first-submitted PI inquiries", async () => {
    const onUnauthorized = vi.fn().mockResolvedValue("refreshed-access-token");
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(new Response(JSON.stringify({ ok: false, error: { message: "Your session is expired or invalid." } }), { status: 401 }))
      .mockResolvedValueOnce(new Response(JSON.stringify({ ok: true, data: [piInquiry] }), { status: 200 }));
    vi.stubGlobal("fetch", fetchMock);

    const { result } = renderHook(() => useCustomerInquiries("expired-access-token", { onUnauthorized }));
    await waitFor(() => expect(result.current.inquiries).toEqual([piInquiry]));

    expect(fetchMock).toHaveBeenCalledTimes(2);
    expect(fetchMock.mock.calls[0]?.[1]?.headers).toEqual({ Authorization: "Bearer expired-access-token" });
    expect(fetchMock.mock.calls[1]?.[1]?.headers).toEqual({ Authorization: "Bearer refreshed-access-token" });
    expect(result.current.inquiries[0]?.inquiry_type).toBe("proforma_invoice");
    expect(result.current.error).toBe("");
  });
});

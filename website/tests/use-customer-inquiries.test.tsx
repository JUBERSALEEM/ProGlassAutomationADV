// @vitest-environment jsdom

import { afterEach, describe, expect, it, vi } from "vitest";
import { act, cleanup, renderHook, waitFor } from "@testing-library/react";
import { useCustomerInquiries } from "@/lib/useCustomerInquiries";

const inquiry = {
  id: "submission-1",
  submission_reference: "PG-2026-001",
  project_name: "Office glazing",
} as never;

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

describe("useCustomerInquiries", () => {
  it("preserves previously loaded rows when an API 401 triggers auth recovery", async () => {
    const onUnauthorized = vi.fn();
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(new Response(JSON.stringify({ ok: true, data: [inquiry] }), { status: 200 }))
      .mockResolvedValueOnce(new Response(JSON.stringify({ ok: false, error: { message: "Unauthorized" } }), { status: 401 }));
    vi.stubGlobal("fetch", fetchMock);

    const { result } = renderHook(() => useCustomerInquiries("saved-access-token", { onUnauthorized }));
    await waitFor(() => expect(result.current.inquiries).toHaveLength(1));

    await act(async () => {
      result.current.refresh();
    });

    await waitFor(() => expect(result.current.error).toMatch(/sign-in is still saved/i));
    expect(result.current.inquiries).toEqual([inquiry]);
    expect(onUnauthorized).toHaveBeenCalledTimes(1);
  });
});

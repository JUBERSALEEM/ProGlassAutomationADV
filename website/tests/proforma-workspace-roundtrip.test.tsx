// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import type { ProformaWorkflowSnapshot, ProformaWorkspaceData } from "../src/types/proforma";

vi.mock("../src/lib/useOperatorSession", () => ({
  useOperatorSession: () => ({ checking: false, userEmail: "operator@example.test", authToken: "staff-token", isAuthRequired: false, requireAuth: vi.fn() }),
}));
vi.mock("../src/components/ui/LocaleLink", () => ({ LocaleLink: ({ href, children, ...props }: React.AnchorHTMLAttributes<HTMLAnchorElement>) => <a href={href} {...props}>{children}</a> }));
vi.mock("../src/components/inquiries/proforma/ProformaSharePanel", () => ({ ProformaSharePanel: () => <div data-testid="share-panel" /> }));

afterEach(() => { cleanup(); vi.restoreAllMocks(); });

function jsonResponse(data: unknown) {
  return new Response(JSON.stringify({ ok: true, data }), { status: 200, headers: { "Content-Type": "application/json" } });
}

function workspaceData(snapshot: ProformaWorkflowSnapshot | null = null, saved = false): ProformaWorkspaceData {
  const lines = saved ? [{ group_no: 1, line_no: 1, glass_ref: "Manual label", w1: 1000, h1: 1000, w2: 0, h2: 0, qty: 1, approved_rate: 777, sqm: 0.5, total_sqm: 0.5, line_value: 388.5 }] : [];
  return {
    inquiry: { id: "22222222-2222-4222-8222-222222222222", inquiry_number: "INQ-2026-0007", customer_name: "Test customer", project_name: "Test project", status: "Prepared", proforma_number: "", inquiry_value: 0 },
    proforma: saved ? { id: "11111111-1111-4111-8111-111111111111", inquiry_id: "22222222-2222-4222-8222-222222222222", proforma_number: "PI-WEB-2026-000007", source_system: "website", source_proforma_number: "", inquiry_number: "INQ-2026-0007", customer_name: "Test customer", status: "Draft", currency: "AED", revision: 1, line_count: 1, total_sqm: 0.5, total_value: 388.5, created_at: "2026-11-04T00:00:00.000Z", updated_at: "2026-11-04T00:00:00.000Z" } : null,
    lines,
    groups: saved ? [{ group_no: 1, base_rate: 176.22, surcharge_percent: 0 }] : [],
    groups_persisted: true,
    workflow_snapshot: snapshot,
    workflow_snapshot_persisted: Boolean(snapshot),
    saves: [],
    installed: true,
  };
}

describe("staff PI Step 4–6 save and reopen fidelity", () => {
  it("persists Step 4, Step 5 ownership and Step 6 state, then reopens without losing manual overrides", async () => {
    let current = workspaceData();
    const postedRef: { current: { workflow_snapshot: ProformaWorkflowSnapshot; lines: Array<Record<string, unknown>> } | null } = { current: null };
    const fetchMock = vi.fn(async (_input: RequestInfo | URL, init?: RequestInit) => {
      if (init?.method === "POST") {
        const body = JSON.parse(String(init.body)) as { workflow_snapshot: ProformaWorkflowSnapshot; lines: Array<Record<string, unknown>> };
        postedRef.current = body;
        current = workspaceData(body.workflow_snapshot, true);
        return jsonResponse(current);
      }
      return jsonResponse(current);
    });
    vi.stubGlobal("fetch", fetchMock);
    const { ProformaWorkspace } = await import("../src/components/inquiries/ProformaWorkspace");

    const first = render(<ProformaWorkspace inquiryId="22222222-2222-4222-8222-222222222222" />);
    await screen.findByRole("button", { name: "Apply Step 4 specification and estimate to Step 5 group 1" });
    fireEvent.change(screen.getByLabelText("Glass build family"), { target: { value: "DGU" } });
    fireEvent.click(screen.getByRole("button", { name: "Apply Step 4 specification and estimate to Step 5 group 1" }));

    fireEvent.click(screen.getByRole("button", { name: "Step 5 · Cut list" }));
    fireEvent.change(screen.getByLabelText("Width for line 1"), { target: { value: "1000" } });
    fireEvent.change(screen.getByLabelText("Height for line 1"), { target: { value: "1000" } });
    fireEvent.change(screen.getByLabelText("Glass reference for line 1 of specification 1"), { target: { value: "Manual label" } });
    fireEvent.change(screen.getByLabelText("Approved rate for line 1"), { target: { value: "777" } });

    fireEvent.click(screen.getByRole("button", { name: "Step 6 · Other Charges" }));
    const argon = screen.getByText("Argon Gas (Sqm)").closest("article");
    expect(argon).not.toBeNull();
    fireEvent.click(within(argon!).getByRole("checkbox", { name: "Argon Gas (Sqm)" }));
    fireEvent.click(within(argon!).getByRole("checkbox", { name: "Spec 1" }));

    fireEvent.click(screen.getByRole("button", { name: "Save draft" }));
    await waitFor(() => expect(postedRef.current).not.toBeNull());
    const posted = postedRef.current;
    expect(posted?.workflow_snapshot.step4_groups[1]?.code).toBe("DGU");
    expect(posted?.workflow_snapshot.step5_ownership?.["1:1"]).toEqual({ glassRef: "manual", rate: "manual" });
    expect(posted?.workflow_snapshot.step6.charges.find((charge) => charge.key === "ARGON_GAS")).toMatchObject({ isActive: true, groupNos: [1] });

    first.unmount();
    render(<ProformaWorkspace inquiryId="22222222-2222-4222-8222-222222222222" />);
    await screen.findByRole("button", { name: "Step 5 · Cut list" });
    fireEvent.click(screen.getByRole("button", { name: "Step 4 · Build & estimate" }));
    expect((screen.getByLabelText("Glass build family") as HTMLSelectElement).value).toBe("DGU");
    const colors = screen.getAllByLabelText("Colour (Sheet Store value)");
    fireEvent.change(colors[0]!, { target: { value: "Bronze" } });
    fireEvent.click(screen.getByRole("button", { name: "Apply Step 4 specification and estimate to Step 5 group 1" }));
    fireEvent.click(screen.getByRole("button", { name: "Step 5 · Cut list" }));
    expect((screen.getByLabelText("Glass reference for line 1 of specification 1") as HTMLInputElement).value).toBe("Manual label");
    expect((screen.getByLabelText("Approved rate for line 1") as HTMLInputElement).value).toBe("777");

    fireEvent.click(screen.getByRole("button", { name: "Step 6 · Other Charges" }));
    const reopenedArgon = screen.getByText("Argon Gas (Sqm)").closest("article");
    expect((within(reopenedArgon!).getByRole("checkbox", { name: "Argon Gas (Sqm)" }) as HTMLInputElement).checked).toBe(true);
    expect((within(reopenedArgon!).getByRole("checkbox", { name: "Spec 1" }) as HTMLInputElement).checked).toBe(true);
  });
});

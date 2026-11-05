import { readFileSync } from "node:fs";
import { describe, expect, it } from "vitest";

const migration = readFileSync("supabase-inquiry-proforma-phaseC12-workflow-state.sql", "utf8");

describe("Phase C12 Step 4/6 revision snapshot contract", () => {
  it("stores a staff workflow snapshot under the exact saved proforma revision", () => {
    expect(migration).toContain("CREATE TABLE IF NOT EXISTS public.inquiry_proforma_workflow_states");
    expect(migration).toContain("PRIMARY KEY (proforma_id, revision)");
    expect(migration).toContain("snapshot jsonb NOT NULL");
    expect(migration).toContain("INSERT INTO public.inquiry_proforma_workflow_states (proforma_id, revision, snapshot)");
    expect(migration).toContain("FROM public.inquiry_proforma_saves s");
    expect(migration).toContain("v_revision integer");
  });

  it("keeps customer access closed and limits direct writes to the security-definer RPC", () => {
    expect(migration).toContain("REVOKE ALL ON public.inquiry_proforma_workflow_states FROM PUBLIC, anon, authenticated");
    expect(migration).toContain("GRANT SELECT ON public.inquiry_proforma_workflow_states TO authenticated");
    expect(migration).toContain("public.has_inquiry_role(ARRAY['inquiry_admin', 'inquiry_auditor'])");
    expect(migration).toContain("public.has_inquiry_role(ARRAY['inquiry_admin'])");
    expect(migration).toContain("GRANT EXECUTE ON FUNCTION public.save_inquiry_proforma_v3");
    expect(migration).toContain("octet_length(p_workflow_snapshot::text) > 262144");
  });

  it("preserves v2 line/rate behavior and never lets an idempotent retry overwrite a snapshot", () => {
    expect(migration).toContain("public.save_inquiry_proforma_v2(");
    expect(migration).toContain("PERFORM pg_advisory_xact_lock(hashtextextended(p_idempotency_key, 0))");
    expect(migration).toContain("IF v_existing_revision IS NULL THEN");
    expect(migration).toContain("ON CONFLICT (proforma_id, revision) DO NOTHING");
  });
});

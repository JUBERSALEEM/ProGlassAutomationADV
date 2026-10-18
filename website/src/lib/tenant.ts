/**
 * Tenant resolution (Phase 2) — dormant by design.
 *
 * Answers "which companies does this signed-in user belong to?" from
 * `public.company_members` (supabase-multitenancy-phase2.sql), as the
 * user themselves: the query runs through `supabaseServerWithToken`, so
 * Postgres RLS (read-own policies) is the boundary and a forged token
 * sees nothing. No service key, consistent with `src/lib/supabase.ts`.
 *
 * Nothing consumes this yet. It exists so activation is wiring, not
 * invention: apply the SQL, backfill, then records/deliveries/inquiry
 * APIs scope by `primaryCompanyId` and the flag flips on (activation
 * order in docs/MONETIZATION_PLAN.md). Until `NEXT_PUBLIC_CLOUD_TENANCY`
 * is exactly "on", every call short-circuits to the off context without
 * touching the database — deploying this file changes no behaviour.
 */

import { isSupabaseConfigured, supabaseServerWithToken } from "@/lib/supabase";
import type { TenantContext, TenantMembership, TenantRole } from "@/types/tenancy";

/* Literal member access, like supabase.ts: the bundler inlines this
   exact form, so the flag is a build-time constant wherever it lands. */
export function isTenancyEnabled(): boolean {
  return process.env.NEXT_PUBLIC_CLOUD_TENANCY === "on";
}

const OFF: TenantContext = { enabled: false, memberships: [], primaryCompanyId: null };

type MemberRow = { company_id: string; role: TenantRole };
type CompanyRow = { id: string; name: string; slug: string | null };

export async function resolveTenantContext(token: string): Promise<TenantContext> {
  if (!isTenancyEnabled() || !isSupabaseConfigured() || !token) return OFF;

  const client = supabaseServerWithToken(token);

  const { data: memberRows, error: memberError } = await client
    .from("company_members")
    .select("company_id, role")
    .order("created_at", { ascending: true });
  if (memberError || !memberRows || memberRows.length === 0) {
    return { enabled: true, memberships: [], primaryCompanyId: null };
  }

  const members = memberRows as MemberRow[];
  const { data: companyRows } = await client
    .from("companies")
    .select("id, name, slug")
    .in(
      "id",
      members.map((member) => member.company_id),
    );
  const companiesById = new Map(((companyRows ?? []) as CompanyRow[]).map((row) => [row.id, row]));

  const memberships: TenantMembership[] = members.flatMap((member) => {
    const company = companiesById.get(member.company_id);
    return company ? [{ company, role: member.role }] : [];
  });

  return {
    enabled: true,
    memberships,
    primaryCompanyId: memberships[0]?.company.id ?? null,
  };
}

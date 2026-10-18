/**
 * Multi-tenancy types (Phase 2).
 *
 * Shapes match `website/supabase-multitenancy-phase2.sql`:
 * `public.companies`, `public.company_members`, `public.subscriptions`.
 */

export type TenantRole = "owner" | "admin" | "operator" | "viewer";

export type TenantCompany = {
  id: string;
  name: string;
  slug: string | null;
};

export type TenantMembership = {
  company: TenantCompany;
  role: TenantRole;
};

export type TenantContext = {
  /** False unless NEXT_PUBLIC_CLOUD_TENANCY=on — the dormant default. */
  enabled: boolean;
  /** Every company the caller belongs to (empty when off or none yet). */
  memberships: TenantMembership[];
  /** First membership's company — the default scope once APIs wire in. */
  primaryCompanyId: string | null;
};

/**
 * Which inquiry surface an account is allowed to see.
 *
 * There is exactly one role per account (`inquiry_operator_roles`), and the
 * website has two very different audiences behind `/inquiries`:
 *
 *   * a customer, who submits inquiries and reads what ProGlass shares back;
 *   * the staff roles, who work the internal registers.
 *
 * The database already keeps those apart — a customer can only read their own
 * submissions, and the registers only answer an `inquiry_auditor` or
 * `inquiry_admin`. This module only decides which page to *show*: it is a
 * router, never a permission. Every read and write re-checks the role on the
 * server, so a wrong classification here can leak nothing.
 */

/** The two roles that can open the inquiry register and the website queue. */
export const INQUIRY_REGISTER_ROLES = ["inquiry_auditor", "inquiry_admin"] as const;

export type InquiryRegisterRole = (typeof INQUIRY_REGISTER_ROLES)[number];

/** The single role that works from the assignment workspace. */
export const INQUIRY_ESTIMATOR_ROLE = "inquiry_estimator";

/** The shop PC's sync identity. It publishes; it does not read registers. */
export const INQUIRY_BRIDGE_ROLE = "inquiry_bridge";

export type InquirySurfaceKind = "register" | "estimator" | "bridge" | "restricted" | "customer";

export function isInquiryRegisterRole(role: string | null | undefined, enabled: boolean): boolean {
  return enabled && Boolean(role) && (INQUIRY_REGISTER_ROLES as readonly string[]).includes(role as string);
}

/**
 * A role that exists but is switched off is *restricted*, not a customer: the
 * person needs the explanation, not the customer home.
 */
export function classifyInquirySurface(role: string | null | undefined, enabled: boolean): InquirySurfaceKind {
  if (!role) return "customer";
  if (!enabled) return "restricted";
  if (isInquiryRegisterRole(role, true)) return "register";
  if (role === INQUIRY_ESTIMATOR_ROLE) return "estimator";
  if (role === INQUIRY_BRIDGE_ROLE) return "bridge";
  return "restricted";
}

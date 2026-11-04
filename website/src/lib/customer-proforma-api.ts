/**
 * Phase C11 retirement of the customer priced-proforma projection.
 *
 * This compatibility module intentionally performs no authentication lookup or
 * database query and contains no priced document DTO. Staff PI workflows remain
 * in `proforma-api.ts`; customer inquiry intake remains technical-only.
 */
export class CustomerProformaFailure extends Error {
  readonly code = "CUSTOMER_PRICING_DISABLED" as const;

  constructor() {
    super("Priced proformas are available only through the authorized ProGlass staff workflow.");
    this.name = "CustomerProformaFailure";
  }
}

export async function listCustomerProformas(_token: string): Promise<never> {
  throw new CustomerProformaFailure();
}

export async function fetchCustomerProforma(_token: string, _proformaId: string): Promise<never> {
  throw new CustomerProformaFailure();
}

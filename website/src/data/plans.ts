/**
 * Sellable licence durations and online SKUs.
 *
 * `licenseDurations` mirrors `Licensing/Contracts/LicensePlanCatalog.cs`
 * (`LicensePlanCatalog.All`) name-for-name and day-for-day — that file is
 * the single source of truth shared by the vendor License Generator and
 * the customer app, and this is its website copy. If a duration is added
 * there, add it here; the Phase-1 cross-check compared the two lists
 * mechanically, and any future drift is a bug in the same way.
 *
 * Prices are NOT invented here (the site's standing policy: see
 * `src/data/editions.ts`). A SKU with `price: null` or an empty
 * `paymentLink` is simply not sold online yet — the pricing page omits
 * its buy button and `/api/checkout` answers 501 for it. Publishing a
 * price is an owner edit to this one file: fill the amount, paste the
 * provider's hosted payment link, deploy. See docs/MONETIZATION_PLAN.md.
 */

export type LicenseDuration = {
  id: string;
  /** Verbatim plan name from LicensePlanCatalog.cs. */
  name: string;
  days: number;
  note: string;
  featured?: boolean;
};

export const licenseDurations: LicenseDuration[] = [
  { id: "7-days", name: "7 Days", days: 7, note: "A short evaluation on a real job, with a signed key." },
  { id: "30-days", name: "30 Days", days: 30, note: "One month of production use." },
  { id: "6-months", name: "6 Months", days: 182, note: "Half a year — one duration step below the annual term." },
  { id: "1-year", name: "1 Year", days: 365, note: "The standard annual licence term.", featured: true },
  { id: "2-years", name: "2 Years", days: 730, note: "Two years locked at today's price once published." },
];

export type LicenseSku = {
  id: string;
  /** `editions.ts` edition id (trial | basic | business | factory | enterprise). */
  editionId: string;
  durationId: string;
  /** null = price not published; the SKU is not sold online. */
  price: { amount: number; currency: string } | null;
  /** Provider-hosted checkout URL (Paddle / Lemon Squeezy / Razorpay…). Empty = not sold online. */
  paymentLink: string;
};

/**
 * Online-sellable combinations. Starts empty on purpose: the owner adds
 * rows here as prices and payment links are decided (suggested launch
 * set in docs/MONETIZATION_PLAN.md). Example shape:
 *
 *   { id: "factory-1-year", editionId: "factory", durationId: "1-year",
 *     price: { amount: 0, currency: "AED" }, paymentLink: "https://…" }
 */
export const licenseSkus: LicenseSku[] = [];

export function durationById(id: string): LicenseDuration | undefined {
  return licenseDurations.find((duration) => duration.id === id);
}

export function durationByDays(days: number): LicenseDuration | undefined {
  return licenseDurations.find((duration) => duration.days === days);
}

export function skuById(id: string): LicenseSku | undefined {
  return licenseSkus.find((sku) => sku.id === id);
}

/** A SKU is buyable only when a price is published AND a real https checkout exists. */
export function isSkuBuyable(sku: LicenseSku): boolean {
  return sku.price !== null && sku.price.amount > 0 && sku.paymentLink.startsWith("https://");
}

export function buyableSkus(): LicenseSku[] {
  return licenseSkus.filter(isSkuBuyable);
}

export function formatPrice(price: { amount: number; currency: string }): string {
  return new Intl.NumberFormat("en-US", {
    style: "currency",
    currency: price.currency,
    maximumFractionDigits: Number.isInteger(price.amount) ? 0 : 2,
  }).format(price.amount);
}

/** This site's stable checkout address for a SKU — the provider sits behind it. */
export function checkoutPath(skuId: string): string {
  return `/api/checkout?sku=${encodeURIComponent(skuId)}`;
}

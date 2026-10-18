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
 * Prices were first published here on 2026-10-18 (owner-delegated AED
 * list; see the launch table in docs/MONETIZATION_PLAN.md for the
 * reasoning). A SKU with `price: null` is not offered online; a priced
 * SKU with an empty `paymentLink` sells through the WhatsApp path on
 * the pricing page until hosted checkout links land. Editing a price
 * is an edit to this one file — the page, the checkout route and the
 * FAQ defer to it (the FAQ deliberately repeats no numbers).
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
 * Published online SKUs — AED, excluding 5% UAE VAT. Launch set is
 * deliberately narrow (one- and two-year terms for the three
 * self-serve editions); the 7/30-day and 6-month durations remain
 * available on request, the in-app Trial covers evaluation, and
 * Enterprise stays quote-only. 2-year terms are priced at 1.6× the
 * annual — a 20% anchor for committing longer.
 *
 * `paymentLink` is empty until the payment provider account exists;
 * priced-but-linkless SKUs sell via WhatsApp (see pricing page).
 */
export const licenseSkus: LicenseSku[] = [
  { id: "basic-1-year", editionId: "basic", durationId: "1-year", price: { amount: 1499, currency: "AED" }, paymentLink: "" },
  { id: "basic-2-years", editionId: "basic", durationId: "2-years", price: { amount: 2399, currency: "AED" }, paymentLink: "" },
  { id: "business-1-year", editionId: "business", durationId: "1-year", price: { amount: 3999, currency: "AED" }, paymentLink: "" },
  { id: "business-2-years", editionId: "business", durationId: "2-years", price: { amount: 6399, currency: "AED" }, paymentLink: "" },
  { id: "factory-1-year", editionId: "factory", durationId: "1-year", price: { amount: 7999, currency: "AED" }, paymentLink: "" },
  { id: "factory-2-years", editionId: "factory", durationId: "2-years", price: { amount: 12799, currency: "AED" }, paymentLink: "" },
];

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

/** Priced = a public number exists, checkout link or not (WhatsApp path). */
export function pricedSkus(): LicenseSku[] {
  return licenseSkus.filter((sku) => sku.price !== null && sku.price.amount > 0);
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

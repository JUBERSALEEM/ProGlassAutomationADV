import { billablePanelSqm, isFloored, moneyRate, physicalPanelSqm } from "@/data/pricingRules";
import type { ProformaLine, ProformaLineInput } from "@/types/proforma";

/**
 * The working state of a website proforma, before it is saved.
 *
 * Editors hold text, not numbers, so a half typed dimension never becomes a
 * zero behind the operator. The priced view is derived from that text with the
 * same three rules the database applies on save: a panel bills at its physical
 * area floored at half a square metre, a line is that area times its quantity,
 * and a line's value is its total area times the approved rate.
 *
 * Only the customer safe half of `pricingRules` is imported here. No build
 * cost, sheet purchase rate, margin or optimiser figure reaches this file, and
 * nothing in it is persisted until the database accepts it.
 */

export type DraftLine = {
  key: string;
  groupNo: number;
  glassRef: string;
  w1: string;
  h1: string;
  w2: string;
  h2: string;
  qty: string;
  rate: string;
};

export type PricedLine = {
  key: string;
  groupNo: number;
  lineNo: number;
  glassRef: string;
  w1: number;
  h1: number;
  w2: number;
  h2: number;
  qty: number;
  rate: number;
  physicalSqm: number;
  sqm: number;
  totalSqm: number;
  value: number;
  floored: boolean;
  /** True when the line is complete enough to save. */
  complete: boolean;
};

export type GroupTotal = {
  groupNo: number;
  lines: number;
  quantity: number;
  sqm: number;
  value: number;
};

let fallbackSeq = 0;

/**
 * A version 4 UUID for the Idempotency-Key header.
 *
 * `crypto.randomUUID` is not exposed in every browser context, so a fallback
 * builds the same shape from `getRandomValues`. The database rejects anything
 * that is not a canonical UUID, which is what makes a retry safe rather than a
 * second revision.
 */
export function newIdempotencyKey(): string {
  const direct = globalThis.crypto?.randomUUID?.();
  if (direct) return direct;
  const bytes = new Uint8Array(16);
  globalThis.crypto?.getRandomValues?.(bytes);
  if (!bytes.some((value) => value !== 0)) {
    for (let index = 0; index < bytes.length; index += 1) bytes[index] = Math.floor(Math.random() * 256);
  }
  bytes[6] = ((bytes[6] ?? 0) & 0x0f) | 0x40;
  bytes[8] = ((bytes[8] ?? 0) & 0x3f) | 0x80;
  const hex = Array.from(bytes, (byte) => byte.toString(16).padStart(2, "0")).join("");
  return `${hex.slice(0, 8)}-${hex.slice(8, 12)}-${hex.slice(12, 16)}-${hex.slice(16, 20)}-${hex.slice(20)}`;
}

export function newLineKey(): string {
  const uuid = globalThis.crypto?.randomUUID?.();
  if (uuid) return `line-${uuid}`;
  fallbackSeq += 1;
  return `line-${Date.now()}-${fallbackSeq}`;
}

function round4(value: number): number {
  return Math.round(value * 10000) / 10000;
}

/**
 * A line's money, rounded the way the database's numeric arithmetic rounds it.
 *
 * Multiplying two decimals in floating point loses a half cent that Postgres
 * keeps: 1.5 sqm at 99.99 is 149.985, which is 149.99. Scaling both factors to
 * integers first keeps the half cent, so the figure on screen is the figure the
 * database will store.
 */
export function lineValue(totalSqm: number, rate: number): number {
  const sqmUnits = Math.round(totalSqm * 10000);
  const rateUnits = Math.round(rate * 100);
  return Math.round((sqmUnits * rateUnits) / 10000) / 100;
}
/** A whole number typed into a dimension or quantity field. */
export function wholeNumber(value: string): number {
  const parsed = Number.parseInt(String(value).replace(/[^0-9]/g, ""), 10);
  return Number.isFinite(parsed) ? parsed : 0;
}
const whole = wholeNumber;
function decimal(value: string): number {
  const parsed = Number.parseFloat(String(value).replace(/[^0-9.]/g, ""));
  return Number.isFinite(parsed) ? parsed : 0;
}

export function emptyLine(groupNo: number): DraftLine {
  return { key: newLineKey(), groupNo, glassRef: "", w1: "", h1: "", w2: "", h2: "", qty: "1", rate: "" };
}

/**
 * One line, priced. `lineNo` is filled when the draft is ordered for saving,
 * so a row keeps the same key even when a group above it changes.
 */
export function priceLine(line: DraftLine, lineNo = 0): PricedLine {
  const w1 = whole(line.w1);
  const h1 = whole(line.h1);
  const w2 = whole(line.w2);
  const h2 = whole(line.h2);
  const qty = whole(line.qty);
  const rate = moneyRate(decimal(line.rate));
  const physicalSqm = round4(physicalPanelSqm(w1, h1));
  const sqm = round4(billablePanelSqm(w1, h1));
  const totalSqm = round4(sqm * qty);
  return {
    key: line.key,
    groupNo: line.groupNo,
    lineNo,
    glassRef: line.glassRef.trim(),
    w1,
    h1,
    w2,
    h2,
    qty,
    rate,
    physicalSqm,
    sqm,
    totalSqm,
    value: lineValue(totalSqm, rate),
    floored: isFloored(w1, h1),
    complete: w1 >= 1 && h1 >= 1 && qty >= 1 && rate >= 0 && w1 <= 20000 && h1 <= 20000,
  };
}

export function priceDraft(lines: DraftLine[]): PricedLine[] {
  const counters = new Map<number, number>();
  return lines.map((line) => {
    const next = (counters.get(line.groupNo) || 0) + 1;
    counters.set(line.groupNo, next);
    return priceLine(line, next);
  });
}

export function groupTotals(priced: PricedLine[]): GroupTotal[] {
  const totals = new Map<number, GroupTotal>();
  for (const line of priced) {
    const current = totals.get(line.groupNo) || { groupNo: line.groupNo, lines: 0, quantity: 0, sqm: 0, value: 0 };
    current.lines += 1;
    current.quantity += line.qty;
    current.sqm = round4(current.sqm + line.totalSqm);
    current.value = moneyRate(current.value + line.value);
    totals.set(line.groupNo, current);
  }
  return [...totals.values()].sort((a, b) => a.groupNo - b.groupNo);
}

export function draftTotals(priced: PricedLine[]): { lines: number; sqm: number; value: number } {
  return priced.reduce(
    (acc, line) => ({
      lines: acc.lines + 1,
      sqm: round4(acc.sqm + line.totalSqm),
      value: moneyRate(acc.value + line.value),
    }),
    { lines: 0, sqm: 0, value: 0 },
  );
}

/**
 * The reasons a draft cannot be saved yet, in the order they appear.
 *
 * These mirror the checks `save_inquiry_proforma_v1` makes, so the operator is
 * told here rather than by a rejected save. The limits are the function's own:
 * a panel side from 1 to 20000 mm, an inner size from 0 to 20000 mm, a quantity
 * from 1 to 100000, a rate from 0 to 1000000, at most 50 specifications, at most
 * 500 lines in one specification and at most 200 lines in the document.
 */
export function draftIssues(lines: DraftLine[]): string[] {
  const issues: string[] = [];
  if (lines.length === 0) return ["Add at least one line to the proforma."];

  if (lines.length > 200) {
    issues.push(`A proforma holds at most 200 lines, and this draft has ${lines.length}.`);
  }

  const priced = priceDraft(lines);
  const perGroup = new Map<number, number>();
  for (const line of priced) perGroup.set(line.groupNo, (perGroup.get(line.groupNo) || 0) + 1);

  for (const [groupNo, count] of perGroup) {
    if (count > 500) issues.push(`Specification ${groupNo} holds ${count} lines, and the limit is 500.`);
  }
  const groupNos = [...perGroup.keys()];
  if (groupNos.length > 50) {
    issues.push(`A proforma holds at most 50 specifications, and this draft has ${groupNos.length}.`);
  }
  for (const groupNo of groupNos) {
    if (groupNo < 1 || groupNo > 50) issues.push(`Specification number ${groupNo} is outside 1 to 50.`);
  }

  priced.forEach((line) => {
    if (line.w1 < 1 || line.h1 < 1) issues.push(`Specification ${line.groupNo}, line ${line.lineNo}: enter a width and a height.`);
    else if (line.w1 > 20000 || line.h1 > 20000) issues.push(`Specification ${line.groupNo}, line ${line.lineNo}: a panel side cannot exceed 20000 mm.`);
    if (line.w2 > 20000 || line.h2 > 20000) issues.push(`Specification ${line.groupNo}, line ${line.lineNo}: an inner size cannot exceed 20000 mm.`);
    if (line.qty < 1) issues.push(`Specification ${line.groupNo}, line ${line.lineNo}: enter a quantity of at least one.`);
    else if (line.qty > 100000) issues.push(`Specification ${line.groupNo}, line ${line.lineNo}: a quantity cannot exceed 100000.`);
    if (!Number.isFinite(line.rate)) issues.push(`Specification ${line.groupNo}, line ${line.lineNo}: enter an approved rate.`);
    else if (line.rate < 0) issues.push(`Specification ${line.groupNo}, line ${line.lineNo}: a rate cannot be negative.`);
    else if (line.rate > 1_000_000) issues.push(`Specification ${line.groupNo}, line ${line.lineNo}: a rate cannot exceed 1000000.`);
  });
  return issues;
}

/**
 * A copy of one line, placed directly after it.
 *
 * The copy keeps the specification, the dimensions, the quantity and the rate,
 * because duplicating a row in a specification is how the desktop repeats an
 * identical panel. It gets a fresh key so React and the draft treat it as its own
 * row, and the line numbers renumber themselves from the order on the next price.
 */
export function duplicateLine(lines: DraftLine[], key: string): DraftLine[] {
  const index = lines.findIndex((line) => line.key === key);
  if (index < 0) return lines;
  const source = lines[index];
  if (!source) return lines;
  const copy: DraftLine = { ...source, key: newLineKey() };
  return [...lines.slice(0, index + 1), copy, ...lines.slice(index + 1)];
}

/**
 * Move a line one place up or down inside its own specification.
 *
 * A row never crosses into a neighbouring specification, because the group is what
 * the line belongs to and moving it silently would change what it is part of. A
 * move at the top or bottom of a specification is a no-op.
 */
export function moveLine(lines: DraftLine[], key: string, direction: -1 | 1): DraftLine[] {
  const index = lines.findIndex((line) => line.key === key);
  if (index < 0) return lines;
  const source = lines[index];
  if (!source) return lines;

  let neighbour = index + direction;
  while (neighbour >= 0 && neighbour < lines.length) {
    const candidate = lines[neighbour];
    if (!candidate) break;
    if (candidate.groupNo === source.groupNo) break;
    neighbour += direction;
  }
  const target = lines[neighbour];
  if (!target || target.groupNo !== source.groupNo) return lines;

  const next = [...lines];
  next[index] = target;
  next[neighbour] = source;
  return next;
}

/** Lines in save order, with the group and position the database expects. */
export function toLineInputs(lines: DraftLine[]): ProformaLineInput[] {
  return priceDraft(lines).map((line) => ({
    group_no: line.groupNo,
    line_no: line.lineNo,
    glass_ref: line.glassRef,
    w1: line.w1,
    h1: line.h1,
    w2: line.w2,
    h2: line.h2,
    qty: line.qty,
    rate: line.rate,
  }));
}

/** A saved line, back in the editor. */
export function fromSavedLine(line: ProformaLine): DraftLine {
  return {
    key: newLineKey(),
    groupNo: line.group_no,
    glassRef: line.glass_ref,
    w1: line.w1 ? String(line.w1) : "",
    h1: line.h1 ? String(line.h1) : "",
    w2: line.w2 ? String(line.w2) : "",
    h2: line.h2 ? String(line.h2) : "",
    qty: line.qty ? String(line.qty) : "1",
    rate: line.approved_rate ? String(line.approved_rate) : "",
  };
}

export function groupsOf(lines: DraftLine[]): number[] {
  return [...new Set(lines.map((line) => line.groupNo))].sort((a, b) => a - b);
}

export function nextGroupNo(lines: DraftLine[]): number {
  const used = groupsOf(lines);
  return used.length ? Math.max(...used) + 1 : 1;
}

/**
 * Copy the rows of a saved group into the working draft, as a new group.
 * Returns the lines that were added so the caller can report the count.
 */
export function copyGroup(lines: DraftLine[], groupNo: number, targetGroupNo: number): DraftLine[] {
  const source = lines.filter((line) => line.groupNo === groupNo);
  return source.map((line) => ({ ...line, key: newLineKey(), groupNo: targetGroupNo }));
}

/** Remove a group and renumber the groups after it, keeping row keys. */
export function removeGroup(lines: DraftLine[], groupNo: number): DraftLine[] {
  return lines
    .filter((line) => line.groupNo !== groupNo)
    .map((line) => ({ ...line, groupNo: line.groupNo > groupNo ? line.groupNo - 1 : line.groupNo }));
}

export function formatSqm(value: number): string {
  return value.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });
}

export function formatMoney(value: number, currency: string): string {
  return new Intl.NumberFormat(undefined, { style: "currency", currency, maximumFractionDigits: 2 }).format(value);
}

export function formatCount(value: number): string {
  return value.toLocaleString();
}

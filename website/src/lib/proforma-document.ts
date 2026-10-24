import type { ProformaLine, ProformaLineInput, ProformaRecord } from "@/types/proforma";

/**
 * The printable document of a SAVED proforma revision.
 *
 * This module deliberately computes nothing that the database already decided.
 * A line's `sqm`, `total_sqm`, `line_value` and the header's `total_value` were
 * written by `save_inquiry_proforma_v1`; the document reprints them. The desktop
 * application is the authority for what a PI says, and the website must not be a
 * second authority that "corrects" a stored figure on its way to a customer.
 *
 * The only arithmetic here is addition of figures that are already stored, plus a
 * comparison of that sum against the stored header totals. A disagreement is
 * reported as `balanced: false` so the page can say so out loud. It is never
 * silently repaired, because a repair would hide the very mismatch an operator
 * needs to see before issuing anything.
 */

export type ProformaDocumentLine = {
  lineNo: number;
  glassRef: string;
  w1: number;
  h1: number;
  w2: number;
  h2: number;
  qty: number;
  /** Billable panel area, as stored. */
  sqm: number;
  /** Area times quantity, as stored. */
  totalSqm: number;
  /** Approved rate, as stored. */
  rate: number;
  /** Rate times total area, as stored. */
  value: number;
};

export type ProformaDocumentGroup = {
  groupNo: number;
  lines: ProformaDocumentLine[];
  totalSqm: number;
  value: number;
};

export type ProformaDocumentIntegrity = {
  /** The stored header line count equals the number of stored lines. */
  lineCountMatches: boolean;
  /** The stored header area equals the sum of the stored line areas. */
  totalSqmMatches: boolean;
  /** The stored header value equals the sum of the stored line values. */
  totalValueMatches: boolean;
};

export type ProformaDocumentModel = {
  groups: ProformaDocumentGroup[];
  lineCount: number;
  /** Sum of the stored line areas. */
  totalSqm: number;
  /**
   * Sum of the stored line values: the glass materials subtotal.
   *
   * It is not an invoice total. The desktop summary adds other charges, applies
   * the discount to the taxable value and then adds VAT, and none of those
   * components are stored in the website proforma tables.
   */
  totalValue: number;
  /** The figures the header carries, echoed unchanged. */
  header: { lineCount: number; totalSqm: number; totalValue: number };
  integrity: ProformaDocumentIntegrity;
  /** True when the header agrees with the lines to the precision it stores. */
  balanced: boolean;
};

/** Half of the smallest stored money unit, so a fil of drift is not a mismatch. */
const MONEY_TOLERANCE = 0.005;
/** Half of the fourth decimal, which is the precision `numeric(14,4)` keeps. */
const AREA_TOLERANCE = 0.00005;

function round(value: number, places: number): number {
  const factor = 10 ** places;
  return Math.round(value * factor) / factor;
}

function lineOf(row: ProformaLine): ProformaDocumentLine {
  return {
    lineNo: row.line_no,
    glassRef: row.glass_ref,
    w1: row.w1,
    h1: row.h1,
    w2: row.w2,
    h2: row.h2,
    qty: row.qty,
    sqm: row.sqm,
    totalSqm: row.total_sqm,
    rate: row.approved_rate,
    value: row.line_value,
  };
}

/**
 * Build the document from a saved header and its stored lines.
 *
 * Ordering follows the database: group number, then line number. Duplicate
 * groups are merged, which is what the table already does by construction, and
 * a line whose group is missing or zero is kept under group 1 rather than being
 * dropped, so a damaged row is visible instead of invisible.
 */
export function buildProformaDocument(proforma: ProformaRecord, lines: ProformaLine[]): ProformaDocumentModel {
  const ordered = [...lines].sort((left, right) => {
    if (left.group_no !== right.group_no) return left.group_no - right.group_no;
    return left.line_no - right.line_no;
  });

  const byGroup = new Map<number, ProformaDocumentLine[]>();
  for (const row of ordered) {
    const groupNo = Number.isFinite(row.group_no) && row.group_no > 0 ? row.group_no : 1;
    const bucket = byGroup.get(groupNo);
    if (bucket) bucket.push(lineOf(row));
    else byGroup.set(groupNo, [lineOf(row)]);
  }

  const groups: ProformaDocumentGroup[] = [...byGroup.entries()]
    .sort(([left], [right]) => left - right)
    .map(([groupNo, groupLines]) => ({
      groupNo,
      lines: groupLines,
      totalSqm: round(groupLines.reduce((sum, line) => sum + line.totalSqm, 0), 4),
      value: round(groupLines.reduce((sum, line) => sum + line.value, 0), 2),
    }));

  const totalSqm = round(groups.reduce((sum, group) => sum + group.totalSqm, 0), 4);
  const totalValue = round(groups.reduce((sum, group) => sum + group.value, 0), 2);
  const lineCount = groups.reduce((sum, group) => sum + group.lines.length, 0);

  const header = {
    lineCount: proforma.line_count,
    totalSqm: proforma.total_sqm,
    totalValue: proforma.total_value,
  };

  const integrity: ProformaDocumentIntegrity = {
    lineCountMatches: header.lineCount === lineCount,
    totalSqmMatches: Math.abs(header.totalSqm - totalSqm) <= AREA_TOLERANCE,
    totalValueMatches: Math.abs(header.totalValue - totalValue) <= MONEY_TOLERANCE,
  };

  return {
    groups,
    lineCount,
    totalSqm,
    totalValue,
    header,
    integrity,
    balanced: integrity.lineCountMatches && integrity.totalSqmMatches && integrity.totalValueMatches,
  };
}

/**
 * The reasons a saved proforma must not be presented as a final document yet.
 *
 * These are statements of fact about the stored record, not policy inventions:
 * a draft has not been issued, a void document is not an offer, and a header that
 * disagrees with its lines has to be seen by a person before it leaves the office.
 */
export function documentCaveats(proforma: ProformaRecord, model: ProformaDocumentModel): string[] {
  const caveats: string[] = [];
  if (proforma.status === "Draft") {
    caveats.push("This revision is a draft. It has not been issued and it is not an offer.");
  }
  if (proforma.status === "Cancelled") {
    caveats.push("This revision is cancelled. It must not be presented as a current quotation.");
  }
  if (!model.balanced) {
    caveats.push(
      "The stored header totals do not match the stored lines. The figures below are shown as recorded and are not corrected here.",
    );
  }
  if (!model.integrity.lineCountMatches) {
    caveats.push(
      `The header records ${model.header.lineCount} line${model.header.lineCount === 1 ? "" : "s"}, and ${model.lineCount} ${model.lineCount === 1 ? "is" : "are"} stored.`,
    );
  }
  return caveats;
}

/**
 * True when the working draft carries anything the saved revision does not.
 *
 * The document always prints the saved revision, so an operator who has typed
 * changes needs to be told, in the document itself, that those changes are not in
 * it yet. Comparing the fields that a save actually transmits keeps this honest:
 * a purely cosmetic difference in the grid cannot raise the warning.
 */
export function draftDiffersFromSaved(draft: ProformaLineInput[], saved: ProformaLine[]): boolean {
  const signature = (line: {
    group_no: number;
    line_no: number;
    glass_ref: string;
    w1: number;
    h1: number;
    w2: number;
    h2: number;
    qty: number;
    rate: number;
  }) => [line.group_no, line.line_no, line.glass_ref, line.w1, line.h1, line.w2, line.h2, line.qty, line.rate].join(":");

  const draftRows = draft.map(signature).sort();
  const savedRows = saved
    .map((line) =>
      signature({
        group_no: line.group_no,
        line_no: line.line_no,
        glass_ref: line.glass_ref,
        w1: line.w1,
        h1: line.h1,
        w2: line.w2,
        h2: line.h2,
        qty: line.qty,
        rate: line.approved_rate,
      }),
    )
    .sort();

  if (draftRows.length !== savedRows.length) return true;
  return draftRows.some((row, index) => row !== savedRows[index]);
}

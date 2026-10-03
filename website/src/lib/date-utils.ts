/**
 * ProGlass Automation — Excel Date Utility Layer
 *
 * Provides high-resilience date parsing, normalization, and serialization
 * for Excel workbooks and ERP records.
 *
 * Supports:
 * - DD-MM-YYYY, DD/MM/YYYY, DD.MM.YYYY (e.g. "01-10-2026", "1-10-2026")
 * - YYYY-MM-DD, YYYY/MM/DD, YYYY.MM.DD (e.g. "2026-10-01")
 * - Excel Serial Date Numbers (e.g. 46296, "46296", 46296.5)
 * - ISO 8601 Timestamps (e.g. "2026-10-01T14:30:00.000Z")
 * - Standard JavaScript Date objects
 */

/**
 * Parses any incoming date representation from Excel or database into a valid Date object.
 */
export function parseExcelDate(val: unknown): Date | null {
  if (val === null || val === undefined) return null;
  if (val instanceof Date) {
    return isNaN(val.getTime()) ? null : val;
  }

  const str = String(val).trim();
  if (!str || str === "null" || str === "undefined") return null;

  // 1. Excel serial date number (typically ~20,000 to ~80,000 for modern calendar dates)
  if (/^\d{4,6}(\.\d+)?$/.test(str)) {
    const num = parseFloat(str);
    if (num >= 1000 && num <= 100000) {
      // Excel epoch offset: 25569 days between 1900-01-01 and 1970-01-01 (including Lotus 1900 leap bug)
      const ms = Math.round((num - 25569) * 86400 * 1000);
      const d = new Date(ms);
      if (!isNaN(d.getTime())) return d;
    }
  }

  // 2. DD-MM-YYYY or DD/MM/YYYY or DD.MM.YYYY (or 2-digit year DD-MM-YY)
  const dmyMatch = str.match(/^(\d{1,2})[-/.](\d{1,2})[-/.](\d{2,4})(?:[\sT].*)?$/);
  if (dmyMatch) {
    const day = parseInt(dmyMatch[1]!, 10);
    const month = parseInt(dmyMatch[2]!, 10) - 1;
    let year = parseInt(dmyMatch[3]!, 10);
    if (year < 100) {
      year = year >= 70 ? 1900 + year : 2000 + year;
    }
    if (month >= 0 && month <= 11 && day >= 1 && day <= 31) {
      const d = new Date(Date.UTC(year, month, day));
      if (!isNaN(d.getTime())) return d;
    }
  }

  // 3. Named month format (e.g. "01-Oct-2026", "1 Oct 2026", "01-OCT-26")
  const dMonYMatch = str.match(/^(\d{1,2})[-/\s.]([A-Za-z]{3,9})[-/\s.](\d{2,4})(?:[\sT].*)?$/);
  if (dMonYMatch) {
    const day = parseInt(dMonYMatch[1]!, 10);
    const monStr = dMonYMatch[2]!.toLowerCase();
    const months: Record<string, number> = {
      jan: 0, feb: 1, mar: 2, apr: 3, may: 4, jun: 5,
      jul: 6, aug: 7, sep: 8, oct: 9, nov: 10, dec: 11,
      january: 0, february: 1, march: 2, april: 3, june: 5,
      july: 6, august: 7, september: 8, october: 9, november: 10, december: 11,
    };
    if (monStr in months) {
      const month = months[monStr]!;
      let year = parseInt(dMonYMatch[3]!, 10);
      if (year < 100) {
        year = year >= 70 ? 1900 + year : 2000 + year;
      }
      if (day >= 1 && day <= 31) {
        const d = new Date(Date.UTC(year, month, day));
        if (!isNaN(d.getTime())) return d;
      }
    }
  }

  // 3. YYYY-MM-DD or YYYY/MM/DD or YYYY.MM.DD (ISO format)
  const ymdMatch = str.match(/^(\d{4})[-/.](\d{1,2})[-/.](\d{1,2})(?:[\sT].*)?$/);
  if (ymdMatch) {
    const year = parseInt(ymdMatch[1]!, 10);
    const month = parseInt(ymdMatch[2]!, 10) - 1;
    const day = parseInt(ymdMatch[3]!, 10);
    if (month >= 0 && month <= 11 && day >= 1 && day <= 31) {
      const d = new Date(Date.UTC(year, month, day));
      if (!isNaN(d.getTime())) return d;
    }
  }

  // 4. General ISO or standard Date fallback
  const fallback = new Date(str);
  if (!isNaN(fallback.getTime())) return fallback;

  return null;
}

/**
 * Formats any date input into canonical user-specified format: DD-MM-YYYY (e.g. "01-10-2026").
 */
export function formatDateDDMMYYYY(val: unknown): string {
  const d = parseExcelDate(val);
  if (!d) return String(val || "").trim();
  const day = String(d.getUTCDate()).padStart(2, "0");
  const month = String(d.getUTCMonth() + 1).padStart(2, "0");
  const year = d.getUTCFullYear();
  return `${day}-${month}-${year}`;
}

/**
 * Formats any date input into canonical ISO YYYY-MM-DD format (for inputs and queries).
 */
export function formatDateYYYYMMDD(val: unknown): string {
  const d = parseExcelDate(val);
  if (!d) return String(val || "").trim();
  const year = d.getUTCFullYear();
  const month = String(d.getUTCMonth() + 1).padStart(2, "0");
  const day = String(d.getUTCDate()).padStart(2, "0");
  return `${year}-${month}-${day}`;
}

/**
 * Returns UTC epoch milliseconds for chronological comparison.
 */
export function getDateEpoch(val: unknown): number {
  const d = parseExcelDate(val);
  return d ? d.getTime() : 0;
}

/**
 * Chronological comparison with update date / serialization tie-breaker.
 * Follows:
 * 1. Primary order: Business order date (e.g. 01-10-2026 before 02-10-2026)
 * 2. Secondary order: Data update date / creation timestamp serialization
 * 3. Tertiary order: Row ID serialization
 */
export function compareRecordDates(
  a: { date?: unknown; updated_at?: unknown; created_at?: unknown; id?: unknown },
  b: { date?: unknown; updated_at?: unknown; created_at?: unknown; id?: unknown },
  ascending: boolean = true,
): number {
  const epochA = getDateEpoch(a.date);
  const epochB = getDateEpoch(b.date);

  if (epochA !== epochB) {
    return ascending ? epochA - epochB : epochB - epochA;
  }

  // Secondary tie-breaker: update timestamp serialization
  const updA = getDateEpoch(a.updated_at || a.created_at);
  const updB = getDateEpoch(b.updated_at || b.created_at);

  if (updA !== updB) {
    return ascending ? updA - updB : updB - updA;
  }

  // Tertiary tie-breaker: ID / row sequence
  const idA = String(a.id || "");
  const idB = String(b.id || "");
  return idA.localeCompare(idB);
}

/**
 * Which Excel workbook is the site reading, and which one did the operator ask for?
 *
 * The records table accumulates every workbook that was ever synced, so "the
 * data" is not one dataset — it is a stack of them. Reports, the Job Order
 * queue and the Deliveries Tracker must agree on which slice of that stack is
 * on screen, or the same PI number could resolve to different workbook rows.
 *
 * This module is that agreement, in one place. It was lifted out of
 * `records-api.ts` so the deliveries tracker and Job Order queue share the
 * same rules: the resolution is subtle enough (freshness ranking, unlabelled
 * recent rows, an RPC fallback) that a second copy would drift within one
 * release.
 *
 * The rules it encodes, in the order they apply:
 *
 *   1. Collect every distinct `source_file` seen on active rows.
 *   2. Score each file by its newest sync timestamp, falling back to its newest
 *      business date, and keep the winner as the current workbook.
 *   3. If the newest rows in the table carry no `source_file` at all, do not
 *      force the view onto some ancient labelled file — show everything recent.
 *   4. An explicit operator choice wins over all of the above: `all` means every
 *      file, a filename means that file, empty means "current, auto-detected".
 */

import type { SupabaseClient } from "@supabase/supabase-js";
import { getDateEpoch } from "@/lib/date-utils";

export type SourceScope = {
  /** The workbook the tracker considers current, when one can be determined. */
  currentSourceFile: string | null;
  /** Every workbook name seen on active rows, sorted. */
  availableSourceFiles: string[];
  /** True when the newest rows carry no `source_file` and scoping must stand down. */
  detectedLatestHasNoSourceFile: boolean;
  /** The scope to actually query with: null means "no source filter". */
  effectiveSourceFile: string | null;
};

/**
 * A PostgREST `or=` fragment that matches a workbook by its filename.
 *
 * Deliberately substring-based: Excel Sync may write a full path in one row and
 * a bare filename in the next, and both should select the same workbook.
 */
export function buildSourceFileFilter(sourceFile: string): string {
  const cleanName = sourceFile.split(/[\\/]/).pop()?.trim() || sourceFile;
  const safeClean = cleanName.replace(/["\\]/g, "");
  return `source_file.ilike."%${safeClean}%"`;
}

export async function resolveSourceScope(
  client: SupabaseClient,
  requestedSourceFile: string | undefined,
): Promise<SourceScope> {
  let currentSourceFile: string | null = null;
  let availableSourceFiles: string[] = [];
  let detectedLatestHasNoSourceFile = false;

  // Step 1: every distinct workbook name on active rows.
  const sfSet = new Set<string>();

  const collectSourceFiles = async (orderCol?: string) => {
    try {
      let q = client
        .from("records")
        .select("source_file")
        .neq("is_active", false)
        .not("source_file", "is", null)
        .neq("source_file", "");
      if (orderCol) {
        q = q.order(orderCol, { ascending: false, nullsFirst: false });
      }
      let res = await q.limit(500);
      if (res.error) {
        let fallbackQ = client
          .from("records")
          .select("source_file")
          .not("source_file", "is", null)
          .neq("source_file", "");
        if (orderCol) {
          fallbackQ = fallbackQ.order(orderCol, { ascending: false, nullsFirst: false });
        }
        res = await fallbackQ.limit(500);
      }
      if (res.data) {
        for (const row of res.data) {
          if (row.source_file && typeof row.source_file === "string") {
            const trimmed = row.source_file.trim();
            if (trimmed) sfSet.add(trimmed);
          }
        }
      }
    } catch {
      // A scope probe must never take the page down; an empty set means "no scoping".
    }
  };

  await collectSourceFiles("created_at");
  await collectSourceFiles("date");
  await collectSourceFiles();

  availableSourceFiles = Array.from(sfSet).sort((a, b) => a.localeCompare(b));

  // Step 2: freshness ranking — newest sync wins, newest order date breaks the tie.
  let winningFile: string | null = null;
  let winningScore = -1;

  for (const sf of availableSourceFiles) {
    const cleanName = sf.split(/[\\/]/).pop()?.trim() || sf;
    const safeClean = cleanName.replace(/["\\]/g, "");

    try {
      const { data: initialCreated, error: crError } = await client
        .from("records")
        .select("created_at, updated_at, date")
        .neq("is_active", false)
        .ilike("source_file", `%${safeClean}%`)
        .order("created_at", { ascending: false, nullsFirst: false })
        .limit(1);

      let createdRows = initialCreated;
      if (crError) {
        const res = await client
          .from("records")
          .select("created_at, updated_at, date")
          .ilike("source_file", `%${safeClean}%`)
          .order("created_at", { ascending: false, nullsFirst: false })
          .limit(1);
        createdRows = res.data;
      }

      const { data: initialDates, error: dtError } = await client
        .from("records")
        .select("created_at, updated_at, date")
        .neq("is_active", false)
        .ilike("source_file", `%${safeClean}%`)
        .order("date", { ascending: false, nullsFirst: false })
        .limit(1);

      let dateRows = initialDates;
      if (dtError) {
        const res = await client
          .from("records")
          .select("created_at, updated_at, date")
          .ilike("source_file", `%${safeClean}%`)
          .order("date", { ascending: false, nullsFirst: false })
          .limit(1);
        dateRows = res.data;
      }

      const topCreated = createdRows?.[0];
      const topDate = dateRows?.[0];

      const tCreated = topCreated?.created_at ? new Date(topCreated.created_at).getTime() : 0;
      const tUpdated = topCreated?.updated_at ? new Date(topCreated.updated_at).getTime() : 0;
      const dCreated = getDateEpoch(topCreated?.date);
      const dTop = getDateEpoch(topDate?.date);

      const syncTime = Math.max(tCreated, tUpdated);
      const orderTime = Math.max(dCreated, dTop);
      const score = syncTime > 0 ? syncTime : orderTime;

      if (score > winningScore) {
        winningScore = score;
        winningFile = sf;
      }
    } catch {
      // Unreadable candidate — skip it rather than fail the whole resolution.
    }
  }

  if (winningFile) currentSourceFile = winningFile;

  // Step 2B: unlabelled recent rows outrank a labelled historical file.
  try {
    const { data: initialUnlabelled, error: unlError } = await client
      .from("records")
      .select("created_at, date, id")
      .neq("is_active", false)
      .is("source_file", null)
      .order("created_at", { ascending: false, nullsFirst: false })
      .limit(1);

    let unlabelledRows = initialUnlabelled;
    if (unlError) {
      const res = await client
        .from("records")
        .select("created_at, date, id")
        .is("source_file", null)
        .order("created_at", { ascending: false, nullsFirst: false })
        .limit(1);
      unlabelledRows = res.data;
    }

    if (unlabelledRows && unlabelledRows.length > 0) {
      const unlabelledTime = unlabelledRows[0]?.created_at
        ? new Date(unlabelledRows[0].created_at).getTime()
        : 0;
      const unlabelledDate = getDateEpoch(unlabelledRows[0]?.date);

      if (
        (unlabelledTime > 0 && unlabelledTime > winningScore) ||
        (winningScore <= 0 && unlabelledDate > 0)
      ) {
        detectedLatestHasNoSourceFile = true;
        currentSourceFile = null;
      }
    } else if (availableSourceFiles.length === 0) {
      detectedLatestHasNoSourceFile = true;
    }
  } catch {
    // Same reasoning as above — a failed probe means "no signal", not "error".
  }

  // Fallback: ask the dedicated RPC, if the project has it installed.
  if (!currentSourceFile && !detectedLatestHasNoSourceFile) {
    try {
      const { data: rpcCurrentSource } = await client.rpc("get_current_excel_source");
      if (rpcCurrentSource && typeof rpcCurrentSource === "string" && rpcCurrentSource.trim()) {
        currentSourceFile = rpcCurrentSource.trim();
      } else if (
        Array.isArray(rpcCurrentSource) &&
        rpcCurrentSource.length > 0 &&
        rpcCurrentSource[0]?.source_file
      ) {
        currentSourceFile = String(rpcCurrentSource[0].source_file).trim();
      }
    } catch {
      // RPC not installed — the ranking above already answered the question.
    }
  }

  if (currentSourceFile && !sfSet.has(currentSourceFile)) {
    sfSet.add(currentSourceFile);
    availableSourceFiles = Array.from(sfSet).sort((a, b) => a.localeCompare(b));
  }

  const effectiveSourceFile =
    requestedSourceFile === "all"
      ? null
      : requestedSourceFile && requestedSourceFile !== "current"
        ? requestedSourceFile
        : detectedLatestHasNoSourceFile
          ? null
          : currentSourceFile;

  return {
    currentSourceFile,
    availableSourceFiles,
    detectedLatestHasNoSourceFile,
    effectiveSourceFile,
  };
}

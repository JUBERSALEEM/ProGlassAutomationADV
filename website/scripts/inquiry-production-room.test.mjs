import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";

const read = (path) => readFileSync(new URL(path, import.meta.url), "utf8");

const roomSql = read("../supabase-production-room-phase11a.sql");
const actionSql = read("../supabase-production-room-actions.sql");
const roomLib = read("../src/lib/production-room.ts");
const roomTypes = read("../src/types/production-room.ts");
const workspace = read("../src/components/production/ProductionRoomWorkspace.tsx");
const page = read("../src/app/[locale]/production/page.tsx");
const siteNav = read("../src/lib/site.ts");
const sitemap = read("../src/app/sitemap.ts");

test("the production room migration is reproducible, member scoped and RPC written", () => {
  for (const table of [
    "production_room_members",
    "production_room_messages",
    "production_room_reads",
    "production_room_events",
  ]) {
    assert.match(roomSql, new RegExp(`CREATE TABLE IF NOT EXISTS public\\.${table}`));
    assert.match(roomSql, new RegExp(`ALTER TABLE public\\.${table} ENABLE ROW LEVEL SECURITY`));
  }
  // Membership is explicit, so every read policy asks about it.
  assert.match(roomSql, /CREATE OR REPLACE FUNCTION public\.is_production_room_member\(\)/);
  assert.match(roomSql, /CREATE POLICY "Production room members read messages" ON public\.production_room_messages\s+FOR SELECT TO authenticated USING \(is_production_room_member\(\)\)/);
  // No write grant and no write policy anywhere.
  assert.match(roomSql, /FROM anon, authenticated;/);
  assert.match(roomSql, /GRANT SELECT ON TABLE public\.production_room_members, public\.production_room_messages/);
  // `delete_production_room_message` contains "delete", so the check has to
  // look at the privilege position rather than anywhere on the line.
  assert.doesNotMatch(roomSql.replace(/--[^\n]*/g, ""), /GRANT\s+(INSERT|UPDATE|DELETE|TRUNCATE|TRIGGER|REFERENCES)\b/i);
  // A pin can only ever describe a live, top-level decision.
  assert.match(roomSql, /production_room_messages_pin_shape/);
  assert.match(roomSql, /Only a top-level decision can be pinned/);
  // Realtime is part of the contract, not an assumption.
  assert.match(roomSql, /ALTER PUBLICATION supabase_realtime ADD TABLE public\.production_room_messages/);
  assert.match(roomSql, /REPLICA IDENTITY FULL/);
});

test("the production room writes only through its functions", () => {
  for (const rpc of [
    "post_production_room_message",
    "edit_production_room_message",
    "delete_production_room_message",
    "set_production_room_message_pinned",
    "mark_production_room_read",
    "register_production_room_member",
    "set_production_room_member_active",
  ]) {
    assert.match(workspace, new RegExp(`rpc\\("${rpc}"`), `${rpc} is called`);
    assert.match(roomSql, new RegExp(`CREATE OR REPLACE FUNCTION public\\.${rpc}`), `${rpc} is defined`);
  }
  assert.match(workspace, /isSupabaseConfigured/);
  // The browser never writes a room table directly.
  assert.doesNotMatch(workspace, /from\("production_room_(messages|members|reads|events)"\)\s*\.(insert|update|delete|upsert)/);
});

test("the production room mirrors the database rules instead of inventing looser ones", () => {
  assert.match(roomTypes, /PRODUCTION_ROOM_CATEGORIES = \[/);
  for (const category of [
    "general_production",
    "urgent_blocker",
    "material_shortage",
    "machine_issue",
    "quality_issue",
    "schedule_change",
    "dispatch_dependency",
    "decision",
    "action_item",
  ]) {
    assert.match(roomTypes, new RegExp(`"${category}"`));
    assert.match(roomSql, new RegExp(`'${category}'`));
  }
  assert.match(roomLib, /PRODUCTION_ROOM_EDIT_WINDOW_MS = 30 \* 60 \* 1000/);
  assert.match(roomLib, /PRODUCTION_ROOM_MAX_BODY = 4000/);
  assert.match(roomSql, /interval '30 minutes'/);
  assert.match(roomSql, /BETWEEN 1 AND 4000/);
  // A reply inherits the root category, exactly as the insert function does.
  assert.match(workspace, /p_category: replyTarget \? replyTarget\.category : category/);
  assert.doesNotMatch(roomLib, /PRODUCTION_ROOM_MAX_BODY = 5000/);
});

test("the production room is internal: noindex, no public navigation, no sitemap entry", () => {
  assert.match(page, /robots: \{ index: false, follow: false \}/);
  assert.doesNotMatch(siteNav, /"\/production"/);
  assert.doesNotMatch(sitemap, /"\/production"/);
});

test("the production room is honest about access and about failure", () => {
  assert.match(workspace, /Your account is not on the production team yet/);
  assert.match(workspace, /Sign in to open the production room/);
  assert.match(workspace, /saved sign-in could not be confirmed yet/);
  assert.match(workspace, /The production room is not configured/);
  assert.match(workspace, /This message was deleted/);
  assert.match(workspace, /Auto-refresh on/);
  // No fake data path: the room must never render sample posts to look busy.
  assert.doesNotMatch(workspace, /sample|placeholder post|mock/i);
});

test("action items carry an owner, a due time and a status that the database polices", () => {
  for (const column of ["action_owner_user_id", "action_due_at", "action_status"]) {
    assert.match(
      actionSql,
      new RegExp(`ADD COLUMN IF NOT EXISTS ${column}`),
      `${column} is declared`,
    );
    assert.match(roomTypes, new RegExp(`${column}`), `${column} is typed`);
  }
  // The three fields are either all empty or belong to a live, top-level action.
  assert.match(actionSql, /production_room_messages_action_status_check/);
  assert.match(actionSql, /action_status IN \('open','done','cancelled'\)/);
  assert.match(actionSql, /production_room_messages_action_shape_check/);
  assert.match(
    actionSql,
    /category = 'action_item' AND parent_id IS NULL/,
    "action fields cannot ride on a reply",
  );
  // Overdue work is found by an index rather than a scan.
  assert.match(actionSql, /ix_production_room_messages_action_due/);
  assert.match(actionSql, /action_status <> 'done'/);
});

test("action items are written only through their two functions", () => {
  const rpcs = [
    ["set_production_room_action", "uuid,uuid,timestamptz,text"],
    ["post_production_room_action_item", "text,uuid,timestamptz"],
  ];
  for (const [rpc, signature] of rpcs) {
    assert.match(actionSql, new RegExp(`CREATE OR REPLACE FUNCTION public\\.${rpc}`), `${rpc} is defined`);
    assert.match(actionSql, new RegExp(`SECURITY DEFINER`), `${rpc} runs as definer`);
    assert.match(
      actionSql,
      new RegExp(`REVOKE ALL ON FUNCTION public\\.${rpc}\\(${signature}\\) FROM PUBLIC, anon;`),
      `${rpc} is closed to anon`,
    );
    assert.match(
      actionSql,
      new RegExp(`GRANT EXECUTE ON FUNCTION public\\.${rpc}\\(${signature}\\) TO authenticated;`),
      `${rpc} is open to members`,
    );
  }
  // The status is validated before it can reach the row.
  assert.match(actionSql, /v_status NOT IN \('open','done','cancelled'\)/);
  // An owner who is neither author nor admin may only change the status.
  assert.match(actionSql, /v_is_owner AND NOT v_is_admin AND NOT v_is_author/);
  // The room screen calls both functions by name.
  assert.match(workspace, /rpc\("set_production_room_action"/);
  assert.match(workspace, /rpc\("post_production_room_action_item"/);
});

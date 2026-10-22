import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";

const read = (path) => readFileSync(new URL(path, import.meta.url), "utf8");

const roomSql = read("../supabase-production-room-phase11a.sql");
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

test("the production room writes only through its nine functions", () => {
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

import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";

const read = (path) => readFileSync(new URL(path, import.meta.url), "utf8");

const sql = read("../supabase-production-room-phase11a.sql");
const types = read("../src/types/production-room.ts");
const api = read("../src/lib/production-room-api.ts");
const routes = [
  "../src/app/api/production-room/shared.ts",
  "../src/app/api/production-room/route.ts",
  "../src/app/api/production-room/messages/[id]/route.ts",
  "../src/app/api/production-room/read/route.ts",
  "../src/app/api/production-room/members/route.ts",
  "../src/app/api/production-room/members/[userId]/route.ts",
].map(read).join("\n");
const ui = read("../src/components/production-room/ProductionRoom.tsx");
const page = read("../src/app/[locale]/production-room/page.tsx");
const phaseDoc = read("../docs/PHASE9_ESTIMATOR_AND_PRODUCTION_COORDINATION.md");

const categories = [
  "general_production",
  "urgent_blocker",
  "material_shortage",
  "machine_issue",
  "quality_issue",
  "schedule_change",
  "dispatch_dependency",
  "decision",
  "action_item",
];

const tables = [
  "production_room_members",
  "production_room_messages",
  "production_room_reads",
  "production_room_events",
];

test("Phase 11A tables are additive, RLS protected, and RPC-write only", () => {
  for (const table of tables) {
    assert.match(sql, new RegExp(`CREATE TABLE IF NOT EXISTS public\\.${table}`));
    assert.match(sql, new RegExp(`ALTER TABLE public\\.${table} ENABLE ROW LEVEL SECURITY`));
    assert.match(sql, new RegExp(`REVOKE INSERT,UPDATE,DELETE ON public\\.${table} FROM authenticated`));
  }
  assert.doesNotMatch(sql, /DROP TABLE|service_role/i);
});

test("SQL and TypeScript category contracts are identical", () => {
  const typeList = types.match(/PRODUCTION_ROOM_CATEGORIES = \[([\s\S]*?)\] as const;/)?.[1] || "";
  const parsed = [...typeList.matchAll(/"([^"]+)"/g)].map((match) => match[1]);
  assert.deepEqual(parsed, categories);
  for (const category of categories) {
    assert.match(sql, new RegExp(`'${category}'`));
    assert.match(types, new RegExp(`"${category}"`));
  }
});

test("room access follows explicit membership, not unrelated operator roles", () => {
  assert.match(sql, /public\.is_production_room_member\(\)/);
  assert.match(sql, /public\.is_production_room_admin\(\)/);
  assert.match(sql, /Production room members read messages/);
  assert.match(sql, /has_inquiry_role\(ARRAY\['inquiry_admin'\]\)/);
  assert.doesNotMatch(sql, /inquiry_estimator|inquiry_viewer|inquiry_bridge/);
  assert.match(sql, /At least one active production room admin is required/);
});

test("messages are bounded, threaded, audited, and decision pins are constrained", () => {
  assert.match(sql, /BETWEEN 1 AND 4000/);
  assert.ok((sql.match(/interval '30 minutes'/g) || []).length >= 2);
  assert.match(sql, /Only a top-level decision can be pinned/);
  assert.match(sql, /category='decision'/);
  assert.match(sql, /This message was deleted\./);
  assert.match(sql, /message_created/);
  assert.match(sql, /message_edited/);
  assert.match(sql, /message_deleted/);
  assert.match(sql, /message_pinned/);
  assert.match(sql, /member_registered/);
  assert.match(sql, /FOR UPDATE/);
});

test("API uses the caller's token, normalizes input, and invokes reviewed RPCs", () => {
  assert.match(api, /auth\.getUser\(token\)/);
  assert.match(api, /normalize\("NFKC"\)/);
  assert.match(api, /\.limit\(500\)/);
  for (const rpc of [
    "post_production_room_message",
    "edit_production_room_message",
    "delete_production_room_message",
    "set_production_room_message_pinned",
    "mark_production_room_read",
    "register_production_room_member",
    "set_production_room_member_active",
  ]) {
    assert.match(api, new RegExp(rpc));
    assert.match(sql, new RegExp(`public\\.${rpc}`));
  }
  assert.doesNotMatch(api + routes, /service.role|SERVICE_ROLE/i);
});

test("every production-room mutation route checks same-origin delivery", () => {
  assert.match(routes, /sec-fetch-site/);
  assert.match(routes, /same-origin/);
  assert.ok((routes.match(/assertSameOrigin\(request\)/g) || []).length >= 6);
  assert.match(routes, /Cache-Control.*private, no-store/);
});

test("room UI exposes Phase 11B controls and keeps 11C scope explicit", () => {
  assert.match(ui, /Production coordination room/);
  assert.match(ui, /Explicit membership/);
  assert.match(ui, /Pinned decisions/);
  assert.match(ui, /Mark room read/);
  assert.match(ui, /maxLength=\{4000\}/);
  assert.match(ui, /Phase 11C scope/);
  assert.match(page, /path: "\/production-room"/);
  assert.match(page, /OperatorOpener/);
  assert.match(page, /route="production-room"/);
});

test("phase record keeps Phase 9E completion and Phase 11 live-database status explicit", () => {
  assert.match(phaseDoc, /supabase-production-room-phase11a\.sql/);
  assert.match(phaseDoc, /Phases 9A–9E are implemented and live-verified/);
  assert.match(phaseDoc, /owner confirmed completion on 19 October 2026/);
  assert.match(phaseDoc, /migration version `20261019043256`/);
  assert.match(phaseDoc, /4 tables, 4 RLS tables, 4 policies, 9 functions/);
  assert.match(phaseDoc, /Register the first room admin\/members/);
  assert.match(phaseDoc, /Phase 11C/);
  assert.doesNotMatch(ui, /Phase 9E Windows bridge verification/);
});

/**
 * The production room contract.
 *
 * These shapes mirror `production_room_messages`, `production_room_members`
 * and `production_room_reads` exactly, because the database is the source of
 * truth here: the column checks decide what a category may be, a pin may point
 * at, and how long an author may still edit or delete. Nothing in the browser
 * is allowed to be more permissive than the constraint it mirrors.
 */

export const PRODUCTION_ROOM_CATEGORIES = [
  "general_production",
  "urgent_blocker",
  "material_shortage",
  "machine_issue",
  "quality_issue",
  "schedule_change",
  "dispatch_dependency",
  "decision",
  "action_item",
] as const;

export type ProductionRoomCategory = (typeof PRODUCTION_ROOM_CATEGORIES)[number];

export type ProductionRoomMessage = {
  id: string;
  parent_id: string | null;
  author_user_id: string;
  category: ProductionRoomCategory;
  body: string;
  is_pinned: boolean;
  pinned_by: string | null;
  pinned_at: string | null;
  created_at: string;
  edited_at: string | null;
  edit_count: number;
  deleted_by: string | null;
  deleted_at: string | null;
};

export type ProductionRoomMember = {
  user_id: string;
  display_name: string;
  department: string;
  is_room_admin: boolean;
  active: boolean;
  created_at: string;
  updated_at: string;
};

export type ProductionRoomRead = {
  user_id: string;
  last_read_at: string;
  last_read_message_id: string | null;
  updated_at: string;
};

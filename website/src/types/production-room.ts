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

export const PRODUCTION_ROOM_CATEGORY_LABELS: Record<ProductionRoomCategory, string> = {
  general_production: "General production",
  urgent_blocker: "Urgent blocker",
  material_shortage: "Material shortage",
  machine_issue: "Machine issue",
  quality_issue: "Quality issue",
  schedule_change: "Schedule change",
  dispatch_dependency: "Dispatch dependency",
  decision: "Decision",
  action_item: "Action item",
};

export type ProductionRoomMember = {
  user_id: string;
  display_name: string;
  department: string;
  is_room_admin: boolean;
  active: boolean;
  created_by: string;
  created_at: string;
  updated_by: string;
  updated_at: string;
};

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

export type ProductionRoomRead = {
  user_id: string;
  last_read_at: string;
  last_read_message_id: string | null;
  updated_at: string;
};

export type ProductionRoomAccess = {
  userId: string;
  member: ProductionRoomMember | null;
  isMember: boolean;
  isRoomAdmin: boolean;
  canManageMembers: boolean;
};

export type ProductionRoomFilters = {
  category?: ProductionRoomCategory;
  query?: string;
  from?: string;
  to?: string;
  pinnedOnly?: boolean;
};

export type ProductionRoomData = {
  access: ProductionRoomAccess;
  members: ProductionRoomMember[];
  messages: ProductionRoomMessage[];
  pinned: ProductionRoomMessage[];
  readPosition: ProductionRoomRead | null;
  unreadCount: number;
};

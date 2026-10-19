import { authorInitials } from "@/lib/global-chat";
import type { UserProfile } from "@/types/user-profile";

export type AvatarIconOption = {
  id: string;
  label: string;
  symbol: string;
};

export type AvatarColorOption = {
  id: string;
  label: string;
  className: string;
  swatchClassName: string;
};

export const AVATAR_ICON_OPTIONS: AvatarIconOption[] = [
  { id: "initials", label: "My initials", symbol: "Aa" },
  { id: "smile", label: "Friendly", symbol: "😊" },
  { id: "star", label: "Star", symbol: "⭐" },
  { id: "gem", label: "Crystal", symbol: "💎" },
  { id: "home", label: "Home", symbol: "🏠" },
  { id: "building", label: "Building", symbol: "🏢" },
  { id: "ruler", label: "Design", symbol: "📐" },
  { id: "tools", label: "Workshop", symbol: "🛠️" },
  { id: "leaf", label: "Leaf", symbol: "🌿" },
  { id: "sun", label: "Sun", symbol: "☀️" },
  { id: "wave", label: "Wave", symbol: "🌊" },
  { id: "shield", label: "Shield", symbol: "🛡️" },
  { id: "sparkles", label: "Sparkles", symbol: "✨" },
];

export const AVATAR_COLOR_OPTIONS: AvatarColorOption[] = [
  {
    id: "brand",
    label: "ProGlass",
    className: "border-brand-400/50 bg-brand-500/20 text-brand-100",
    swatchClassName: "bg-brand-400",
  },
  {
    id: "emerald",
    label: "Emerald",
    className: "border-emerald-400/50 bg-emerald-500/20 text-emerald-100",
    swatchClassName: "bg-emerald-400",
  },
  {
    id: "sky",
    label: "Sky",
    className: "border-sky-400/50 bg-sky-500/20 text-sky-100",
    swatchClassName: "bg-sky-400",
  },
  {
    id: "amber",
    label: "Amber",
    className: "border-amber-400/50 bg-amber-500/20 text-amber-100",
    swatchClassName: "bg-amber-400",
  },
  {
    id: "rose",
    label: "Rose",
    className: "border-rose-400/50 bg-rose-500/20 text-rose-100",
    swatchClassName: "bg-rose-400",
  },
  {
    id: "violet",
    label: "Violet",
    className: "border-violet-400/50 bg-violet-500/20 text-violet-100",
    swatchClassName: "bg-violet-400",
  },
  {
    id: "slate",
    label: "Slate",
    className: "border-slate-400/50 bg-slate-500/25 text-slate-100",
    swatchClassName: "bg-slate-400",
  },
];

const ICON_IDS = new Set(AVATAR_ICON_OPTIONS.map((option) => option.id));
const COLOR_IDS = new Set(AVATAR_COLOR_OPTIONS.map((option) => option.id));

type UnknownRecord = Record<string, unknown>;

function recordFrom(value: unknown): UnknownRecord | null {
  return typeof value === "object" && value !== null && !Array.isArray(value) ? (value as UnknownRecord) : null;
}

function cleanDisplayName(value: unknown, fallback: string): string {
  const name = typeof value === "string" ? value.replace(/\s+/g, " ").trim() : "";
  return (name || fallback || "ProGlass user").slice(0, 60);
}

export function defaultAvatarColor(userId: string): string {
  let hash = 0;
  const safeUserId = typeof userId === "string" && userId ? userId : "ProGlass";
  for (const char of safeUserId) hash = (hash * 31 + char.charCodeAt(0)) >>> 0;
  return AVATAR_COLOR_OPTIONS[hash % AVATAR_COLOR_OPTIONS.length]?.id ?? "brand";
}

export function normalizeAvatarIcon(value: unknown): string {
  return typeof value === "string" && ICON_IDS.has(value) ? value : "initials";
}

export function normalizeAvatarColor(value: unknown, userId = ""): string {
  return typeof value === "string" && COLOR_IDS.has(value) ? value : defaultAvatarColor(userId);
}

export function normalizeUserProfile(
  value: unknown,
  fallback: { userId?: string; displayName?: string } = {},
): UserProfile | null {
  const record = recordFrom(value);
  if (!record) return null;
  const userId =
    typeof record.user_id === "string" && record.user_id.trim() ? record.user_id.trim() : (fallback.userId ?? "");
  if (!userId) return null;
  return {
    user_id: userId,
    display_name: cleanDisplayName(record.display_name, fallback.displayName ?? "ProGlass user"),
    avatar_icon: normalizeAvatarIcon(record.avatar_icon),
    avatar_color: normalizeAvatarColor(record.avatar_color, userId),
    updated_at: typeof record.updated_at === "string" ? record.updated_at : undefined,
  };
}

export function normalizeUserProfiles(value: unknown): UserProfile[] {
  if (!Array.isArray(value)) return [];
  return value
    .map((profile) => normalizeUserProfile(profile))
    .filter((profile): profile is UserProfile => Boolean(profile));
}

export function profileFromMetadata(input: {
  userId: string;
  displayName: string;
  metadata?: Record<string, unknown> | null;
}): UserProfile {
  const metadata = recordFrom(input.metadata);
  return {
    user_id: input.userId,
    display_name: cleanDisplayName(metadata?.full_name ?? metadata?.name, input.displayName),
    avatar_icon: normalizeAvatarIcon(metadata?.avatar_icon),
    avatar_color: normalizeAvatarColor(metadata?.avatar_color, input.userId),
  };
}

export function fallbackUserProfile(userId: string, displayName: string): UserProfile {
  return {
    user_id: userId,
    display_name: cleanDisplayName(displayName, "ProGlass user"),
    avatar_icon: "initials",
    avatar_color: defaultAvatarColor(userId),
  };
}

export function avatarColorClass(colorId: string, userId = ""): string {
  const normalized = normalizeAvatarColor(colorId, userId);
  return (
    AVATAR_COLOR_OPTIONS.find((option) => option.id === normalized)?.className ??
    "border-brand-500/50 bg-brand-500/20 text-brand-100"
  );
}

export function avatarSymbol(profile: UserProfile): string {
  const icon = normalizeAvatarIcon(profile.avatar_icon);
  if (icon === "initials") return authorInitials(profile.display_name);
  return AVATAR_ICON_OPTIONS.find((option) => option.id === icon)?.symbol ?? authorInitials(profile.display_name);
}

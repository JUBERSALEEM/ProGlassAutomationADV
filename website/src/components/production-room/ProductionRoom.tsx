"use client";

import { useCallback, useEffect, useMemo, useState, type FormEvent } from "react";
import { usePathname, useRouter, useSearchParams } from "next/navigation";

import { Badge } from "@/components/ui/Badge";
import { LocaleLink } from "@/components/ui/LocaleLink";
import { useOperatorSession } from "@/lib/useOperatorSession";
import {
  PRODUCTION_ROOM_CATEGORIES,
  PRODUCTION_ROOM_CATEGORY_LABELS,
  type ProductionRoomCategory,
  type ProductionRoomData,
  type ProductionRoomMember,
  type ProductionRoomMessage,
} from "@/types/production-room";

const EDIT_WINDOW_MS = 30 * 60 * 1000;

class RoomApiError extends Error {
  code: string;

  constructor(code: string, message: string) {
    super(message);
    this.code = code;
  }
}

type BadgeTone = "neutral" | "accent" | "success" | "warning" | "danger";

function categoryTone(category: ProductionRoomCategory): BadgeTone {
  if (category === "urgent_blocker") return "danger";
  if (category === "material_shortage" || category === "machine_issue" || category === "quality_issue") {
    return "warning";
  }
  if (category === "decision") return "success";
  if (category === "action_item" || category === "schedule_change" || category === "dispatch_dependency") {
    return "accent";
  }
  return "neutral";
}

function formatTime(value: string): string {
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? value : date.toLocaleString();
}

export function ProductionRoom() {
  const router = useRouter();
  const pathname = usePathname();
  const searchParams = useSearchParams();
  const { checking, authToken, userEmail, isAuthRequired, requireAuth, signOut } = useOperatorSession();

  const [data, setData] = useState<ProductionRoomData | null>(null);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<{ code: string; message: string } | null>(null);
  const [error, setError] = useState("");
  const [notice, setNotice] = useState("");
  const [busyKey, setBusyKey] = useState("");

  const [searchInput, setSearchInput] = useState(() => searchParams.get("q") || "");
  const [query, setQuery] = useState(() => searchParams.get("q") || "");
  const [categoryFilter, setCategoryFilter] = useState(() => searchParams.get("category") || "");
  const [dateFrom, setDateFrom] = useState(() => searchParams.get("from") || "");
  const [dateTo, setDateTo] = useState(() => searchParams.get("to") || "");
  const [pinnedOnly, setPinnedOnly] = useState(() => {
    const value = searchParams.get("pinned");
    return value === "1" || value === "true";
  });

  const [composerCategory, setComposerCategory] = useState<ProductionRoomCategory>("general_production");
  const [composerBody, setComposerBody] = useState("");
  const [replyTo, setReplyTo] = useState<string | null>(null);
  const [replyBody, setReplyBody] = useState("");
  const [editingId, setEditingId] = useState<string | null>(null);
  const [editBody, setEditBody] = useState("");

  const [memberEmail, setMemberEmail] = useState("");
  const [memberDisplayName, setMemberDisplayName] = useState("");
  const [memberDepartment, setMemberDepartment] = useState("");
  const [memberIsAdmin, setMemberIsAdmin] = useState(false);
  const [memberActive, setMemberActive] = useState(true);
  const [nowMs, setNowMs] = useState(() => Date.now());

  const busy = busyKey !== "";

  const requestRoom = useCallback(
    async <T,>(url: string, init?: RequestInit): Promise<T> => {
      if (!authToken) throw new RoomApiError("UNAUTHORIZED", "Sign in to continue.");
      const response = await fetch(url, {
        ...init,
        cache: "no-store",
        headers: {
          Authorization: `Bearer ${authToken}`,
          "Content-Type": "application/json",
          ...(init?.headers || {}),
        },
      });
      const payload = (await response.json().catch(() => null)) as {
        ok?: boolean;
        data?: T;
        error?: { code: string; message: string };
      } | null;

      if (response.status === 401) {
        requireAuth();
        throw new RoomApiError("UNAUTHORIZED", payload?.error?.message || "Session expired or invalid.");
      }
      if (!response.ok || !payload || payload.ok !== true) {
        throw new RoomApiError(
          payload?.error?.code || "TEMPORARY_FAILURE",
          payload?.error?.message || "Production room request failed.",
        );
      }
      return payload.data as T;
    },
    [authToken, requireAuth],
  );

  const load = useCallback(async () => {
    if (!authToken) return;
    setLoading(true);
    try {
      const params = new URLSearchParams();
      if (categoryFilter) params.set("category", categoryFilter);
      if (query) params.set("q", query);
      if (dateFrom) params.set("from", dateFrom);
      if (dateTo) params.set("to", dateTo);
      if (pinnedOnly) params.set("pinned", "1");
      const queryString = params.toString();
      const next = await requestRoom<ProductionRoomData>(
        `/api/production-room${queryString ? `?${queryString}` : ""}`,
      );
      setData(next);
      setLoadError(null);
      setError("");
    } catch (loadFailure) {
      if (loadFailure instanceof RoomApiError && loadFailure.code === "UNAUTHORIZED") return;
      setLoadError({
        code: loadFailure instanceof RoomApiError ? loadFailure.code : "TEMPORARY_FAILURE",
        message: loadFailure instanceof Error ? loadFailure.message : "Unable to load the production room.",
      });
    } finally {
      setLoading(false);
    }
  }, [authToken, categoryFilter, dateFrom, dateTo, pinnedOnly, query, requestRoom]);

  useEffect(() => {
    const timer = window.setTimeout(() => setQuery(searchInput.trim()), 300);
    return () => window.clearTimeout(timer);
  }, [searchInput]);

  useEffect(() => {
    const next = new URLSearchParams();
    if (query) next.set("q", query);
    if (categoryFilter) next.set("category", categoryFilter);
    if (dateFrom) next.set("from", dateFrom);
    if (dateTo) next.set("to", dateTo);
    if (pinnedOnly) next.set("pinned", "1");
    const queryString = next.toString();
    router.replace(queryString ? `${pathname}?${queryString}` : pathname, { scroll: false });
  }, [categoryFilter, dateFrom, dateTo, pathname, pinnedOnly, query, router]);

  useEffect(() => {
    // Operator data loads follow the established session-dashboard pattern.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    if (!checking && authToken) void load();
  }, [authToken, checking, load]);

  useEffect(() => {
    const timer = window.setInterval(() => setNowMs(Date.now()), 60_000);
    return () => window.clearInterval(timer);
  }, []);

  const mutate = useCallback(
    async (key: string, action: () => Promise<unknown>, success: string): Promise<boolean> => {
      setBusyKey(key);
      setError("");
      setNotice("");
      try {
        await action();
        setNotice(success);
        await load();
        return true;
      } catch (mutationFailure) {
        setError(mutationFailure instanceof Error ? mutationFailure.message : "Update failed.");
        return false;
      } finally {
        setBusyKey("");
      }
    },
    [load],
  );

  const memberById = useMemo(
    () => new Map((data?.members || []).map((member) => [member.user_id, member])),
    [data?.members],
  );

  const threads = useMemo(() => {
    const messages = data?.messages || [];
    const replies = new Map<string, ProductionRoomMessage[]>();
    for (const message of messages) {
      if (!message.parent_id) continue;
      const current = replies.get(message.parent_id) || [];
      current.push(message);
      replies.set(message.parent_id, current);
    }
    return messages
      .filter((message) => !message.parent_id)
      .slice()
      .sort((a, b) => new Date(b.created_at).getTime() - new Date(a.created_at).getTime())
      .map((top) => ({ top, replies: replies.get(top.id) || [] }));
  }, [data?.messages]);

  if (checking) {
    return (
      <section className="bg-ink-950 pb-20 text-text-primary">
        <div className="shell py-10 text-sm text-text-muted">Verifying production room access…</div>
      </section>
    );
  }

  if (isAuthRequired || !authToken) {
    return (
      <main className="min-h-[70vh] bg-ink-950 px-4 py-16 text-text-primary">
        <section className="card-pad mx-auto w-full max-w-md rounded-panel border border-line-strong/80 bg-ink-900 text-center shadow-raised">
          <Badge tone="warning">Restricted production room</Badge>
          <h2 className="mt-4 text-xl font-bold tracking-tight">Sign in required</h2>
          <p className="mt-2 text-xs leading-relaxed text-text-muted">
            Explicit membership is required for the Production coordination room. Signing in alone does not
            grant access; an approved room membership must also be active.
          </p>
          <div className="mt-5 flex flex-col gap-3">
            <LocaleLink
              href={`/reports/login?redirect=${encodeURIComponent("/production-room")}`}
              className="flex h-11 items-center justify-center rounded-xl bg-brand-400 px-5 text-sm font-semibold text-accent-ink hover:bg-brand-300"
            >
              Sign in as an approved operator
            </LocaleLink>
            <LocaleLink
              href="/job-orders"
              className="flex h-10 items-center justify-center rounded-xl border border-line-strong/70 bg-ink-950/60 text-xs font-semibold text-text-muted hover:text-text-primary"
            >
              Open Job Orders
            </LocaleLink>
          </div>
        </section>
      </main>
    );
  }

  if (loadError?.code === "NOT_CONFIGURED") {
    return (
      <section className="bg-ink-950 pb-20 text-text-primary">
        <div className="shell py-10">
          <div className="rounded-panel border border-state-warning/40 bg-ink-900 p-6">
            <Badge tone="warning">Not installed yet</Badge>
            <h2 className="mt-4 text-xl font-bold">Production room schema is not installed in this deployment</h2>
            <p className="mt-2 max-w-2xl text-sm leading-relaxed text-text-muted">
              The repository implementation is present, but the additive migration
              {" "}
              <code className="font-mono text-brand-200">supabase-production-room-phase11a.sql</code>
              {" "}
              is not available in the database used by this deployment, or this deployment is not configured to use the live project.
            </p>
            <button
              type="button"
              onClick={() => void load()}
              className="mt-5 rounded-xl border border-brand-400/50 px-4 py-2 text-xs font-bold text-brand-200 hover:bg-brand-500/10"
            >
              Retry
            </button>
          </div>
        </div>
      </section>
    );
  }

  if (loadError && !data) {
    return (
      <section className="bg-ink-950 pb-20 text-text-primary">
        <div className="shell py-10">
          <div className="rounded-panel border border-state-danger/40 bg-ink-900 p-6">
            <Badge tone="danger">Unable to load</Badge>
            <p role="alert" className="mt-4 text-sm text-state-danger">{loadError.message}</p>
            <button
              type="button"
              onClick={() => void load()}
              className="mt-5 rounded-xl border border-brand-400/50 px-4 py-2 text-xs font-bold text-brand-200 hover:bg-brand-500/10"
            >
              Retry
            </button>
          </div>
        </div>
      </section>
    );
  }

  if (!data) {
    return (
      <section className="bg-ink-950 pb-20 text-text-primary">
        <div className="shell py-10 text-sm text-text-muted">Loading Production coordination room…</div>
      </section>
    );
  }

  const roomData = data;
  const access = roomData.access;
  const activeMembers = roomData.members.filter((member) => member.active);

  function authorName(message: ProductionRoomMessage): string {
    const member = memberById.get(message.author_user_id);
    if (member) return member.display_name;
    return message.author_user_id === access.userId ? userEmail || "You" : "Former member";
  }

  function authorDepartment(message: ProductionRoomMessage): string {
    return memberById.get(message.author_user_id)?.department || "Production";
  }

  function isUnread(message: ProductionRoomMessage): boolean {
    if (message.author_user_id === access.userId || message.deleted_at) return false;
    if (!data?.readPosition?.last_read_at) return true;
    return new Date(message.created_at).getTime() > new Date(data.readPosition.last_read_at).getTime();
  }

  function canEdit(message: ProductionRoomMessage): boolean {
    return (
      message.author_user_id === access.userId &&
      !message.deleted_at &&
      nowMs - new Date(message.created_at).getTime() < EDIT_WINDOW_MS
    );
  }

  function canDelete(message: ProductionRoomMessage): boolean {
    if (message.deleted_at) return false;
    return access.isRoomAdmin || canEdit(message);
  }

  function canPin(message: ProductionRoomMessage): boolean {
    return access.isRoomAdmin && !message.parent_id && message.category === "decision" && !message.deleted_at;
  }

  async function submitTopLevel(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const saved = await mutate(
      "post-top-level",
      () =>
        requestRoom("/api/production-room", {
          method: "POST",
          body: JSON.stringify({ body: composerBody, category: composerCategory }),
        }),
      "Message posted and audited.",
    );
    if (saved) setComposerBody("");
  }

  async function submitReply(parentId: string) {
    const saved = await mutate(
      `reply-${parentId}`,
      () =>
        requestRoom("/api/production-room", {
          method: "POST",
          body: JSON.stringify({ body: replyBody, parentId }),
        }),
      "Reply posted and audited.",
    );
    if (saved) {
      setReplyBody("");
      setReplyTo(null);
    }
  }

  async function saveEdit(message: ProductionRoomMessage) {
    const saved = await mutate(
      `edit-${message.id}`,
      () =>
        requestRoom(`/api/production-room/messages/${message.id}`, {
          method: "PATCH",
          body: JSON.stringify({ action: "edit", body: editBody }),
        }),
      "Message updated and audited.",
    );
    if (saved) setEditingId(null);
  }

  async function deleteMessage(message: ProductionRoomMessage) {
    if (!window.confirm("Delete this message? Replies will remain visible under the tombstone.")) return;
    await mutate(
      `delete-${message.id}`,
      () =>
        requestRoom(`/api/production-room/messages/${message.id}`, {
          method: "DELETE",
        }),
      "Message deleted and audited.",
    );
  }

  async function togglePin(message: ProductionRoomMessage) {
    await mutate(
      `pin-${message.id}`,
      () =>
        requestRoom(`/api/production-room/messages/${message.id}`, {
          method: "PATCH",
          body: JSON.stringify({ action: "pin", pinned: !message.is_pinned }),
        }),
      message.is_pinned ? "Decision unpinned." : "Decision pinned.",
    );
  }

  async function markRead() {
    await mutate(
      "mark-read",
      () =>
        requestRoom("/api/production-room/read", {
          method: "POST",
          body: JSON.stringify({}),
        }),
      "Production room marked as read.",
    );
  }

  async function submitMember(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const saved = await mutate(
      "register-member",
      () =>
        requestRoom("/api/production-room/members", {
          method: "POST",
          body: JSON.stringify({
            email: memberEmail,
            displayName: memberDisplayName,
            department: memberDepartment,
            isRoomAdmin: memberIsAdmin,
            active: memberActive,
          }),
        }),
      "Production room membership saved and audited.",
    );
    if (saved) {
      setMemberEmail("");
      setMemberDisplayName("");
      setMemberDepartment("");
      setMemberIsAdmin(false);
      setMemberActive(true);
    }
  }

  async function toggleMemberActive(member: ProductionRoomMember) {
    await mutate(
      `member-${member.user_id}`,
      () =>
        requestRoom(`/api/production-room/members/${member.user_id}`, {
          method: "PATCH",
          body: JSON.stringify({ active: !member.active }),
        }),
      member.active ? "Member deactivated." : "Member reactivated.",
    );
  }

  function renderMemberDirectory() {
    return (
      <section className="rounded-panel border border-line-strong/80 bg-ink-900 p-5">
        <div className="flex items-center justify-between gap-3">
          <h3 className="font-bold">Room members</h3>
          <Badge tone="accent">{activeMembers.length} active</Badge>
        </div>
        <div className="mt-4 space-y-3">
          {roomData.members.map((member) => (
            <article key={member.user_id} className="rounded-2xl border border-line-strong/70 bg-ink-950 p-3">
              <div className="flex flex-wrap items-start justify-between gap-2">
                <div>
                  <p className="text-sm font-semibold">{member.display_name}</p>
                  <p className="mt-0.5 text-2xs text-text-muted">{member.department}</p>
                </div>
                <div className="flex flex-wrap gap-1.5">
                  {member.is_room_admin ? <Badge tone="accent">Room admin</Badge> : null}
                  {!member.active ? <Badge tone="warning">Inactive</Badge> : null}
                </div>
              </div>
              {access.canManageMembers ? (
                <button
                  type="button"
                  disabled={busy}
                  onClick={() => void toggleMemberActive(member)}
                  className="mt-3 rounded-lg border border-line-strong px-2.5 py-1.5 text-3xs font-bold text-text-secondary hover:border-brand-400/60 hover:text-brand-200 disabled:opacity-50"
                >
                  {member.active ? "Deactivate" : "Reactivate"}
                </button>
              ) : null}
            </article>
          ))}
          {!roomData.members.length ? (
            <p className="text-xs leading-relaxed text-text-muted">No approved room members are registered yet.</p>
          ) : null}
        </div>
      </section>
    );
  }

  function renderMemberForm() {
    if (!access.canManageMembers) return null;
    return (
      <form onSubmit={(event) => void submitMember(event)} className="rounded-panel border border-line-strong/80 bg-ink-900 p-5">
        <h3 className="font-bold">Manage explicit membership</h3>
        <p className="mt-1 text-2xs leading-relaxed text-text-muted">
          Register an existing Auth user by email. Submitting the same email updates that person&apos;s room profile.
        </p>
        <div className="mt-4 grid gap-3">
          <label className="text-3xs font-bold uppercase tracking-[0.1em] text-text-faint">
            Auth email
            <input
              required
              type="email"
              value={memberEmail}
              onChange={(event) => setMemberEmail(event.target.value)}
              placeholder="department.head@example.com"
              className="mt-1 h-10 w-full rounded-xl border border-line-strong bg-ink-950 px-3 text-xs normal-case tracking-normal text-text-primary"
            />
          </label>
          <label className="text-3xs font-bold uppercase tracking-[0.1em] text-text-faint">
            Display name
            <input
              required
              maxLength={120}
              value={memberDisplayName}
              onChange={(event) => setMemberDisplayName(event.target.value)}
              placeholder="Full name"
              className="mt-1 h-10 w-full rounded-xl border border-line-strong bg-ink-950 px-3 text-xs normal-case tracking-normal text-text-primary"
            />
          </label>
          <label className="text-3xs font-bold uppercase tracking-[0.1em] text-text-faint">
            Department
            <input
              required
              maxLength={80}
              value={memberDepartment}
              onChange={(event) => setMemberDepartment(event.target.value)}
              placeholder="Cutting, tempering, dispatch…"
              className="mt-1 h-10 w-full rounded-xl border border-line-strong bg-ink-950 px-3 text-xs normal-case tracking-normal text-text-primary"
            />
          </label>
          <div className="grid grid-cols-2 gap-3 text-xs text-text-secondary">
            <label className="flex items-center gap-2 rounded-xl border border-line-strong/70 bg-ink-950 px-3 py-2.5">
              <input
                type="checkbox"
                checked={memberIsAdmin}
                onChange={(event) => setMemberIsAdmin(event.target.checked)}
              />
              Room admin
            </label>
            <label className="flex items-center gap-2 rounded-xl border border-line-strong/70 bg-ink-950 px-3 py-2.5">
              <input
                type="checkbox"
                checked={memberActive}
                onChange={(event) => setMemberActive(event.target.checked)}
              />
              Active
            </label>
          </div>
          <button
            type="submit"
            disabled={busy}
            className="rounded-xl bg-brand-400 px-4 py-2.5 text-xs font-bold text-accent-ink disabled:opacity-50"
          >
            {busyKey === "register-member" ? "Saving…" : "Save member"}
          </button>
        </div>
      </form>
    );
  }

  function renderMessage(message: ProductionRoomMessage, isReply = false) {
    const editing = editingId === message.id;
    const unread = isUnread(message);
    return (
      <article
        key={message.id}
        id={`production-message-${message.id}`}
        className={`rounded-2xl border p-4 ${
          unread
            ? "border-brand-400/70 bg-brand-500/5"
            : "border-line-strong/70 bg-ink-950"
        } ${isReply ? "ml-4 sm:ml-8" : ""}`}
      >
        <div className="flex flex-wrap items-start justify-between gap-3">
          <div>
            <p className="text-sm font-semibold">{authorName(message)}</p>
            <p className="mt-0.5 text-2xs text-text-muted">
              {authorDepartment(message)} · <time dateTime={message.created_at}>{formatTime(message.created_at)}</time>
            </p>
          </div>
          <div className="flex flex-wrap gap-1.5">
            <Badge tone={categoryTone(message.category)}>
              {PRODUCTION_ROOM_CATEGORY_LABELS[message.category]}
            </Badge>
            {unread ? <Badge tone="accent">New</Badge> : null}
            {message.is_pinned ? <Badge tone="success">Pinned decision</Badge> : null}
            {message.edit_count > 0 ? <Badge>Edited</Badge> : null}
          </div>
        </div>

        {editing ? (
          <div className="mt-3 grid gap-2">
            <textarea
              aria-label="Edit production room message"
              maxLength={4000}
              value={editBody}
              onChange={(event) => setEditBody(event.target.value)}
              className="min-h-28 w-full rounded-xl border border-line-strong bg-ink-900 p-3 text-sm text-text-primary"
            />
            <div className="flex items-center justify-between gap-3">
              <span className="text-3xs text-text-faint">{editBody.length}/4000</span>
              <div className="flex gap-2">
                <button
                  type="button"
                  disabled={busy}
                  onClick={() => setEditingId(null)}
                  className="rounded-lg border border-line-strong px-3 py-1.5 text-3xs font-bold text-text-secondary"
                >
                  Cancel
                </button>
                <button
                  type="button"
                  disabled={busy || !editBody.trim()}
                  onClick={() => void saveEdit(message)}
                  className="rounded-lg bg-brand-400 px-3 py-1.5 text-3xs font-bold text-accent-ink disabled:opacity-50"
                >
                  Save edit
                </button>
              </div>
            </div>
          </div>
        ) : (
          <p className={`mt-3 whitespace-pre-wrap text-sm leading-relaxed ${message.deleted_at ? "italic text-text-muted" : "text-text-secondary"}`}>
            {message.body}
          </p>
        )}

        {!editing ? (
          <div className="mt-3 flex flex-wrap gap-2">
            {!isReply && !message.deleted_at ? (
              <button
                type="button"
                disabled={busy}
                onClick={() => {
                  setReplyTo(replyTo === message.id ? null : message.id);
                  setReplyBody("");
                }}
                className="rounded-lg border border-line-strong px-3 py-1.5 text-3xs font-bold text-text-secondary hover:border-brand-400/60 hover:text-brand-200 disabled:opacity-50"
              >
                Reply
              </button>
            ) : null}
            {canEdit(message) ? (
              <button
                type="button"
                disabled={busy}
                onClick={() => {
                  setEditingId(message.id);
                  setEditBody(message.body);
                }}
                className="rounded-lg border border-line-strong px-3 py-1.5 text-3xs font-bold text-text-secondary hover:border-brand-400/60 hover:text-brand-200 disabled:opacity-50"
              >
                Edit
              </button>
            ) : null}
            {canPin(message) || message.is_pinned ? (
              <button
                type="button"
                disabled={busy || !canPin(message)}
                onClick={() => void togglePin(message)}
                className="rounded-lg border border-line-strong px-3 py-1.5 text-3xs font-bold text-text-secondary hover:border-brand-400/60 hover:text-brand-200 disabled:opacity-50"
              >
                {message.is_pinned ? "Unpin" : "Pin decision"}
              </button>
            ) : null}
            {canDelete(message) ? (
              <button
                type="button"
                disabled={busy}
                onClick={() => void deleteMessage(message)}
                className="rounded-lg border border-state-danger/50 px-3 py-1.5 text-3xs font-bold text-state-danger disabled:opacity-50"
              >
                Delete
              </button>
            ) : null}
          </div>
        ) : null}
      </article>
    );
  }

  const statusBar = (
    <div className="flex flex-wrap items-center justify-between gap-3 rounded-panel border border-line-strong/80 bg-ink-900 p-4">
      <div className="flex flex-wrap items-center gap-2 text-xs text-text-muted">
        <Badge tone={access.isMember ? "success" : "warning"}>
          {access.isRoomAdmin ? "Room admin" : access.isMember ? "Active member" : "Membership required"}
        </Badge>
        <span>{access.member?.display_name || userEmail || "Signed in"}</span>
        {access.member?.department ? <span>· {access.member.department}</span> : null}
      </div>
      <div className="flex flex-wrap gap-2">
        <LocaleLink href="/job-orders" className="rounded-lg border border-line-strong px-3 py-2 text-3xs font-bold text-text-secondary hover:text-brand-200">
          Job Orders
        </LocaleLink>
        <LocaleLink href="/inquiries/estimator" className="rounded-lg border border-line-strong px-3 py-2 text-3xs font-bold text-text-secondary hover:text-brand-200">
          Estimator workspace
        </LocaleLink>
        <button
          type="button"
          disabled={loading || busy}
          onClick={() => void load()}
          className="rounded-lg border border-line-strong px-3 py-2 text-3xs font-bold text-text-secondary hover:text-brand-200 disabled:opacity-50"
        >
          {loading ? "Refreshing…" : "Refresh"}
        </button>
        <button
          type="button"
          disabled={busy}
          onClick={() => void signOut()}
          className="rounded-lg border border-line-strong px-3 py-2 text-3xs font-bold text-text-secondary hover:text-brand-200 disabled:opacity-50"
        >
          Sign out
        </button>
      </div>
    </div>
  );

  const feedback = (
    <>
      {notice ? <p role="status" className="mt-4 text-sm text-state-success">{notice}</p> : null}
      {error ? <p role="alert" className="mt-4 text-sm text-state-danger">{error}</p> : null}
      {loadError && data ? <p role="alert" className="mt-4 text-sm text-state-warning">{loadError.message}</p> : null}
    </>
  );

  if (!access.isMember) {
    return (
      <section className="bg-ink-950 pb-20 text-text-primary">
        <div className="shell py-8">
          {statusBar}
          {feedback}
          <div className="mt-6 rounded-panel border border-state-warning/40 bg-ink-900 p-6">
            <Badge tone="warning">Explicit membership</Badge>
            <h2 className="mt-4 text-xl font-bold">You are not an active member of this room</h2>
            <p className="mt-2 max-w-2xl text-sm leading-relaxed text-text-muted">
              Access is limited to approved department heads and room admins named in the production-room
              membership register. Customer, estimator, viewer, auditor, and bridge roles do not grant access
              automatically.
            </p>
            {access.member && !access.member.active ? (
              <p className="mt-3 text-sm text-state-warning">Your production-room membership is currently inactive.</p>
            ) : null}
            {!access.canManageMembers ? (
              <p className="mt-3 text-xs text-text-muted">
                Ask an inquiry admin or production room admin to register and activate your membership.
              </p>
            ) : null}
          </div>

          {access.canManageMembers ? (
            <div className="mt-6 grid gap-5 lg:grid-cols-2">
              {renderMemberForm()}
              {renderMemberDirectory()}
            </div>
          ) : null}
        </div>
      </section>
    );
  }

  const stats = [
    { label: "Active members", value: activeMembers.length.toString() },
    { label: "Unread messages", value: data.unreadCount.toString() },
    { label: "Pinned decisions", value: data.pinned.length.toString() },
    { label: "Visible threads", value: threads.length.toString() },
  ];

  return (
    <section className="bg-ink-950 pb-20 text-text-primary">
      <div className="shell py-8">
        {statusBar}
        {feedback}

        <div className="mt-6 grid grid-cols-2 gap-3.5 lg:grid-cols-4">
          {stats.map((stat) => (
            <article key={stat.label} className="rounded-panel border border-line-strong/80 bg-ink-900 p-4">
              <p className="text-3xs font-bold uppercase tracking-[0.12em] text-text-faint">{stat.label}</p>
              <p className="mt-2 font-mono text-3xl font-bold text-text-primary">{stat.value}</p>
            </article>
          ))}
        </div>

        {data.pinned.length ? (
          <section className="mt-6">
            <div className="flex flex-wrap items-center justify-between gap-3">
              <h2 className="text-lg font-bold">Pinned decisions</h2>
              <Badge tone="success">{data.pinned.length} pinned</Badge>
            </div>
            <div className="mt-3 grid gap-3.5 sm:grid-cols-2 xl:grid-cols-3">
              {data.pinned.map((message) => (
                <article key={message.id} className="rounded-panel border border-state-success/35 bg-ink-900 p-4">
                  <div className="flex items-start justify-between gap-3">
                    <div>
                      <p className="text-sm font-semibold">{authorName(message)}</p>
                      <p className="mt-0.5 text-2xs text-text-muted">{formatTime(message.created_at)}</p>
                    </div>
                    <Badge tone="success">Decision</Badge>
                  </div>
                  <p className="mt-3 line-clamp-4 whitespace-pre-wrap text-sm leading-relaxed text-text-secondary">
                    {message.body}
                  </p>
                  <a href={`#production-message-${message.id}`} className="mt-3 inline-block text-3xs font-bold text-brand-200 underline">
                    View in timeline
                  </a>
                </article>
              ))}
            </div>
          </section>
        ) : null}

        <div className="mt-6 grid gap-5 xl:grid-cols-[minmax(0,1fr)_340px]">
          <div className="min-w-0 space-y-5">
            <section className="rounded-panel border border-line-strong/80 bg-ink-900 p-5">
              <div className="flex flex-wrap items-center justify-between gap-3">
                <h2 className="font-bold">Find production updates</h2>
                <button
                  type="button"
                  onClick={() => {
                    setSearchInput("");
                    setQuery("");
                    setCategoryFilter("");
                    setDateFrom("");
                    setDateTo("");
                    setPinnedOnly(false);
                  }}
                  className="rounded-lg border border-line-strong px-3 py-1.5 text-3xs font-bold text-text-secondary hover:text-brand-200"
                >
                  Reset filters
                </button>
              </div>
              <div className="mt-4 grid gap-3 md:grid-cols-2">
                <label className="text-3xs font-bold uppercase tracking-[0.1em] text-text-faint md:col-span-2">
                  Search messages
                  <input
                    value={searchInput}
                    onChange={(event) => setSearchInput(event.target.value)}
                    placeholder="Search blockers, decisions, machines, materials…"
                    className="mt-1 h-10 w-full rounded-xl border border-line-strong bg-ink-950 px-3 text-xs normal-case tracking-normal text-text-primary"
                  />
                </label>
                <label className="text-3xs font-bold uppercase tracking-[0.1em] text-text-faint">
                  Category
                  <select
                    value={categoryFilter}
                    onChange={(event) => setCategoryFilter(event.target.value)}
                    className="mt-1 h-10 w-full rounded-xl border border-line-strong bg-ink-950 px-3 text-xs normal-case tracking-normal text-text-primary"
                  >
                    <option value="">All categories</option>
                    {PRODUCTION_ROOM_CATEGORIES.map((category) => (
                      <option key={category} value={category}>
                        {PRODUCTION_ROOM_CATEGORY_LABELS[category]}
                      </option>
                    ))}
                  </select>
                </label>
                <label className="flex items-end gap-2 rounded-xl border border-line-strong/70 bg-ink-950 px-3 py-2.5 text-xs text-text-secondary">
                  <input
                    type="checkbox"
                    checked={pinnedOnly}
                    onChange={(event) => setPinnedOnly(event.target.checked)}
                  />
                  Pinned decisions only
                </label>
                <label className="text-3xs font-bold uppercase tracking-[0.1em] text-text-faint">
                  From
                  <input
                    type="date"
                    value={dateFrom}
                    onChange={(event) => setDateFrom(event.target.value)}
                    className="mt-1 h-10 w-full rounded-xl border border-line-strong bg-ink-950 px-3 text-xs normal-case tracking-normal text-text-primary"
                  />
                </label>
                <label className="text-3xs font-bold uppercase tracking-[0.1em] text-text-faint">
                  To
                  <input
                    type="date"
                    value={dateTo}
                    onChange={(event) => setDateTo(event.target.value)}
                    className="mt-1 h-10 w-full rounded-xl border border-line-strong bg-ink-950 px-3 text-xs normal-case tracking-normal text-text-primary"
                  />
                </label>
              </div>
            </section>

            <form onSubmit={(event) => void submitTopLevel(event)} className="rounded-panel border border-line-strong/80 bg-ink-900 p-5">
              <div className="flex flex-wrap items-center justify-between gap-3">
                <h2 className="font-bold">Post to the production room</h2>
                <Badge tone="accent">Members only</Badge>
              </div>
              <div className="mt-4 grid gap-3">
                <label className="text-3xs font-bold uppercase tracking-[0.1em] text-text-faint">
                  Category
                  <select
                    value={composerCategory}
                    onChange={(event) => setComposerCategory(event.target.value as ProductionRoomCategory)}
                    className="mt-1 h-10 w-full rounded-xl border border-line-strong bg-ink-950 px-3 text-xs normal-case tracking-normal text-text-primary"
                  >
                    {PRODUCTION_ROOM_CATEGORIES.map((category) => (
                      <option key={category} value={category}>
                        {PRODUCTION_ROOM_CATEGORY_LABELS[category]}
                      </option>
                    ))}
                  </select>
                </label>
                <textarea
                  aria-label="Production room message"
                  required
                  maxLength={4000}
                  value={composerBody}
                  onChange={(event) => setComposerBody(event.target.value)}
                  placeholder="Share a production update, blocker, schedule change, decision, or action item…"
                  className="min-h-32 w-full rounded-xl border border-line-strong bg-ink-950 p-3 text-sm text-text-primary"
                />
                <div className="flex flex-wrap items-center justify-between gap-3">
                  <span className="text-3xs text-text-faint">{composerBody.length}/4000</span>
                  <button
                    type="submit"
                    disabled={busy || !composerBody.trim()}
                    className="rounded-xl bg-brand-400 px-4 py-2.5 text-xs font-bold text-accent-ink disabled:opacity-50"
                  >
                    {busyKey === "post-top-level" ? "Posting…" : "Post message"}
                  </button>
                </div>
              </div>
            </form>

            <section>
              <div className="mb-3 flex flex-wrap items-center justify-between gap-3">
                <h2 className="text-lg font-bold">Production timeline</h2>
                <div className="flex items-center gap-2">
                  <Badge>{threads.length} threads</Badge>
                  <button
                    type="button"
                    disabled={busy}
                    onClick={() => void markRead()}
                    className="rounded-lg border border-brand-400/50 px-3 py-1.5 text-3xs font-bold text-brand-200 disabled:opacity-50"
                  >
                    Mark room read
                  </button>
                </div>
              </div>

              <div className="space-y-5">
                {threads.map(({ top, replies }) => (
                  <section key={top.id} className="space-y-3">
                    {renderMessage(top)}
                    {replies.map((reply) => renderMessage(reply, true))}
                    {replyTo === top.id && !top.deleted_at ? (
                      <div className="ml-4 grid gap-2 rounded-2xl border border-brand-400/35 bg-ink-900 p-3 sm:ml-8">
                        <textarea
                          aria-label="Reply to production thread"
                          maxLength={4000}
                          value={replyBody}
                          onChange={(event) => setReplyBody(event.target.value)}
                          placeholder="Reply in this thread…"
                          className="min-h-24 w-full rounded-xl border border-line-strong bg-ink-950 p-3 text-sm text-text-primary"
                        />
                        <div className="flex items-center justify-between gap-3">
                          <span className="text-3xs text-text-faint">{replyBody.length}/4000</span>
                          <div className="flex gap-2">
                            <button
                              type="button"
                              disabled={busy}
                              onClick={() => setReplyTo(null)}
                              className="rounded-lg border border-line-strong px-3 py-1.5 text-3xs font-bold text-text-secondary"
                            >
                              Cancel
                            </button>
                            <button
                              type="button"
                              disabled={busy || !replyBody.trim()}
                              onClick={() => void submitReply(top.id)}
                              className="rounded-lg bg-brand-400 px-3 py-1.5 text-3xs font-bold text-accent-ink disabled:opacity-50"
                            >
                              Post reply
                            </button>
                          </div>
                        </div>
                      </div>
                    ) : null}
                  </section>
                ))}
                {!threads.length && !loading ? (
                  <p className="rounded-panel border border-line-strong/70 bg-ink-900 p-8 text-center text-sm text-text-muted">
                    No production messages match the current filters.
                  </p>
                ) : null}
                {loading ? <p className="text-center text-xs text-text-muted">Refreshing timeline…</p> : null}
              </div>
            </section>
          </div>

          <aside className="space-y-5">
            {renderMemberDirectory()}
            {renderMemberForm()}
            <section className="rounded-panel border border-line-strong/80 bg-ink-900 p-5">
              <h3 className="font-bold">Room controls</h3>
              <ul className="mt-3 list-disc space-y-2 pl-5 text-xs leading-relaxed text-text-muted">
                <li>Explicit membership is enforced by database RLS and RPC checks.</li>
                <li>Authors may edit or delete their own messages for 30 minutes.</li>
                <li>Room admins may moderate messages and pin top-level decisions.</li>
                <li>Posts, edits, deletions, pins, and membership changes create immutable audit events.</li>
                <li>Mentions, action owner/due/status, record links, and attachments remain Phase 11C scope.</li>
              </ul>
            </section>
          </aside>
        </div>
      </div>
    </section>
  );
}

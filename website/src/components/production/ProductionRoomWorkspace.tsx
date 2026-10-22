"use client";

/*
 * The production coordination room.
 *
 * One room for the shop floor, gated by explicit membership rather than by a
 * role that follows from something else: `production_room_messages` is only
 * readable to an active member, so a signed-in visitor who is not on the team
 * sees an honest explanation and no content at all, not an empty room.
 *
 * Threading is one level deep and replies inherit the parent's category,
 * because that is what the table stores. Category is chosen once on the root,
 * a pin is only legal on a top-level decision, and author edit and delete both
 * close after thirty minutes. Every one of those rules is enforced by the
 * database; the component mirrors them so a button never offers an action the
 * write will refuse.
 */

import { useCallback, useEffect, useMemo, useRef, useState, type FormEvent, type KeyboardEvent } from "react";
import type { User } from "@supabase/supabase-js";
import { LocaleLink } from "@/components/ui/LocaleLink";
import { useLocale } from "@/i18n/provider";
import { accountPathForRedirect } from "@/lib/customer-inquiry-chat";
import {
  PRODUCTION_ROOM_CATEGORY_META,
  PRODUCTION_ROOM_MAX_BODY,
  PRODUCTION_ROOM_PAGE_SIZE,
  PRODUCTION_ROOM_PATH,
  actionDueInputToIso,
  actionDueIsoToInput,
  canDeleteProductionRoomMessage,
  canEditProductionRoomMessage,
  canManageActionItem,
  canPinProductionRoomMessage,
  canReassignActionItem,
  countUnreadProductionRoomMessages,
  filterProductionRoomThreads,
  firstUnreadProductionRoomMessageId,
  formatProductionRoomDateTime,
  formatProductionRoomDueDate,
  formatProductionRoomTime,
  groupProductionRoomThreads,
  isActionClosed,
  isActionItem,
  isActionOverdue,
  isProductionRoomDraftValid,
  mergeProductionRoomMessages,
  normalizeProductionRoomDraft,
  normalizeProductionRoomMembers,
  normalizeProductionRoomMessages,
  normalizeProductionRoomRead,
  pinnedProductionRoomDecisions,
  productionRoomActionStatusLabel,
  productionRoomCategoryMeta,
  productionRoomExcerpt,
  productionRoomStats,
  validateProductionRoomMemberInput,
  type ProductionRoomThread,
} from "@/lib/production-room";
import { isSupabaseConfigured, isSupabaseUnavailableError, supabaseBrowser } from "@/lib/supabase";
import { useWebsiteSession } from "@/lib/website-session";
import type {
  ProductionRoomCategory,
  ProductionRoomMember,
  ProductionRoomMessage,
} from "@/types/production-room";

const MESSAGE_SELECT =
  "id,parent_id,author_user_id,category,body,is_pinned,pinned_by,pinned_at,created_at,edited_at,edit_count,deleted_by,deleted_at,action_owner_user_id,action_due_at,action_status";
const MEMBER_SELECT = "user_id,display_name,department,is_room_admin,active,created_at,updated_at";

type RoomUser = { id: string; email: string | null; displayName: string };

function toRoomUser(user: User | null): RoomUser | null {
  if (!user) return null;
  const metadata = user.user_metadata as Record<string, unknown> | null | undefined;
  const fullName = typeof metadata?.full_name === "string" ? metadata.full_name.trim() : "";
  const email = typeof user.email === "string" ? user.email : null;
  return {
    id: user.id,
    email,
    displayName: fullName || (email ? email.split("@")[0] ?? "Team member" : "Team member"),
  };
}

function friendlyError(reason: unknown, fallback: string): string {
  if (reason instanceof Error && reason.message) return reason.message;
  if (typeof reason === "string" && reason) return reason;
  if (reason && typeof reason === "object" && "message" in reason) {
    const message = (reason as { message?: unknown }).message;
    if (typeof message === "string" && message) return message;
  }
  return fallback;
}

export function ProductionRoomWorkspace() {
  const locale = useLocale();
  const configured = isSupabaseConfigured();
  const session = useWebsiteSession({ enabled: configured });
  const user = useMemo(() => toRoomUser(session.user), [session.user]);

  const [messages, setMessages] = useState<ProductionRoomMessage[]>([]);
  const [members, setMembers] = useState<ProductionRoomMember[]>([]);
  const [selfMember, setSelfMember] = useState<ProductionRoomMember | null>(null);
  const [lastReadAt, setLastReadAt] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [realtimeStatus, setRealtimeStatus] = useState("connecting");

  const [category, setCategory] = useState<ProductionRoomCategory>("general_production");
  const [draft, setDraft] = useState("");
  const [replyTarget, setReplyTarget] = useState<ProductionRoomMessage | null>(null);
  const [editTarget, setEditTarget] = useState<ProductionRoomMessage | null>(null);
  const [editDraft, setEditDraft] = useState("");
  const [filterCategory, setFilterCategory] = useState<ProductionRoomCategory | "all">("all");
  const [filterQuery, setFilterQuery] = useState("");
  const [unreadOnly, setUnreadOnly] = useState(false);
  const [openActionsOnly, setOpenActionsOnly] = useState(false);
  const [mineOnly, setMineOnly] = useState(false);
  const [overdueOnly, setOverdueOnly] = useState(false);
  const [showMembers, setShowMembers] = useState(false);
  const [actionOwner, setActionOwner] = useState("");
  const [actionDue, setActionDue] = useState("");
  const [memberForm, setMemberForm] = useState({
    email: "",
    displayName: "",
    department: "",
    isRoomAdmin: false,
  });

  const composerRef = useRef<HTMLTextAreaElement | null>(null);
  const scrollRef = useRef<HTMLDivElement | null>(null);
  const messagesRef = useRef<ProductionRoomMessage[]>([]);
  const lastReadRef = useRef<string | null>(null);
  const readTimerRef = useRef<number | null>(null);

  useEffect(() => {
    messagesRef.current = messages;
  }, [messages]);

  useEffect(() => {
    lastReadRef.current = lastReadAt;
  }, [lastReadAt]);

  const showLoading = loading && Boolean(user) && configured;
  const isRoomAdmin = selfMember?.is_room_admin === true;
  const isMember = Boolean(selfMember?.active);
  const isInquiryAdmin = session.user?.user_metadata?.inquiry_admin === true;
  const canManageMembers = isRoomAdmin || isInquiryAdmin;

  const normalizedDraft = useMemo(() => normalizeProductionRoomDraft(draft), [draft]);
  const canSend = Boolean(
    user && configured && isMember && !busy && normalizedDraft.length > 0 && normalizedDraft.length <= PRODUCTION_ROOM_MAX_BODY,
  );

  const threads = useMemo(() => groupProductionRoomThreads(messages), [messages]);
  const stats = useMemo(
    () => productionRoomStats(messages, { userId: user?.id ?? null }),
    [messages, user?.id],
  );
  const pinned = useMemo(() => pinnedProductionRoomDecisions(messages), [messages]);
  const unreadCount = useMemo(
    () => countUnreadProductionRoomMessages(messages, user?.id ?? null, lastReadAt),
    [lastReadAt, messages, user?.id],
  );
  const firstUnreadId = useMemo(
    () => firstUnreadProductionRoomMessageId(messages, user?.id ?? null, lastReadAt),
    [lastReadAt, messages, user?.id],
  );
  const visibleThreads = useMemo(
    () =>
      filterProductionRoomThreads(
        threads,
        { category: filterCategory, query: filterQuery, unreadOnly, openActionsOnly, mineOnly, overdueOnly },
        { userId: user?.id ?? null, lastReadAt },
      ),
    [filterCategory, filterQuery, lastReadAt, mineOnly, openActionsOnly, overdueOnly, threads, unreadOnly, user?.id],
  );

  const memberName = useCallback(
    (userId: string) =>
      members.find((member) => member.user_id === userId)?.display_name ??
      (userId === user?.id ? user.displayName : "Team member"),
    [members, user],
  );

  const refreshMembers = useCallback(async () => {
    if (!configured || !user) return;
    const { data, error: memberError } = await supabaseBrowser()
      .from("production_room_members")
      .select(MEMBER_SELECT)
      .order("display_name", { ascending: true });
    if (memberError) throw memberError;
    const rows = normalizeProductionRoomMembers(data);
    setMembers(rows);
    setSelfMember(rows.find((member) => member.user_id === user.id) ?? null);
  }, [configured, user]);

  const refreshMessages = useCallback(async () => {
    if (!configured || !user) return;
    const { data, error: messageError } = await supabaseBrowser()
      .from("production_room_messages")
      .select(MESSAGE_SELECT)
      .order("created_at", { ascending: true })
      .order("id", { ascending: true })
      .limit(PRODUCTION_ROOM_PAGE_SIZE);
    if (messageError) throw messageError;
    const rows = normalizeProductionRoomMessages(data);
    messagesRef.current = rows;
    setMessages(rows);
  }, [configured, user]);

  const refreshReadState = useCallback(async () => {
    if (!configured || !user) return;
    const { data, error: readError } = await supabaseBrowser()
      .from("production_room_reads")
      .select("user_id,last_read_at,last_read_message_id,updated_at")
      .eq("user_id", user.id)
      .maybeSingle();
    if (readError) throw readError;
    const read = normalizeProductionRoomRead(data);
    setLastReadAt(read?.last_read_at ?? null);
  }, [configured, user]);

  const markRead = useCallback(
    async (through?: string | null) => {
      if (!configured || !user || !isMember) return;
      const latest = through ?? messagesRef.current[messagesRef.current.length - 1]?.created_at ?? null;
      if (!latest) return;
      const { data, error: readError } = await supabaseBrowser().rpc("mark_production_room_read", {
        p_through: latest,
      });
      if (readError) return;
      const read = normalizeProductionRoomRead(data);
      if (read) setLastReadAt(read.last_read_at);
    },
    [configured, isMember, user],
  );

  // Initial load. A non-member is not an error: RLS returns an empty message
  // list, and the membership row is what tells the two apart.
  useEffect(() => {
    // Loading is only meaningful once there is a signed-in reader; asking for
    // state here would render twice on every signed-out visit for no gain.
    if (!configured || !user) return;
    let active = true;
    void (async () => {
      try {
        await refreshMembers();
        if (!active) return;
        await refreshMessages();
        if (!active) return;
        await refreshReadState();
        if (!active) return;
        setError("");
      } catch (reason) {
        if (!active) return;
        setError(
          isSupabaseUnavailableError(reason as { message?: string } | null)
            ? "The production room service is temporarily unreachable. Try again shortly."
            : friendlyError(reason, "Unable to load the production room."),
        );
      } finally {
        if (active) setLoading(false);
      }
    })();
    return () => {
      active = false;
    };
  }, [configured, refreshMembers, refreshMessages, refreshReadState, user]);

  // Realtime plus a slow reconcile. The socket makes the room feel immediate;
  // the tick covers a dropped socket without the reader having to refresh.
  useEffect(() => {
    if (!configured || !user || !isMember) return;
    let active = true;
    let removeChannel: (() => void) | null = null;
    void (async () => {
      try {
        const token = session.accessToken;
        if (token) {
          await Promise.resolve(supabaseBrowser().realtime.setAuth(token)).catch(() => undefined);
        }
        if (!active) return;
        const channel = supabaseBrowser()
          .channel(`production-room-${user.id}-${Date.now().toString(36)}`)
          .on(
            "postgres_changes",
            { event: "*", schema: "public", table: "production_room_messages" },
            (payload: { new?: unknown }) => {
              if (!active) return;
              const row = normalizeProductionRoomMessages([payload.new])[0];
              if (!row) {
                void refreshMessages().catch(() => undefined);
                return;
              }
              const merged = mergeProductionRoomMessages(messagesRef.current, [row]);
              messagesRef.current = merged;
              setMessages(merged);
            },
          )
          .subscribe((status: string) => {
            if (!active) return;
            setRealtimeStatus(typeof status === "string" && status ? status.toLowerCase() : "connecting");
          });
        removeChannel = () => {
          try {
            void supabaseBrowser().removeChannel(channel);
          } catch {
            // Channel teardown is best effort.
          }
        };
      } catch {
        if (!active) return;
        setRealtimeStatus("error");
      }
    })();
    const timer = window.setInterval(() => {
      if (!active) return;
      void refreshMessages().catch(() => undefined);
    }, 12000);
    return () => {
      active = false;
      window.clearInterval(timer);
      removeChannel?.();
    };
  }, [configured, isMember, refreshMessages, session.accessToken, user]);

  // Reading the room marks it read once the reader has actually seen the
  // newest message. The timer coalesces bursts into one write.
  useEffect(() => {
    if (!isMember || !messages.length) return;
    if (readTimerRef.current !== null) window.clearTimeout(readTimerRef.current);
    readTimerRef.current = window.setTimeout(() => {
      void markRead();
    }, 1200);
    return () => {
      if (readTimerRef.current !== null) window.clearTimeout(readTimerRef.current);
    };
  }, [isMember, markRead, messages.length]);

  const send = async (event?: FormEvent) => {
    event?.preventDefault();
    if (!canSend) return;
    setBusy(true);
    setError("");
    const postingAction = !replyTarget && category === "action_item";
    try {
      const { data, error: postError } = postingAction
        ? await supabaseBrowser().rpc("post_production_room_action_item", {
            p_body: normalizedDraft,
            p_owner_user_id: actionOwner || null,
            p_due_at: actionDueInputToIso(actionDue),
          })
        : await supabaseBrowser().rpc("post_production_room_message", {
            p_body: normalizedDraft,
            p_category: replyTarget ? replyTarget.category : category,
            p_parent_id: replyTarget?.id ?? null,
          });
      if (postError) throw postError;
      const row = normalizeProductionRoomMessages([data])[0];
      if (!row) throw new Error("The message was saved but could not be displayed. Reload the room.");
      const merged = mergeProductionRoomMessages(messagesRef.current, [row]);
      messagesRef.current = merged;
      setMessages(merged);
      setDraft("");
      setReplyTarget(null);
      setActionOwner("");
      setActionDue("");
      requestAnimationFrame(() => composerRef.current?.focus());
      void markRead(row.created_at);
    } catch (reason) {
      setError(
        isSupabaseUnavailableError(reason as { message?: string } | null)
          ? "The room is unreachable right now. Your message was not sent."
          : friendlyError(reason, "Unable to post your message."),
      );
    } finally {
      setBusy(false);
    }
  };

  const saveEdit = async () => {
    if (!editTarget || !isProductionRoomDraftValid(editDraft)) return;
    setBusy(true);
    setError("");
    try {
      const { data, error: editError } = await supabaseBrowser().rpc("edit_production_room_message", {
        p_message_id: editTarget.id,
        p_body: normalizeProductionRoomDraft(editDraft),
      });
      if (editError) throw editError;
      const row = normalizeProductionRoomMessages([data])[0];
      if (row) {
        const merged = mergeProductionRoomMessages(messagesRef.current, [row]);
        messagesRef.current = merged;
        setMessages(merged);
      }
      setEditTarget(null);
      setEditDraft("");
    } catch (reason) {
      setError(friendlyError(reason, "Unable to save that edit."));
    } finally {
      setBusy(false);
    }
  };

  const removeMessage = async (message: ProductionRoomMessage) => {
    if (!window.confirm("Delete this message for everyone in the production room?")) return;
    setBusy(true);
    setError("");
    try {
      const { data, error: deleteError } = await supabaseBrowser().rpc("delete_production_room_message", {
        p_message_id: message.id,
      });
      if (deleteError) throw deleteError;
      const row = normalizeProductionRoomMessages([data])[0];
      if (row) {
        const merged = mergeProductionRoomMessages(messagesRef.current, [row]);
        messagesRef.current = merged;
        setMessages(merged);
      }
      setReplyTarget((current) => (current?.id === message.id ? null : current));
      setEditTarget((current) => (current?.id === message.id ? null : current));
    } catch (reason) {
      setError(friendlyError(reason, "Unable to delete that message."));
    } finally {
      setBusy(false);
    }
  };

  const togglePin = async (message: ProductionRoomMessage) => {
    setBusy(true);
    setError("");
    try {
      const { data, error: pinError } = await supabaseBrowser().rpc("set_production_room_message_pinned", {
        p_message_id: message.id,
        p_pinned: !message.is_pinned,
      });
      if (pinError) throw pinError;
      const row = normalizeProductionRoomMessages([data])[0];
      if (row) {
        const merged = mergeProductionRoomMessages(messagesRef.current, [row]);
        messagesRef.current = merged;
        setMessages(merged);
      }
    } catch (reason) {
      setError(friendlyError(reason, "Unable to change the pin."));
    } finally {
      setBusy(false);
    }
  };

  const updateAction = async (
    message: ProductionRoomMessage,
    changes: { ownerUserId?: string | null; dueAt?: string | null; status?: string | null },
  ) => {
    setBusy(true);
    setError("");
    try {
      const { data, error: actionError } = await supabaseBrowser().rpc("set_production_room_action", {
        p_message_id: message.id,
        p_owner_user_id: changes.ownerUserId === undefined ? message.action_owner_user_id : changes.ownerUserId,
        p_due_at: changes.dueAt === undefined ? message.action_due_at : changes.dueAt,
        p_status: changes.status === undefined ? message.action_status : changes.status,
      });
      if (actionError) throw actionError;
      const row = normalizeProductionRoomMessages([data])[0];
      if (row) {
        const merged = mergeProductionRoomMessages(messagesRef.current, [row]);
        messagesRef.current = merged;
        setMessages(merged);
      }
    } catch (reason) {
      setError(friendlyError(reason, "Unable to update that action item."));
    } finally {
      setBusy(false);
    }
  };

  const registerMember = async (event: FormEvent) => {
    event.preventDefault();
    const problem = validateProductionRoomMemberInput(memberForm);
    if (problem) {
      setError(problem);
      return;
    }
    setBusy(true);
    setError("");
    try {
      const { error: registerError } = await supabaseBrowser().rpc("register_production_room_member", {
        p_email: memberForm.email.trim().toLowerCase(),
        p_display_name: memberForm.displayName.trim(),
        p_department: memberForm.department.trim(),
        p_is_room_admin: memberForm.isRoomAdmin,
        p_active: true,
      });
      if (registerError) throw registerError;
      setMemberForm({ email: "", displayName: "", department: "", isRoomAdmin: false });
      await refreshMembers();
    } catch (reason) {
      setError(friendlyError(reason, "Unable to register that member."));
    } finally {
      setBusy(false);
    }
  };

  const setMemberActive = async (member: ProductionRoomMember, active: boolean) => {
    setBusy(true);
    setError("");
    try {
      const { error: toggleError } = await supabaseBrowser().rpc("set_production_room_member_active", {
        p_user_id: member.user_id,
        p_active: active,
      });
      if (toggleError) throw toggleError;
      await refreshMembers();
    } catch (reason) {
      setError(friendlyError(reason, "Unable to change that membership."));
    } finally {
      setBusy(false);
    }
  };

  const startReply = (message: ProductionRoomMessage) => {
    const root = message.parent_id
      ? messagesRef.current.find((row) => row.id === message.parent_id) ?? message
      : message;
    setReplyTarget(root);
    setEditTarget(null);
    requestAnimationFrame(() => composerRef.current?.focus());
  };

  const startEdit = (message: ProductionRoomMessage) => {
    setEditTarget(message);
    setEditDraft(message.body);
    setReplyTarget(null);
  };

  const onComposerKeyDown = (event: KeyboardEvent<HTMLTextAreaElement>) => {
    if (event.key === "Escape" && replyTarget) {
      event.preventDefault();
      setReplyTarget(null);
      return;
    }
    if (event.key === "Enter" && !event.shiftKey) {
      event.preventDefault();
      void send();
    }
  };

  const jumpToUnread = () => {
    if (!firstUnreadId) return;
    const element = scrollRef.current?.querySelector<HTMLElement>(`[data-message-id="${firstUnreadId}"]`);
    element?.scrollIntoView({ block: "center" });
  };

  if (!configured) {
    return (
      <main className="bg-ink-950 px-4 py-16 text-text-primary">
        <section className="shell max-w-xl rounded-3xl border border-line-strong bg-ink-900 p-8 text-center">
          <h1 className="text-2xl font-bold">The production room is not configured</h1>
          <p className="mt-3 text-sm text-text-muted">
            This deployment has no Supabase project configured, so the room cannot load. Nothing is being simulated.
          </p>
        </section>
      </main>
    );
  }

  if (session.checking) {
    return (
      <main role="status" className="grid min-h-[60vh] place-items-center bg-ink-950 text-xs text-text-muted">
        Checking your saved sign-in…
      </main>
    );
  }

  if (session.unavailable && !user) {
    return (
      <main className="bg-ink-950 px-4 py-16 text-text-primary">
        <section className="shell max-w-xl rounded-3xl border border-state-warning/40 bg-ink-900 p-8 text-center">
          <h1 className="text-2xl font-bold">Your saved sign-in could not be confirmed yet</h1>
          <p className="mt-3 text-sm text-text-muted">
            {session.error ?? "The authentication service is temporarily unreachable. Your saved sign-in has not been discarded."}
          </p>
          <button
            type="button"
            onClick={() => void session.refreshSession()}
            className="mt-6 rounded-xl bg-brand-400 px-5 py-3 text-sm font-bold text-accent-ink"
          >
            Retry saved sign-in
          </button>
        </section>
      </main>
    );
  }

  if (!user) {
    return (
      <main className="bg-ink-950 px-4 py-16 text-text-primary">
        <section className="shell max-w-xl rounded-3xl border border-line-strong bg-ink-900 p-8 text-center">
          <p className="eyebrow text-brand-300">Internal room</p>
          <h1 className="mt-3 text-2xl font-bold">Sign in to open the production room</h1>
          <p className="mt-3 text-sm leading-relaxed text-text-muted">
            The production coordination room is for approved ProGlass team members. Membership is assigned by an
            administrator; it is not granted by signing in.
          </p>
          <LocaleLink
            href={accountPathForRedirect(PRODUCTION_ROOM_PATH)}
            className="mt-6 inline-flex rounded-xl bg-brand-400 px-5 py-3 text-sm font-bold text-accent-ink"
          >
            Sign in
          </LocaleLink>
        </section>
      </main>
    );
  }

  if (!showLoading && !isMember) {
    return (
      <main className="bg-ink-950 px-4 py-16 text-text-primary">
        <section className="shell max-w-xl rounded-3xl border border-line-strong bg-ink-900 p-8 text-center">
          <p className="eyebrow text-brand-300">Internal room</p>
          <h1 className="mt-3 text-2xl font-bold">Your account is not on the production team yet</h1>
          <p className="mt-3 text-sm leading-relaxed text-text-muted">
            You are signed in as {user.email ?? user.displayName}. The room shows nothing until an administrator
            registers this account, which is deliberate: production posts name customers, machines and dates.
          </p>
          <LocaleLink href="/chat" className="mt-6 inline-flex text-xs font-bold text-brand-300 underline underline-offset-4">
            Open the community room instead
          </LocaleLink>
        </section>
      </main>
    );
  }

  return (
    <main className="bg-ink-950 px-3 py-6 text-text-primary sm:px-5">
      <div className="shell space-y-4">
        <header className="flex flex-wrap items-end justify-between gap-3">
          <div>
            <p className="eyebrow text-brand-300">Production coordination</p>
            <h1 className="mt-2 text-2xl font-bold sm:text-3xl">Shop floor room</h1>
            <p className="mt-2 max-w-2xl text-sm leading-relaxed text-text-muted">
              Blockers, shortages, quality, schedule changes, dispatch dependencies, decisions and action items, in
              one room that keeps its decisions pinned. Membership is explicit; nothing here follows from a customer
              or viewer role.
            </p>
          </div>
          <div className="flex flex-wrap items-center gap-2 text-xs">
            <span
              className={`rounded-full border px-3 py-1 font-bold ${
                realtimeStatus === "subscribed"
                  ? "border-emerald-300/40 text-emerald-300"
                  : "border-state-warning/40 text-state-warning"
              }`}
            >
              {realtimeStatus === "subscribed" ? "Live" : "Auto-refresh on"}
            </span>
            <span className="rounded-full border border-line-strong px-3 py-1 text-text-muted">
              {stats.threads} threads · {stats.replies} replies
            </span>
            {unreadCount > 0 ? (
              <button
                type="button"
                onClick={jumpToUnread}
                className="rounded-full border border-brand-300/50 px-3 py-1 font-bold text-brand-200"
              >
                {unreadCount} unread · jump
              </button>
            ) : null}
            {canManageMembers ? (
              <button
                type="button"
                onClick={() => setShowMembers((value) => !value)}
                aria-expanded={showMembers}
                className="rounded-full border border-line-strong px-3 py-1 font-bold text-text-secondary hover:text-text-primary"
              >
                Members
              </button>
            ) : null}
          </div>
        </header>

        {error ? (
          <div role="alert" className="rounded-2xl border border-state-danger/40 bg-state-danger/10 px-4 py-2 text-xs font-semibold text-state-danger">
            {error}
          </div>
        ) : null}

        {showMembers && canManageMembers ? (
          <section aria-label="Production room members" className="rounded-3xl border border-line-strong bg-ink-900 p-4">
            <h2 className="text-sm font-bold">Members</h2>
            <p className="mt-1 text-xs text-text-muted">
              Registration needs the exact email of an existing ProGlass sign-in. At least one active room admin must
              always remain.
            </p>
            <ul className="mt-3 divide-y divide-line">
              {members.map((member) => (
                <li key={member.user_id} className="flex flex-wrap items-center justify-between gap-2 py-2">
                  <div className="min-w-0">
                    <p className="truncate text-sm font-semibold">
                      {member.display_name}
                      {member.is_room_admin ? (
                        <span className="ml-2 rounded-full border border-brand-300/40 px-2 py-0.5 text-[10px] font-bold text-brand-200">
                          Room admin
                        </span>
                      ) : null}
                      {!member.active ? (
                        <span className="ml-2 rounded-full border border-line-strong px-2 py-0.5 text-[10px] font-bold text-text-faint">
                          Inactive
                        </span>
                      ) : null}
                    </p>
                    <p className="truncate text-xs text-text-faint">{member.department}</p>
                  </div>
                  <button
                    type="button"
                    disabled={busy}
                    onClick={() => void setMemberActive(member, !member.active)}
                    className="rounded-lg border border-line-strong px-3 py-1 text-[11px] font-bold text-text-secondary hover:text-text-primary disabled:opacity-50"
                  >
                    {member.active ? "Deactivate" : "Reactivate"}
                  </button>
                </li>
              ))}
              {!members.length ? <li className="py-2 text-xs text-text-muted">No members registered yet.</li> : null}
            </ul>
            <form onSubmit={(event) => void registerMember(event)} className="mt-4 grid gap-2 border-t border-line pt-4 sm:grid-cols-4">
              <label className="text-[11px] font-bold uppercase tracking-wide text-text-faint">
                Email
                <input
                  type="email"
                  required
                  value={memberForm.email}
                  onChange={(event) => setMemberForm((form) => ({ ...form, email: event.target.value }))}
                  className="mt-1 h-9 w-full rounded-lg border border-line-strong bg-ink-950 px-2 text-xs font-normal normal-case tracking-normal text-text-primary"
                />
              </label>
              <label className="text-[11px] font-bold uppercase tracking-wide text-text-faint">
                Display name
                <input
                  type="text"
                  required
                  value={memberForm.displayName}
                  onChange={(event) => setMemberForm((form) => ({ ...form, displayName: event.target.value }))}
                  className="mt-1 h-9 w-full rounded-lg border border-line-strong bg-ink-950 px-2 text-xs font-normal normal-case tracking-normal text-text-primary"
                />
              </label>
              <label className="text-[11px] font-bold uppercase tracking-wide text-text-faint">
                Department
                <input
                  type="text"
                  required
                  value={memberForm.department}
                  onChange={(event) => setMemberForm((form) => ({ ...form, department: event.target.value }))}
                  className="mt-1 h-9 w-full rounded-lg border border-line-strong bg-ink-950 px-2 text-xs font-normal normal-case tracking-normal text-text-primary"
                />
              </label>
              <div className="flex items-end gap-2">
                <label className="flex items-center gap-2 text-[11px] font-bold text-text-secondary">
                  <input
                    type="checkbox"
                    checked={memberForm.isRoomAdmin}
                    onChange={(event) => setMemberForm((form) => ({ ...form, isRoomAdmin: event.target.checked }))}
                  />
                  Admin
                </label>
                <button
                  type="submit"
                  disabled={busy}
                  className="h-9 rounded-lg bg-brand-400 px-3 text-xs font-bold text-accent-ink disabled:opacity-50"
                >
                  Register
                </button>
              </div>
            </form>
          </section>
        ) : null}

        {pinned.length ? (
          <section aria-label="Pinned decisions" className="rounded-3xl border border-emerald-300/30 bg-emerald-300/5 p-4">
            <h2 className="text-xs font-bold uppercase tracking-wide text-emerald-300">
              Pinned decisions {stats.pinnedDecisions ? `(${stats.pinnedDecisions})` : ""}
            </h2>
            <ul className="mt-2 space-y-2">
              {pinned.map((decision) => (
                <li key={decision.id} className="text-sm leading-relaxed text-text-secondary">
                  <span className="font-semibold text-text-primary">{productionRoomExcerpt(decision.body, 220)}</span>{" "}
                  <span className="whitespace-nowrap text-[11px] text-text-faint">
                    {memberName(decision.author_user_id)} · {formatProductionRoomDateTime(decision.pinned_at, locale)}
                  </span>
                  {isRoomAdmin ? (
                    <button
                      type="button"
                      disabled={busy}
                      onClick={() => void togglePin(decision)}
                      className="ml-2 text-[11px] font-bold text-emerald-300 underline underline-offset-2 disabled:opacity-50"
                    >
                      Unpin
                    </button>
                  ) : null}
                </li>
              ))}
            </ul>
          </section>
        ) : null}

        <div className="flex flex-wrap items-center gap-2">
          <button
            type="button"
            onClick={() => setFilterCategory("all")}
            aria-pressed={filterCategory === "all"}
            className={`rounded-full border px-3 py-1 text-[11px] font-bold ${
              filterCategory === "all" ? "border-brand-300 text-brand-200" : "border-line-strong text-text-muted"
            }`}
          >
            All ({stats.threads})
          </button>
          {PRODUCTION_ROOM_CATEGORY_META.map((meta) => (
            <button
              key={meta.value}
              type="button"
              onClick={() => setFilterCategory(meta.value)}
              aria-pressed={filterCategory === meta.value}
              title={meta.description}
              className={`rounded-full border px-3 py-1 text-[11px] font-bold ${
                filterCategory === meta.value ? "border-brand-300 text-brand-200" : meta.tone
              }`}
            >
              {meta.short} ({stats.byCategory[meta.value]})
            </button>
          ))}
          <button
            type="button"
            onClick={() => {
              setOpenActionsOnly((value) => !value);
              if (openActionsOnly) setOverdueOnly(false);
            }}
            aria-pressed={openActionsOnly}
            className={`rounded-full border px-3 py-1 text-[11px] font-bold ${
              openActionsOnly ? "border-brand-300 text-brand-200" : "border-line-strong text-text-muted"
            }`}
          >
            Open actions ({stats.openActions})
          </button>
          <button
            type="button"
            onClick={() => {
              setMineOnly((value) => !value);
              if (mineOnly) setOverdueOnly(false);
            }}
            aria-pressed={mineOnly}
            className={`rounded-full border px-3 py-1 text-[11px] font-bold ${
              mineOnly ? "border-brand-300 text-brand-200" : "border-line-strong text-text-muted"
            }`}
          >
            Mine ({stats.myOpenActions})
          </button>
          <button
            type="button"
            onClick={() => {
              setOverdueOnly((value) => !value);
              if (overdueOnly) {
                setMineOnly(false);
                setOpenActionsOnly(false);
              }
            }}
            aria-pressed={overdueOnly}
            className={`rounded-full border px-3 py-1 text-[11px] font-bold ${
              overdueOnly
                ? "border-state-danger text-state-danger"
                : stats.overdueActions
                  ? "border-state-danger/50 text-state-danger"
                  : "border-line-strong text-text-muted"
            }`}
          >
            Overdue ({stats.overdueActions})
          </button>
          <input
            type="search"
            value={filterQuery}
            onChange={(event) => setFilterQuery(event.target.value)}
            aria-label="Search the production room"
            placeholder="Search this room…"
            className="h-8 min-w-[180px] flex-1 rounded-full border border-line-strong bg-ink-900 px-3 text-xs text-text-primary placeholder:text-text-faint"
          />
          <label className="flex items-center gap-2 text-[11px] font-bold text-text-muted">
            <input type="checkbox" checked={unreadOnly} onChange={(event) => setUnreadOnly(event.target.checked)} />
            Unread only
          </label>
        </div>

        <div className="grid gap-4 lg:grid-cols-[minmax(0,1fr)_20rem]">
          <section className="flex min-h-[520px] flex-col rounded-3xl border border-line-strong bg-ink-900">
            <h2 className="sr-only">Room thread</h2>
            <div
              ref={scrollRef}
              role="log"
              aria-live="polite"
              aria-label="Production room messages"
              className="min-h-0 flex-1 space-y-3 overflow-y-auto px-3 py-3"
            >
              {showLoading ? (
                <p role="status" className="grid h-full place-items-center text-sm text-text-muted">
                  Loading the production room…
                </p>
              ) : visibleThreads.length === 0 ? (
                <div className="grid h-full place-items-center px-4 text-center">
                  <div>
                    <h3 className="text-lg font-bold">
                      {threads.length ? "Nothing matches this filter" : "No posts yet"}
                    </h3>
                    <p className="mx-auto mt-2 max-w-sm text-sm text-text-muted">
                      {threads.length
                        ? "Clear the category, the search, the unread filter or the action filters to see the rest of the room."
                        : "Post the first item: a blocker, a shortage, a decision, or an action with an owner."}
                    </p>
                  </div>
                </div>
              ) : (
                visibleThreads.map((thread) => (
                  <RoomThread
                    key={thread.root.id}
                    thread={thread}
                    locale={locale}
                    userId={user.id}
                    isRoomAdmin={isRoomAdmin}
                    busy={busy}
                    memberName={memberName}
                    firstUnreadId={firstUnreadId}
                    editTargetId={editTarget?.id ?? null}
                    editDraft={editDraft}
                    onEditDraft={setEditDraft}
                    onStartEdit={startEdit}
                    onCancelEdit={() => setEditTarget(null)}
                    onSaveEdit={() => void saveEdit()}
                    onReply={startReply}
                    onDelete={(message) => void removeMessage(message)}
                    onPin={(message) => void togglePin(message)}
                    canManageAction={(message) => canManageActionItem(message, user.id, isRoomAdmin)}
                    canReassignAction={(message) => canReassignActionItem(message, user.id, isRoomAdmin)}
                    onChangeAction={(message, changes) => void updateAction(message, changes)}
                    members={members.filter((member) => member.active)}
                  />
                ))
              )}
            </div>

            <form onSubmit={(event) => void send(event)} className="border-t border-line px-3 py-2">
              {replyTarget ? (
                <div className="mb-2 flex items-center gap-2 rounded-xl border border-line-strong bg-ink-950 px-2 py-1">
                  <span className="min-w-0 flex-1 truncate text-[11px] text-text-muted">
                    Replying in {productionRoomCategoryMeta(replyTarget.category).label}:{" "}
                    {productionRoomExcerpt(replyTarget.body, 90)}
                  </span>
                  <button
                    type="button"
                    onClick={() => setReplyTarget(null)}
                    aria-label="Cancel reply"
                    className="shrink-0 rounded-full border border-line-strong px-2 py-0.5 text-[10px] font-bold text-text-muted"
                  >
                    ✕
                  </button>
                </div>
              ) : null}
              <div className="flex flex-wrap items-end gap-2">
                <label className="sr-only" htmlFor="production-room-category">
                  Category
                </label>
                <select
                  id="production-room-category"
                  value={replyTarget ? replyTarget.category : category}
                  disabled={Boolean(replyTarget)}
                  onChange={(event) => setCategory(event.target.value as ProductionRoomCategory)}
                  className="h-9 rounded-xl border border-line-strong bg-ink-950 px-2 text-xs text-text-primary disabled:opacity-60"
                >
                  {PRODUCTION_ROOM_CATEGORY_META.map((meta) => (
                    <option key={meta.value} value={meta.value}>
                      {meta.label}
                    </option>
                  ))}
                </select>
                {!replyTarget && category === "action_item" ? (
                  <>
                    <label className="flex items-center gap-1 text-[10px] font-bold uppercase tracking-wide text-text-faint">
                      Owner
                      <select
                        aria-label="Action owner"
                        value={actionOwner}
                        onChange={(event) => setActionOwner(event.target.value)}
                        className="h-9 rounded-xl border border-line-strong bg-ink-950 px-2 text-xs font-normal normal-case tracking-normal text-text-primary"
                      >
                        <option value="">Unassigned</option>
                        {members
                          .filter((member) => member.active)
                          .map((member) => (
                            <option key={member.user_id} value={member.user_id}>
                              {member.display_name}
                            </option>
                          ))}
                      </select>
                    </label>
                    <label className="flex items-center gap-1 text-[10px] font-bold uppercase tracking-wide text-text-faint">
                      Due
                      <input
                        type="datetime-local"
                        aria-label="Action due date"
                        value={actionDue}
                        onChange={(event) => setActionDue(event.target.value)}
                        className="h-9 rounded-xl border border-line-strong bg-ink-950 px-2 text-xs font-normal normal-case tracking-normal text-text-primary"
                      />
                    </label>
                  </>
                ) : null}
                <label className="sr-only" htmlFor="production-room-composer">
                  Message the production room
                </label>
                <textarea
                  ref={composerRef}
                  id="production-room-composer"
                  value={draft}
                  rows={2}
                  onChange={(event) => setDraft(event.target.value)}
                  onKeyDown={onComposerKeyDown}
                  placeholder={replyTarget ? "Write your reply…" : "Post to the production room…"}
                  aria-describedby="production-room-composer-hint"
                  className="min-h-9 flex-1 resize-y rounded-xl border border-line-strong bg-ink-950 px-3 py-1.5 text-sm text-text-primary placeholder:text-text-faint focus:border-brand-400 focus:outline-none focus:ring-2 focus:ring-brand-400/40"
                />
                <button
                  type="submit"
                  disabled={!canSend}
                  className="h-9 rounded-xl bg-brand-400 px-4 text-xs font-bold text-accent-ink disabled:cursor-not-allowed disabled:opacity-50"
                >
                  {busy ? "Saving…" : replyTarget ? "Send reply" : "Post"}
                </button>
              </div>
              <p id="production-room-composer-hint" className="sr-only">
                Enter posts. Shift plus Enter adds a new line. Escape cancels a reply.
              </p>
              <div className="mt-1 flex items-center justify-between text-[10px] text-text-faint">
                <span>{productionRoomCategoryMeta(replyTarget ? replyTarget.category : category).description}</span>
                <span className={normalizedDraft.length > PRODUCTION_ROOM_MAX_BODY ? "font-bold text-state-danger" : undefined}>
                  {normalizedDraft.length}/{PRODUCTION_ROOM_MAX_BODY}
                </span>
              </div>
            </form>
          </section>

          <aside className="space-y-3">
            <section className="rounded-3xl border border-line-strong bg-ink-900 p-4">
              <h2 className="text-xs font-bold uppercase tracking-wide text-text-faint">Room shape</h2>
              <dl className="mt-2 space-y-1 text-sm">
                <div className="flex justify-between gap-2">
                  <dt className="text-text-muted">Top-level items</dt>
                  <dd className="font-bold">{stats.threads}</dd>
                </div>
                <div className="flex justify-between gap-2">
                  <dt className="text-text-muted">Replies</dt>
                  <dd className="font-bold">{stats.replies}</dd>
                </div>
                <div className="flex justify-between gap-2">
                  <dt className="text-text-muted">Pinned decisions</dt>
                  <dd className="font-bold">{stats.pinnedDecisions}</dd>
                </div>
                <div className="flex justify-between gap-2">
                  <dt className="text-text-muted">Unread</dt>
                  <dd className="font-bold">{unreadCount}</dd>
                </div>
                <div className="flex justify-between gap-2">
                  <dt className="text-text-muted">Open actions</dt>
                  <dd className="font-bold">{stats.openActions}</dd>
                </div>
                <div className="flex justify-between gap-2">
                  <dt className="text-text-muted">Overdue</dt>
                  <dd className={`font-bold ${stats.overdueActions ? "text-state-danger" : ""}`}>
                    {stats.overdueActions}
                  </dd>
                </div>
                <div className="flex justify-between gap-2">
                  <dt className="text-text-muted">Mine to close</dt>
                  <dd className="font-bold">{stats.myOpenActions}</dd>
                </div>
              </dl>
            </section>
            <section className="rounded-3xl border border-line-strong bg-ink-900 p-4">
              <h2 className="text-xs font-bold uppercase tracking-wide text-text-faint">Who is in</h2>
              <ul className="mt-2 space-y-1 text-sm">
                {members
                  .filter((member) => member.active)
                  .map((member) => (
                    <li key={member.user_id} className="flex items-center justify-between gap-2">
                      <span className="truncate">{member.display_name}</span>
                      <span className="shrink-0 text-[11px] text-text-faint">
                        {member.is_room_admin ? "Admin" : member.department}
                      </span>
                    </li>
                  ))}
              </ul>
              {!members.filter((member) => member.active).length ? (
                <p className="mt-2 text-xs text-text-muted">No active members.</p>
              ) : null}
            </section>
            <p className="px-1 text-[11px] leading-relaxed text-text-faint">
              Author edits and deletions close after 30 minutes. Only a top-level decision can be pinned. Every action
              writes an immutable event.
            </p>
          </aside>
        </div>
      </div>
    </main>
  );
}

function RoomThread({
  thread,
  locale,
  userId,
  isRoomAdmin,
  busy,
  memberName,
  firstUnreadId,
  editTargetId,
  editDraft,
  onEditDraft,
  onStartEdit,
  onCancelEdit,
  onSaveEdit,
  onReply,
  onDelete,
  onPin,
  canManageAction,
  canReassignAction,
  onChangeAction,
  members,
}: {
  thread: ProductionRoomThread;
  locale: string;
  userId: string;
  isRoomAdmin: boolean;
  busy: boolean;
  memberName: (userId: string) => string;
  firstUnreadId: string | null;
  editTargetId: string | null;
  editDraft: string;
  onEditDraft: (value: string) => void;
  onStartEdit: (message: ProductionRoomMessage) => void;
  onCancelEdit: () => void;
  onSaveEdit: () => void;
  onReply: (message: ProductionRoomMessage) => void;
  onDelete: (message: ProductionRoomMessage) => void;
  onPin: (message: ProductionRoomMessage) => void;
  canManageAction: (message: ProductionRoomMessage) => boolean;
  canReassignAction: (message: ProductionRoomMessage) => boolean;
  onChangeAction: (
    message: ProductionRoomMessage,
    changes: { ownerUserId?: string | null; dueAt?: string | null; status?: string | null },
  ) => void;
  members: ProductionRoomMember[];
}) {
  const meta = productionRoomCategoryMeta(thread.root.category);
  const action = isActionItem(thread.root) ? thread.root : null;
  const actionStatus = action ? productionRoomActionStatusLabel(action) : "";
  const actionClosed = action ? isActionClosed(action) : false;
  const actionOverdue = action ? isActionOverdue(action) : false;
  return (
    <article
      data-message-id={thread.root.id}
      className={`rounded-2xl border bg-ink-950/60 p-3 ${
        firstUnreadId && (thread.root.id === firstUnreadId ||
          thread.replies.some((reply) => reply.id === firstUnreadId))
          ? "border-brand-300/60"
          : "border-line"
      }`}
    >
      <header className="flex flex-wrap items-center gap-2">
        <span className={`rounded-full border px-2 py-0.5 text-[10px] font-bold uppercase tracking-wide ${meta.tone}`}>
          {meta.label}
        </span>
        {thread.root.is_pinned ? (
          <span className="rounded-full border border-emerald-300/50 px-2 py-0.5 text-[10px] font-bold text-emerald-300">
            Pinned
          </span>
        ) : null}
        {action ? (
          <>
            <span
              className={`rounded-full border px-2 py-0.5 text-[10px] font-bold ${
                actionClosed
                  ? "border-line-strong text-text-faint"
                  : actionOverdue
                    ? "border-state-danger/60 text-state-danger"
                    : "border-brand-300/50 text-brand-200"
              }`}
            >
              {actionStatus}
            </span>
            <span className="text-[11px] text-text-muted">
              {action.action_owner_user_id
                ? `Owner: ${memberName(action.action_owner_user_id)}`
                : "Unassigned"}
              {action.action_due_at ? ` · due ${formatProductionRoomDueDate(action.action_due_at, locale)}` : ""}
            </span>
          </>
        ) : null}
        <span className="text-xs font-semibold text-text-secondary">{memberName(thread.root.author_user_id)}</span>
        <time dateTime={thread.root.created_at} className="text-[11px] text-text-faint">
          {formatProductionRoomDateTime(thread.root.created_at, locale)}
        </time>
        {thread.root.edited_at ? <span className="text-[11px] text-text-faint">edited</span> : null}
      </header>

      <MessageBody
        message={thread.root}
        editing={editTargetId === thread.root.id}
        editDraft={editDraft}
        onEditDraft={onEditDraft}
        onCancelEdit={onCancelEdit}
        onSaveEdit={onSaveEdit}
      />

      {thread.replies.length ? (
        <ul className="mt-1 space-y-1 border-l border-line pl-3">
          {thread.replies.map((reply) => (
            <li key={reply.id} data-message-id={reply.id} className="pt-1">
              <div className="flex flex-wrap items-center gap-2">
                <span className="text-[11px] font-semibold text-text-secondary">{memberName(reply.author_user_id)}</span>
                <time dateTime={reply.created_at} className="text-[10px] text-text-faint">
                  {formatProductionRoomTime(reply.created_at, locale)}
                </time>
                {reply.edited_at ? <span className="text-[10px] text-text-faint">edited</span> : null}
                {firstUnreadId === reply.id ? (
                  <span className="rounded-full border border-brand-300/60 px-2 py-0.5 text-[9px] font-bold text-brand-200">
                    New
                  </span>
                ) : null}
              </div>
              <MessageBody
                message={reply}
                editing={editTargetId === reply.id}
                editDraft={editDraft}
                onEditDraft={onEditDraft}
                onCancelEdit={onCancelEdit}
                onSaveEdit={onSaveEdit}
              />
            </li>
          ))}
        </ul>
      ) : null}

      <footer className="mt-1 flex flex-wrap items-center gap-3 text-[11px]">
        {!thread.root.deleted_at ? (
          <button
            type="button"
            onClick={() => onReply(thread.root)}
            className="font-bold text-brand-300 hover:text-brand-200"
          >
            Reply
          </button>
        ) : null}
        {action && canManageAction(action) ? (
          <>
            <button
              type="button"
              disabled={busy}
              onClick={() =>
                onChangeAction(action, { status: actionClosed ? "open" : "done" })
              }
              className={`font-bold disabled:opacity-50 ${
                actionClosed ? "text-brand-300 hover:text-brand-200" : "text-emerald-300 hover:opacity-80"
              }`}
            >
              {actionClosed ? "Reopen" : "Mark done"}
            </button>
            {!actionClosed && action.action_status !== "cancelled" ? (
              <button
                type="button"
                disabled={busy}
                onClick={() => onChangeAction(action, { status: "cancelled" })}
                className="font-bold text-text-muted hover:text-state-warning disabled:opacity-50"
              >
                Cancel item
              </button>
            ) : null}
            {canReassignAction(action) ? (
              <details className="inline-block">
                <summary className="cursor-pointer font-bold text-text-muted hover:text-text-primary">
                  Assign
                </summary>
                <div className="mt-1 flex flex-wrap items-center gap-2">
                  <label className="text-[10px] font-bold uppercase tracking-wide text-text-faint">
                    Owner
                    <select
                      aria-label={`Owner for ${productionRoomExcerpt(action.body, 40)}`}
                      value={action.action_owner_user_id ?? ""}
                      disabled={busy}
                      onChange={(event) =>
                        onChangeAction(action, { ownerUserId: event.target.value || null })
                      }
                      className="ml-1 h-7 rounded-lg border border-line-strong bg-ink-950 px-1 text-[11px] font-normal normal-case tracking-normal text-text-primary"
                    >
                      <option value="">Unassigned</option>
                      {members.map((member) => (
                        <option key={member.user_id} value={member.user_id}>
                          {member.display_name}
                        </option>
                      ))}
                    </select>
                  </label>
                  <label className="text-[10px] font-bold uppercase tracking-wide text-text-faint">
                    Due
                    <input
                      type="datetime-local"
                      aria-label={`Due date for ${productionRoomExcerpt(action.body, 40)}`}
                      value={actionDueIsoToInput(action.action_due_at)}
                      disabled={busy}
                      onChange={(event) =>
                        onChangeAction(action, { dueAt: actionDueInputToIso(event.target.value) })
                      }
                      className="ml-1 h-7 rounded-lg border border-line-strong bg-ink-950 px-1 text-[11px] font-normal normal-case tracking-normal text-text-primary"
                    />
                  </label>
                </div>
              </details>
            ) : null}
          </>
        ) : null}
        {canPinProductionRoomMessage(thread.root, isRoomAdmin) ? (
          <button
            type="button"
            disabled={busy}
            onClick={() => onPin(thread.root)}
            className="font-bold text-emerald-300 hover:opacity-80 disabled:opacity-50"
          >
            {thread.root.is_pinned ? "Unpin" : "Pin decision"}
          </button>
        ) : null}
        {canEditProductionRoomMessage(thread.root, userId) || canDeleteProductionRoomMessage(thread.root, userId, isRoomAdmin) ? (
          <span className="ml-auto flex items-center gap-3">
            {canEditProductionRoomMessage(thread.root, userId) ? (
              <button
                type="button"
                onClick={() => onStartEdit(thread.root)}
                className="font-bold text-text-muted hover:text-text-primary"
              >
                Edit
              </button>
            ) : null}
            {canDeleteProductionRoomMessage(thread.root, userId, isRoomAdmin) ? (
              <button
                type="button"
                disabled={busy}
                onClick={() => onDelete(thread.root)}
                className="font-bold text-text-muted hover:text-state-danger disabled:opacity-50"
              >
                Delete
              </button>
            ) : null}
          </span>
        ) : null}
      </footer>
    </article>
  );
}

function MessageBody({
  message,
  editing,
  editDraft,
  onEditDraft,
  onCancelEdit,
  onSaveEdit,
}: {
  message: ProductionRoomMessage;
  editing: boolean;
  editDraft: string;
  onEditDraft: (value: string) => void;
  onCancelEdit: () => void;
  onSaveEdit: () => void;
}) {
  if (message.deleted_at) {
    return <p className="mt-1 text-sm italic text-text-faint">This message was deleted.</p>;
  }
  if (editing) {
    return (
      <div className="mt-1">
        <label className="sr-only" htmlFor={`edit-${message.id}`}>
          Edit message
        </label>
        <textarea
          id={`edit-${message.id}`}
          value={editDraft}
          onChange={(event) => onEditDraft(event.target.value)}
          onKeyDown={(event) => {
            if (event.key === "Escape") onCancelEdit();
          }}
          rows={3}
          className="w-full rounded-xl border border-brand-400/60 bg-ink-950 p-2 text-sm text-text-primary"
        />
        <div className="mt-1 flex gap-2">
          <button
            type="button"
            onClick={onSaveEdit}
            disabled={editDraft.trim().length === 0 || editDraft.trim().length > PRODUCTION_ROOM_MAX_BODY}
            className="rounded-lg bg-brand-400 px-3 py-1 text-[11px] font-bold text-accent-ink disabled:opacity-50"
          >
            Save edit
          </button>
          <button
            type="button"
            onClick={onCancelEdit}
            className="rounded-lg border border-line-strong px-3 py-1 text-[11px] font-bold text-text-muted"
          >
            Cancel
          </button>
        </div>
      </div>
    );
  }
  return <p className="mt-1 whitespace-pre-wrap break-words text-sm leading-relaxed text-text-secondary">{message.body}</p>;
}

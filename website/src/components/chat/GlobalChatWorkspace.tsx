"use client";

import {
  useCallback,
  useEffect,
  useMemo,
  useRef,
  useState,
  type FormEvent,
  type KeyboardEvent,
} from "react";
import type { User } from "@supabase/supabase-js";
import { LocaleLink } from "@/components/ui/LocaleLink";
import { useLocale } from "@/i18n/provider";
import { accountPathForRedirect } from "@/lib/customer-inquiry-chat";
import {
  GLOBAL_CHAT_MAX_BODY,
  GLOBAL_CHAT_PAGE_SIZE,
  GLOBAL_CHAT_PATH,
  countUnreadGlobalChatMessages,
  firstUnreadGlobalChatMessageId,
  formatGlobalChatDateTime,
  formatGlobalChatDay,
  formatGlobalChatTime,
  globalChatDisplayName,
  globalChatMessageExcerpt,
  groupGlobalChatReceiptsByMessage,
  mergeGlobalChatMessages,
  normalizeGlobalChatDraft,
  normalizeGlobalChatMessage,
  normalizeGlobalChatMessages,
  normalizeGlobalChatReadState,
  normalizeGlobalChatReceipt,
  normalizeGlobalChatReceipts,
  summarizeGlobalChatReceipts,
} from "@/lib/global-chat";
import { isSupabaseConfigured, supabaseBrowser } from "@/lib/supabase";
import {
  avatarColorClass,
  avatarSymbol,
  fallbackUserProfile,
  normalizeUserProfile,
  normalizeUserProfiles,
  profileFromMetadata,
} from "@/lib/user-profile";
import type { GlobalChatMessage, GlobalChatReceipt } from "@/types/global-chat";
import type { UserProfile } from "@/types/user-profile";

const MESSAGE_SELECT =
  "id,author_user_id,author_display_name,body,reply_to_message_id,deleted_at,deleted_by,created_at";
const RECEIPT_SELECT = "message_id,user_id,display_name,delivered_at,read_at";
const PROFILE_SELECT = "user_id,display_name,avatar_icon,avatar_color,updated_at";

function mergeReceiptRows(
  current: ReadonlyMap<string, GlobalChatReceipt[]>,
  rows: readonly GlobalChatReceipt[],
): Map<string, GlobalChatReceipt[]> {
  const next = new Map(current);
  for (const [messageId, incoming] of groupGlobalChatReceiptsByMessage(rows)) {
    const merged = normalizeGlobalChatReceipts(next.get(messageId) ?? []);
    for (const receipt of incoming) {
      const index = merged.findIndex((row) => row.user_id === receipt.user_id);
      if (index >= 0) merged[index] = receipt;
      else merged.push(receipt);
    }
    next.set(messageId, merged);
  }
  return next;
}

function firstMessageFromUnknown(value: unknown): GlobalChatMessage | null {
  return normalizeGlobalChatMessages(Array.isArray(value) ? value : [value])[0] ?? null;
}

function recordFromUnknown(value: unknown): Record<string, unknown> | null {
  if (Array.isArray(value)) return value.map(recordFromUnknown).find(Boolean) ?? null;
  return typeof value === "object" && value !== null ? (value as Record<string, unknown>) : null;
}

type ChatUser = {
  id: string;
  email: string | null;
  displayName: string;
  avatarIcon: string;
  avatarColor: string;
};

type ErrorLike = {
  message?: unknown;
  code?: unknown;
  details?: unknown;
};

function toChatUser(user: User | null): ChatUser | null {
  if (!user || typeof user.id !== "string" || !user.id) return null;
  const metadata = recordFromUnknown(user.user_metadata);
  const displayName = globalChatDisplayName({
    email: typeof user.email === "string" ? user.email : null,
    metadata,
  });
  const profile = profileFromMetadata({ userId: user.id, displayName, metadata });
  return {
    id: user.id,
    email: typeof user.email === "string" ? user.email : null,
    displayName: profile.display_name,
    avatarIcon: profile.avatar_icon,
    avatarColor: profile.avatar_color,
  };
}

function sameChatUser(left: ChatUser | null, right: ChatUser | null): boolean {
  return (
    left?.id === right?.id &&
    left?.email === right?.email &&
    left?.displayName === right?.displayName &&
    left?.avatarIcon === right?.avatarIcon &&
    left?.avatarColor === right?.avatarColor
  );
}

function friendlyError(error: unknown, fallback: string): string {
  const err = (typeof error === "object" && error !== null ? error : {}) as ErrorLike;
  const message = error instanceof Error ? error.message : typeof err.message === "string" ? err.message : "";
  const code = typeof err.code === "string" ? err.code : "";
  const details = typeof err.details === "string" ? err.details : "";
  if (/global_chat_|schema cache|does not exist|42P01|PGRST20|PGRST202|PGRST205/i.test(`${code} ${message} ${details}`)) {
    return "Community chat is not installed in this deployment yet.";
  }
  return message || fallback;
}

function ChatAvatar({
  profile,
  sizeClassName = "h-8 w-8",
}: {
  profile: UserProfile;
  sizeClassName?: string;
}) {
  const symbol = avatarSymbol(profile);
  return (
    <span
      aria-hidden="true"
      className={`grid shrink-0 place-items-center rounded-full border font-black ${avatarColorClass(
        profile.avatar_color,
        profile.user_id,
      )} ${sizeClassName} ${profile.avatar_icon === "initials" ? "text-[10px]" : "text-[15px]"}`}
    >
      {symbol}
    </span>
  );
}

function DayDivider({ label }: { label: string }) {
  return (
    <div className="flex items-center gap-3 py-1" aria-label={`Messages from ${label}`}>
      <span className="h-px flex-1 bg-line/70" />
      <span className="rounded-full border border-line-strong bg-ink-900 px-3 py-1 text-3xs font-bold uppercase tracking-wider text-text-faint">
        {label}
      </span>
      <span className="h-px flex-1 bg-line/70" />
    </div>
  );
}

export function GlobalChatWorkspace() {
  const locale = useLocale();
  const configured = isSupabaseConfigured();

  const [checking, setChecking] = useState(true);
  const [user, setUser] = useState<ChatUser | null>(null);
  const [messages, setMessages] = useState<GlobalChatMessage[]>([]);
  const [draft, setDraft] = useState("");
  const [busy, setBusy] = useState(false);
  const [loading, setLoading] = useState(true);
  const [loadingOlder, setLoadingOlder] = useState(false);
  const [error, setError] = useState("");
  const [lastReadAt, setLastReadAt] = useState<string | null>(null);
  const [unreadCount, setUnreadCount] = useState(0);
  const [hasMoreOlder, setHasMoreOlder] = useState(false);
  const [isModerator, setIsModerator] = useState(false);
  const [realtimeStatus, setRealtimeStatus] = useState("connecting");
  const [replyTarget, setReplyTarget] = useState<GlobalChatMessage | null>(null);
  const [receiptsByMessage, setReceiptsByMessage] = useState<Map<string, GlobalChatReceipt[]>>(new Map());
  const [infoMessage, setInfoMessage] = useState<GlobalChatMessage | null>(null);
  const [highlightedMessageId, setHighlightedMessageId] = useState<string | null>(null);
  const [quotedMessages, setQuotedMessages] = useState<Map<string, GlobalChatMessage>>(new Map());
  const [profilesByUserId, setProfilesByUserId] = useState<Map<string, UserProfile>>(new Map());
  // A fresh topic for every mounted workspace prevents Supabase from handing
  // back a same-topic channel from the previous mount while its asynchronous
  // removeChannel() is still leaving. The realtime effect adds its own attempt
  // suffix too, covering an effect restart inside the same mount. A reused
  // subscribed channel can reject new postgres_changes handlers, which must
  // never be able to take down the whole route.
  const [realtimeSessionId] = useState(
    () => `${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 10)}`,
  );
  const realtimeAttemptRef = useRef(0);

  const scrollRef = useRef<HTMLDivElement | null>(null);
  const composerRef = useRef<HTMLTextAreaElement | null>(null);
  const nearBottomRef = useRef(true);
  const messagesRef = useRef<GlobalChatMessage[]>([]);
  const lastReadAtRef = useRef<string | null>(null);
  const markReadRef = useRef<(at?: string | null) => Promise<void>>(async () => {});
  const deliveredSentRef = useRef<Set<string>>(new Set());
  const readSentRef = useRef<Set<string>>(new Set());
  const deliveredQueueRef = useRef<Set<string>>(new Set());
  const deliveredTimerRef = useRef<number | null>(null);
  const quotedFetchingRef = useRef<Set<string>>(new Set());
  const activeUserIdRef = useRef<string | null>(null);
  const authAccessTokenRef = useRef<string | null>(null);
  const profileFetchingRef = useRef<Set<string>>(new Set());
  const profileAttemptedRef = useRef<Set<string>>(new Set());
  const refreshInFlightRef = useRef(false);
  const refreshLatestRef = useRef<() => Promise<void>>(async () => {});
  const visibleReadFrameRef = useRef<number | null>(null);

  const normalizedDraft = useMemo(() => normalizeGlobalChatDraft(draft), [draft]);
  const draftTooLong = normalizedDraft.length > GLOBAL_CHAT_MAX_BODY;
  const canSend = Boolean(user && configured && !busy && normalizedDraft.length > 0 && !draftTooLong);
  const realtimeLabel =
    typeof realtimeStatus === "string" && realtimeStatus ? realtimeStatus.toLowerCase() : "connecting";
  const firstUnreadId = useMemo(
    () => firstUnreadGlobalChatMessageId(messages, user?.id ?? null, lastReadAt),
    [lastReadAt, messages, user?.id],
  );
  const latestMessage = messages[messages.length - 1] ?? null;
  const currentProfile = useMemo<UserProfile | null>(() => {
    if (!user) return null;
    return (
      profilesByUserId.get(user.id) ?? {
        user_id: user.id,
        display_name: user.displayName,
        avatar_icon: user.avatarIcon,
        avatar_color: user.avatarColor,
      }
    );
  }, [profilesByUserId, user]);
  const profileFor = useCallback(
    (userId: string, fallbackName: string): UserProfile =>
      profilesByUserId.get(userId) ??
      (currentProfile && currentProfile.user_id === userId
        ? currentProfile
        : fallbackUserProfile(userId, fallbackName)),
    [currentProfile, profilesByUserId],
  );
  const messagesById = useMemo(() => new Map(messages.map((message) => [message.id, message])), [messages]);
  const infoTarget = infoMessage ? (messagesById.get(infoMessage.id) ?? infoMessage) : null;
  const infoReceipts = infoTarget ? (receiptsByMessage.get(infoTarget.id) ?? []) : [];
  const infoReaders = infoReceipts
    .filter((receipt) => receipt.read_at)
    .sort((a, b) => Date.parse(a.read_at ?? "") - Date.parse(b.read_at ?? ""));
  const infoDeliveredOnly = infoReceipts
    .filter((receipt) => receipt.delivered_at && !receipt.read_at)
    .sort((a, b) => Date.parse(a.delivered_at ?? "") - Date.parse(b.delivered_at ?? ""));

  const scrollToBottom = useCallback((smooth = true) => {
    const element = scrollRef.current;
    if (!element) return;
    try {
      element.scrollTo?.({ top: element.scrollHeight, behavior: smooth ? "smooth" : "auto" });
    } catch {
      // Scrolling is progressive enhancement; it must never crash the room.
    }
    nearBottomRef.current = true;
  }, []);

  const scrollToMessage = useCallback((messageId: string) => {
    try {
      document.getElementById(`global-chat-message-${messageId}`)?.scrollIntoView?.({
        behavior: "smooth",
        block: "center",
      });
    } catch {
      // Jump-to-quote remains best-effort across browsers.
    }
  }, []);

  const fetchUnreadCount = useCallback(
    async (readAt: string | null): Promise<number | null> => {
      if (!user || !configured) return 0;
      try {
        let query = supabaseBrowser()
          .from("global_chat_messages")
          .select("id", { count: "exact", head: true })
          .neq("author_user_id", user.id)
          .is("deleted_at", null);
        if (readAt) query = query.gt("created_at", readAt);
        const { count, error: countError } = await query;
        if (countError) throw countError;
        return count ?? 0;
      } catch {
        return null;
      }
    },
    [configured, user],
  );

  const mergeReceipts = useCallback((rows: readonly GlobalChatReceipt[]) => {
    const normalizedRows = normalizeGlobalChatReceipts(rows);
    if (!normalizedRows.length) return;
    setReceiptsByMessage((current) => mergeReceiptRows(current, normalizedRows));
  }, []);

  const mergeProfiles = useCallback((rows: readonly UserProfile[]) => {
    const normalizedRows = normalizeUserProfiles(rows);
    if (!normalizedRows.length) return;
    setProfilesByUserId((current) => {
      const next = new Map(current);
      for (const profile of normalizedRows) next.set(profile.user_id, profile);
      return next;
    });
  }, []);

  const fetchProfiles = useCallback(
    async (userIds: readonly string[], force = false) => {
      if (!user || !configured) return;
      const unique = [...new Set(userIds)].filter(
        (id) =>
          typeof id === "string" &&
          Boolean(id) &&
          !profileFetchingRef.current.has(id) &&
          (force || !profileAttemptedRef.current.has(id)),
      );
      if (!unique.length) return;
      unique.forEach((id) => {
        profileFetchingRef.current.add(id);
        profileAttemptedRef.current.add(id);
      });
      try {
        for (let start = 0; start < unique.length; start += 100) {
          const { data, error: profileError } = await supabaseBrowser()
            .from("user_profiles")
            .select(PROFILE_SELECT)
            .in("user_id", unique.slice(start, start + 100));
          if (profileError) throw profileError;
          if (activeUserIdRef.current !== user.id) return;
          mergeProfiles(normalizeUserProfiles(data));
        }
      } catch {
        // Avatar profiles are additive. Initials remain a safe fallback if a
        // deployment has not installed the profile table yet.
      } finally {
        unique.forEach((id) => profileFetchingRef.current.delete(id));
      }
    },
    [configured, mergeProfiles, user],
  );

  const fetchReceipts = useCallback(
    async (messageIds: readonly string[]): Promise<GlobalChatReceipt[]> => {
      if (!user || !configured || messageIds.length === 0) return [];
      const fetched: GlobalChatReceipt[] = [];
      try {
        for (let start = 0; start < messageIds.length; start += 200) {
          const { data, error: receiptError } = await supabaseBrowser()
            .from("global_chat_receipts")
            .select(RECEIPT_SELECT)
            .in("message_id", messageIds.slice(start, start + 200));
          if (receiptError) throw receiptError;
          if (activeUserIdRef.current !== user.id) return fetched;
          const rows = normalizeGlobalChatReceipts(data);
          fetched.push(...rows);
          for (const row of rows) {
            if (row.user_id !== user.id) continue;
            if (row.delivered_at) deliveredSentRef.current.add(row.message_id);
            if (row.read_at) readSentRef.current.add(row.message_id);
          }
          mergeReceipts(rows);
        }
      } catch {
        // Receipts are an upgrade layer; the core room must keep working if absent.
      }
      return fetched;
    },
    [configured, mergeReceipts, user],
  );

  const markDelivered = useCallback(
    async (messageIds: readonly string[]) => {
      if (!user || !configured) return;
      const pending = [...new Set(messageIds)].filter((id) => !deliveredSentRef.current.has(id));
      if (!pending.length) return;
      pending.forEach((id) => deliveredSentRef.current.add(id));
      try {
        for (let start = 0; start < pending.length; start += 200) {
          const { data, error: deliveredError } = await supabaseBrowser().rpc(
            "mark_global_chat_receipts_delivered",
            { p_message_ids: pending.slice(start, start + 200) },
          );
          if (deliveredError) throw deliveredError;
          mergeReceipts(normalizeGlobalChatReceipts(data));
        }
      } catch {
        pending.forEach((id) => deliveredSentRef.current.delete(id));
      }
    },
    [configured, mergeReceipts, user],
  );

  const queueDelivered = useCallback(
    (messageIds: readonly string[]) => {
      messageIds.forEach((id) => deliveredQueueRef.current.add(id));
      if (deliveredTimerRef.current !== null) return;
      deliveredTimerRef.current = window.setTimeout(() => {
        deliveredTimerRef.current = null;
        const queued = [...deliveredQueueRef.current];
        deliveredQueueRef.current.clear();
        void markDelivered(queued);
      }, 700);
    },
    [markDelivered],
  );

  const markReceiptsReadUpTo = useCallback(
    async (target: string) => {
      if (!user || !configured) return;
      const targetTime = Date.parse(target);
      const pending = normalizeGlobalChatMessages(messagesRef.current)
        .filter(
          (message) =>
            message.author_user_id !== user.id &&
            !message.deleted_at &&
            Date.parse(message.created_at) <= targetTime &&
            !readSentRef.current.has(message.id),
        )
        .map((message) => message.id);
      if (!pending.length) return;
      pending.forEach((id) => {
        readSentRef.current.add(id);
        deliveredSentRef.current.add(id);
      });
      try {
        for (let start = 0; start < pending.length; start += 200) {
          const { data, error: readReceiptError } = await supabaseBrowser().rpc(
            "mark_global_chat_receipts_read",
            { p_message_ids: pending.slice(start, start + 200) },
          );
          if (readReceiptError) throw readReceiptError;
          mergeReceipts(normalizeGlobalChatReceipts(data));
        }
      } catch {
        pending.forEach((id) => readSentRef.current.delete(id));
      }
    },
    [configured, mergeReceipts, user],
  );

  const markRead = useCallback(
    async (at?: string | null) => {
      if (!user || !configured) return;
      const target =
        at ?? messagesRef.current[messagesRef.current.length - 1]?.created_at ?? new Date().toISOString();
      const targetTime = Date.parse(target);
      if (!Number.isFinite(targetTime)) return;

      try {
        let effectiveReadAt = lastReadAtRef.current;
        const effectiveReadTime = effectiveReadAt ? Date.parse(effectiveReadAt) : 0;

        if (!effectiveReadAt || !Number.isFinite(effectiveReadTime) || effectiveReadTime < targetTime) {
          const { data, error: readError } = await supabaseBrowser().rpc("mark_global_chat_read", {
            p_last_read_at: target,
          });
          if (readError) throw readError;
          effectiveReadAt = normalizeGlobalChatReadState(data)?.last_read_at ?? target;
          lastReadAtRef.current = effectiveReadAt;
          setLastReadAt(effectiveReadAt);
        }

        // Always sync exact per-message receipts up to the effective cursor.
        // This is essential after the receipts upgrade and on a second device:
        // the cursor may already cover messages whose individual read rows do
        // not exist yet, so "unread = 0" must not skip receipt creation.
        await markReceiptsReadUpTo(effectiveReadAt ?? target);
        setUnreadCount(
          countUnreadGlobalChatMessages(messagesRef.current, user.id, effectiveReadAt ?? target),
        );
      } catch (reason) {
        setError(friendlyError(reason, "Unable to update your read position."));
      }
    },
    [configured, markReceiptsReadUpTo, user],
  );

  const markVisibleMessagesRead = useCallback(() => {
    if (!user || !configured || document.visibilityState !== "visible") return;
    const container = scrollRef.current;
    if (!container) return;

    const containerRect = container.getBoundingClientRect();
    const readCursorTime = lastReadAtRef.current ? Date.parse(lastReadAtRef.current) : 0;
    let latestVisible: GlobalChatMessage | null = null;

    for (const message of normalizeGlobalChatMessages(messagesRef.current)) {
      if (message.author_user_id === user.id || message.deleted_at) continue;
      const messageTime = Date.parse(message.created_at);
      const alreadyFullySynced =
        Number.isFinite(messageTime) &&
        Number.isFinite(readCursorTime) &&
        messageTime <= readCursorTime &&
        readSentRef.current.has(message.id);
      if (alreadyFullySynced) continue;

      const element = document.getElementById(`global-chat-message-${message.id}`);
      if (!element) continue;
      const rect = element.getBoundingClientRect();
      if (rect.bottom > containerRect.top + 4 && rect.top < containerRect.bottom - 4) {
        latestVisible = message;
      }
    }

    if (latestVisible) void markReadRef.current(latestVisible.created_at);
  }, [configured, user]);

  const scheduleVisibleMessagesRead = useCallback(() => {
    if (visibleReadFrameRef.current !== null) return;
    visibleReadFrameRef.current = requestAnimationFrame(() => {
      visibleReadFrameRef.current = null;
      markVisibleMessagesRead();
    });
  }, [markVisibleMessagesRead]);

  const loadChat = useCallback(
    async ({ scroll = false }: { scroll?: boolean } = {}) => {
      if (!user || !configured) return;
      setLoading(true);
      setError("");
      try {
        const supabase = supabaseBrowser();
        const [readResult, messageResult, roleResult] = await Promise.all([
          supabase
            .from("global_chat_reads")
            .select("user_id,last_read_at,updated_at")
            .eq("user_id", user.id)
            .maybeSingle(),
          supabase
            .from("global_chat_messages")
            .select(MESSAGE_SELECT)
            .order("created_at", { ascending: false })
            .order("id", { ascending: false })
            .limit(GLOBAL_CHAT_PAGE_SIZE),
          supabase.from("inquiry_operator_roles").select("role,enabled").eq("user_id", user.id).maybeSingle(),
        ]);

        if (readResult.error) throw readResult.error;
        if (messageResult.error) throw messageResult.error;
        if (activeUserIdRef.current !== user.id) return;

        const rowsDescending = normalizeGlobalChatMessages(messageResult.data);
        const nextMessages = mergeGlobalChatMessages([], rowsDescending);
        const readAt = normalizeGlobalChatReadState(readResult.data)?.last_read_at ?? null;
        const fetchedCount = Array.isArray(messageResult.data)
          ? messageResult.data.length
          : rowsDescending.length;

        messagesRef.current = nextMessages;
        lastReadAtRef.current = readAt;
        setMessages(nextMessages);
        setLastReadAt(readAt);
        if (readAt) void markReceiptsReadUpTo(readAt);
        setHasMoreOlder(fetchedCount >= GLOBAL_CHAT_PAGE_SIZE);
        void fetchReceipts(nextMessages.map((message) => message.id));
        queueDelivered(
          nextMessages
            .filter((message) => message.author_user_id !== user.id && !message.deleted_at)
            .map((message) => message.id),
        );

        if (!roleResult.error) {
          const role = recordFromUnknown(roleResult.data);
          setIsModerator(Boolean(role?.enabled === true && role.role === "inquiry_admin"));
        }

        const databaseUnread = await fetchUnreadCount(readAt);
        if (activeUserIdRef.current !== user.id) return;
        const unread = databaseUnread ?? countUnreadGlobalChatMessages(nextMessages, user.id, readAt);
        setUnreadCount(unread);

        if (scroll) {
          const firstUnread = firstUnreadGlobalChatMessageId(nextMessages, user.id, readAt);
          requestAnimationFrame(() => {
            if (unread > 0 && firstUnread) scrollToMessage(firstUnread);
            else scrollToBottom(false);
            window.setTimeout(scheduleVisibleMessagesRead, 350);
          });
        }
      } catch (reason) {
        setError(friendlyError(reason, "Unable to load community chat."));
      } finally {
        setLoading(false);
      }
    },
    [
      configured,
      fetchReceipts,
      fetchUnreadCount,
      markReceiptsReadUpTo,
      queueDelivered,
      scheduleVisibleMessagesRead,
      scrollToBottom,
      scrollToMessage,
      user,
    ],
  );

  const refreshLatest = useCallback(async () => {
    if (!user || !configured || activeUserIdRef.current !== user.id || refreshInFlightRef.current) return;
    refreshInFlightRef.current = true;
    try {
      const supabase = supabaseBrowser();
      const batchSize = Math.max(GLOBAL_CHAT_PAGE_SIZE, Math.min(messagesRef.current.length || 0, 200));
      const { data, error: refreshError } = await supabase
        .from("global_chat_messages")
        .select(MESSAGE_SELECT)
        .order("created_at", { ascending: false })
        .order("id", { ascending: false })
        .limit(batchSize);
      if (refreshError) throw refreshError;
      if (activeUserIdRef.current !== user.id) return;

      const fresh = normalizeGlobalChatMessages(data);
      if (!fresh.length) return;
      const existingIds = new Set(messagesRef.current.map((message) => message.id));
      const merged = mergeGlobalChatMessages(messagesRef.current, fresh);
      const newMessages = merged.filter((message: GlobalChatMessage) => !existingIds.has(message.id));
      messagesRef.current = merged;
      setMessages(merged);
      await fetchReceipts(fresh.map((message) => message.id));
      if (!newMessages.length) return;
      void fetchProfiles(newMessages.map((message) => message.author_user_id), true);

      const newIncoming = newMessages.filter(
        (message) => message.author_user_id !== user.id && !message.deleted_at,
      );
      if (newIncoming.length) queueDelivered(newIncoming.map((message) => message.id));

      const newest = newMessages[newMessages.length - 1];
      if (!newest) return;
      if (newest.author_user_id === user.id) {
        requestAnimationFrame(() => scrollToBottom(true));
      } else if (nearBottomRef.current) {
        requestAnimationFrame(() => {
          scrollToBottom(true);
          window.setTimeout(scheduleVisibleMessagesRead, 80);
        });
      } else {
        setUnreadCount((count) => count + newIncoming.length);
      }
    } catch {
      // Silent background reconcile: a failed poll keeps the visible room
      // intact, and the next 3-second tick or realtime event retries.
    } finally {
      refreshInFlightRef.current = false;
    }
  }, [configured, fetchProfiles, fetchReceipts, queueDelivered, scheduleVisibleMessagesRead, scrollToBottom, user]);

  useEffect(() => {
    refreshLatestRef.current = refreshLatest;
  }, [refreshLatest]);

  useEffect(() => {
    if (!user || !configured) return;
    // Supabase Realtime remains the primary path. This quiet 3-second
    // reconcile guarantees message/receipt convergence even when a browser
    // tab, proxy, or reconnect drops a websocket event.
    const interval = window.setInterval(() => {
      if (document.visibilityState === "visible") void refreshLatest();
    }, 3000);
    const refreshWhenActive = () => void refreshLatest();
    const onVisibility = () => {
      if (document.visibilityState === "visible") refreshWhenActive();
    };
    window.addEventListener("focus", refreshWhenActive);
    window.addEventListener("online", refreshWhenActive);
    document.addEventListener("visibilitychange", onVisibility);
    return () => {
      window.clearInterval(interval);
      window.removeEventListener("focus", refreshWhenActive);
      window.removeEventListener("online", refreshWhenActive);
      document.removeEventListener("visibilitychange", onVisibility);
    };
  }, [configured, refreshLatest, user]);

  const loadOlder = useCallback(async (): Promise<boolean> => {
    const oldest = messagesRef.current[0];
    if (!user || !configured || !oldest || loadingOlder) return false;

    const element = scrollRef.current;
    const oldHeight = element?.scrollHeight ?? 0;
    const oldTop = element?.scrollTop ?? 0;

    setLoadingOlder(true);
    setError("");
    try {
      const { data, error: olderError } = await supabaseBrowser()
        .from("global_chat_messages")
        .select(MESSAGE_SELECT)
        .lt("created_at", oldest.created_at)
        .order("created_at", { ascending: false })
        .order("id", { ascending: false })
        .limit(GLOBAL_CHAT_PAGE_SIZE);
      if (olderError) throw olderError;
      if (activeUserIdRef.current !== user.id) return false;

      const olderRows = mergeGlobalChatMessages([], normalizeGlobalChatMessages(data));
      const merged = mergeGlobalChatMessages(messagesRef.current, olderRows);
      messagesRef.current = merged;
      setMessages(merged);
      const more = (Array.isArray(data) ? data.length : 0) >= GLOBAL_CHAT_PAGE_SIZE;
      setHasMoreOlder(more);
      void fetchReceipts(olderRows.map((message) => message.id));
      void fetchProfiles(olderRows.map((message) => message.author_user_id));
      queueDelivered(
        olderRows
          .filter((message) => message.author_user_id !== user.id && !message.deleted_at)
          .map((message) => message.id),
      );
      if (lastReadAtRef.current) void markReceiptsReadUpTo(lastReadAtRef.current);
      requestAnimationFrame(() => {
        const nextElement = scrollRef.current;
        if (nextElement) nextElement.scrollTop = nextElement.scrollHeight - oldHeight + oldTop;
      });
      return more;
    } catch (reason) {
      setError(friendlyError(reason, "Unable to load older community messages."));
      return false;
    } finally {
      setLoadingOlder(false);
    }
  }, [configured, fetchProfiles, fetchReceipts, loadingOlder, markReceiptsReadUpTo, queueDelivered, user]);

  useEffect(() => {
    messagesRef.current = normalizeGlobalChatMessages(messages);
  }, [messages]);

  useEffect(() => {
    if (!user) return;
    const participantIds = new Set<string>([user.id]);
    for (const message of normalizeGlobalChatMessages(messages)) participantIds.add(message.author_user_id);
    for (const message of quotedMessages.values()) participantIds.add(message.author_user_id);
    for (const receipts of receiptsByMessage.values()) {
      for (const receipt of receipts) participantIds.add(receipt.user_id);
    }
    queueMicrotask(() => {
      void fetchProfiles([...participantIds]);
    });
  }, [fetchProfiles, messages, quotedMessages, receiptsByMessage, user]);

  useEffect(
    () => () => {
      if (deliveredTimerRef.current !== null) window.clearTimeout(deliveredTimerRef.current);
      if (visibleReadFrameRef.current !== null) cancelAnimationFrame(visibleReadFrameRef.current);
    },
    [],
  );

  useEffect(() => {
    lastReadAtRef.current = lastReadAt;
  }, [lastReadAt]);

  useEffect(() => {
    markReadRef.current = markRead;
  }, [markRead]);

  const jumpToQuoted = useCallback(
    async (originalId: string) => {
      const highlight = () => {
        scrollToMessage(originalId);
        setHighlightedMessageId(originalId);
        window.setTimeout(() => {
          setHighlightedMessageId((current) => (current === originalId ? null : current));
        }, 2400);
      };
      if (messagesRef.current.some((message) => message.id === originalId)) {
        highlight();
        return;
      }
      for (let attempt = 0; attempt < 20; attempt += 1) {
        const more = await loadOlder();
        if (messagesRef.current.some((message) => message.id === originalId) || !more) break;
      }
      requestAnimationFrame(highlight);
    },
    [loadOlder, scrollToMessage],
  );

  const startReply = useCallback((message: GlobalChatMessage) => {
    setReplyTarget(message);
    requestAnimationFrame(() => composerRef.current?.focus());
  }, []);

  const openMessageInfo = useCallback(
    (message: GlobalChatMessage) => {
      setInfoMessage(message);
      void fetchReceipts([message.id]).then((rows) => {
        void fetchProfiles([message.author_user_id, ...rows.map((row) => row.user_id)], true);
      });
    },
    [fetchProfiles, fetchReceipts],
  );

  useEffect(() => {
    if (!user || !configured) return;
    const safeMessages = normalizeGlobalChatMessages(messages);
    const known = new Set(safeMessages.map((message) => message.id));
    const missing = [
      ...new Set(
        safeMessages
          .map((message) => message.reply_to_message_id)
          .filter((id): id is string => Boolean(id)),
      ),
    ].filter((id) => !known.has(id) && !quotedMessages.has(id) && !quotedFetchingRef.current.has(id));
    if (!missing.length) return;

    missing.forEach((id) => quotedFetchingRef.current.add(id));
    let alive = true;
    void (async () => {
      try {
        const { data, error: quotedError } = await supabaseBrowser()
          .from("global_chat_messages")
          .select(MESSAGE_SELECT)
          .in("id", missing.slice(0, 50));
        if (!alive || quotedError) return;
        setQuotedMessages((current) => {
          const next = new Map(current);
          for (const row of normalizeGlobalChatMessages(data)) next.set(row.id, row);
          return next;
        });
      } catch {
        // Missing quote context is non-fatal; the reply bubble shows a fallback.
      } finally {
        missing.forEach((id) => quotedFetchingRef.current.delete(id));
      }
    })();
    return () => {
      alive = false;
    };
  }, [configured, messages, quotedMessages, user]);

  useEffect(() => {
    if (!infoMessage) return;
    const onKeyDown = (event: globalThis.KeyboardEvent) => {
      if (event.key === "Escape") setInfoMessage(null);
    };
    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
  }, [infoMessage]);

  useEffect(() => {
    if (!configured) {
      queueMicrotask(() => setChecking(false));
      return;
    }

    let alive = true;
    let subscription: { unsubscribe: () => void } | null = null;

    const applySessionUser = (nextUser: ChatUser | null, accessToken?: string | null) => {
      if (!alive) return;
      if (typeof accessToken === "string" || accessToken === null) {
        const previousToken = authAccessTokenRef.current;
        authAccessTokenRef.current = accessToken || null;
        if (accessToken && accessToken !== previousToken) {
          try {
            void Promise.resolve(supabaseBrowser().realtime.setAuth(accessToken)).catch(() => undefined);
          } catch {
            // The realtime effect also calls setAuth before subscribing.
          }
        }
      }
      const nextUserId = nextUser?.id ?? null;
      if (activeUserIdRef.current !== nextUserId) {
        activeUserIdRef.current = nextUserId;
        messagesRef.current = [];
        lastReadAtRef.current = null;
        deliveredSentRef.current.clear();
        readSentRef.current.clear();
        deliveredQueueRef.current.clear();
        quotedFetchingRef.current.clear();
        profileFetchingRef.current.clear();
        profileAttemptedRef.current.clear();
        refreshInFlightRef.current = false;
        setMessages([]);
        setReceiptsByMessage(new Map());
        setQuotedMessages(new Map());
        setProfilesByUserId(new Map());
        setReplyTarget(null);
        setInfoMessage(null);
        setHighlightedMessageId(null);
        setLastReadAt(null);
        setUnreadCount(0);
        setHasMoreOlder(false);
        setIsModerator(false);
      }
      // Supabase may emit INITIAL_SESSION, TOKEN_REFRESHED, and getSession()
      // for the same account in quick succession. Preserve object identity
      // when nothing user-visible changed so the load and Realtime effects do
      // not restart (and do not race channel teardown) for duplicate events.
      setUser((current) => (sameChatUser(current, nextUser) ? current : nextUser));
      setChecking(false);
    };

    try {
      const supabase = supabaseBrowser();
      void supabase.auth
        .getSession()
        .then(({ data, error: sessionError }) => {
          if (sessionError) throw sessionError;
          applySessionUser(toChatUser(data.session?.user ?? null), data.session?.access_token ?? null);
        })
        .catch((reason: unknown) => {
          if (!alive) return;
          setError(friendlyError(reason, "Unable to confirm your sign-in. Please refresh and sign in again."));
          setChecking(false);
        });

      const { data } = supabase.auth.onAuthStateChange((_event, session) => {
        try {
          applySessionUser(toChatUser(session?.user ?? null), session?.access_token ?? null);
        } catch (reason) {
          if (!alive) return;
          setError(friendlyError(reason, "Unable to confirm your sign-in. Please refresh and sign in again."));
          setChecking(false);
        }
      });
      subscription = data.subscription;
    } catch (reason) {
      queueMicrotask(() => {
        if (!alive) return;
        setError(friendlyError(reason, "Unable to start sign-in. Please refresh and try again."));
        setChecking(false);
      });
    }

    return () => {
      alive = false;
      try {
        subscription?.unsubscribe();
      } catch {
        // Auth teardown is best-effort during route cleanup.
      }
    };
  }, [configured]);

  useEffect(() => {
    if (!user) return;
    queueMicrotask(() => {
      void loadChat({ scroll: true });
    });
  }, [loadChat, user]);

  useEffect(() => {
    if (!user || !configured) return;

    let active = true;
    let removeChannel: (() => void) | null = null;
    void (async () => {
    try {
      const supabase = supabaseBrowser();
      const accessToken = authAccessTokenRef.current;
      if (accessToken) await supabase.realtime.setAuth(accessToken);
      if (!active) return;
      realtimeAttemptRef.current += 1;
      const realtimeTopic = `global-community-chat-${user.id}-${realtimeSessionId}-${realtimeAttemptRef.current}`;
      const channel = supabase.channel(realtimeTopic);
      removeChannel = () => {
        void Promise.resolve(supabase.removeChannel(channel)).catch(() => undefined);
      };
      channel
        .on(
          "postgres_changes",
          { event: "*", schema: "public", table: "global_chat_messages" },
          (payload) => {
            if (!active) return;
            try {
              const previousId = recordFromUnknown(payload.old)?.id;
              if (payload.eventType === "DELETE" && typeof previousId === "string" && previousId) {
                const filtered = normalizeGlobalChatMessages(messagesRef.current).filter(
                  (message) => message.id !== previousId,
                );
                messagesRef.current = filtered;
                setMessages(filtered);
                return;
              }

              const next = normalizeGlobalChatMessage(payload.new);
              if (!next) return;
              const merged = mergeGlobalChatMessages(messagesRef.current, [next]);
              messagesRef.current = merged;
              setMessages(merged);
              void fetchProfiles([next.author_user_id], true);

              const isMine = next.author_user_id === user.id;
              if (payload.eventType === "INSERT" && isMine) {
                requestAnimationFrame(() => scrollToBottom(true));
              } else if (payload.eventType === "INSERT" && !next.deleted_at) {
                queueDelivered([next.id]);
                if (nearBottomRef.current) {
                  requestAnimationFrame(() => scrollToBottom(true));
                  void markReadRef.current(next.created_at);
                } else {
                  setUnreadCount((count) => count + 1);
                }
              }
            } catch (reason) {
              setRealtimeStatus("error");
              setError(
                friendlyError(reason, "A live community message could not be displayed. Refresh to reload the room."),
              );
            }
          },
        )
        .on(
          "postgres_changes",
          { event: "*", schema: "public", table: "global_chat_receipts" },
          (payload) => {
            if (!active) return;
            try {
              const receipt = normalizeGlobalChatReceipt(payload.new);
              if (receipt) mergeReceipts([receipt]);
            } catch {
              // A malformed receipt event must never hide the message stream.
            }
          },
        )
        .on(
          "postgres_changes",
          { event: "*", schema: "public", table: "user_profiles" },
          (payload) => {
            if (!active) return;
            try {
              const profile = normalizeUserProfile(payload.new);
              if (profile) mergeProfiles([profile]);
            } catch {
              // Avatar changes are cosmetic; initials remain the fallback.
            }
          },
        )
        .subscribe((status) => {
          if (!active) return;
          setRealtimeStatus(typeof status === "string" && status ? status : "connecting");
          if (status === "SUBSCRIBED") void refreshLatestRef.current();
        });
    } catch (reason) {
      // Realtime is an enhancement over the already-loaded REST history. A
      // browser/WebSocket/channel setup failure should be visible inline, not
      // replaced by the route error boundary.
      try {
        removeChannel?.();
      } catch {
        // Best-effort cleanup for a channel whose setup failed part-way.
      }
      queueMicrotask(() => {
        if (!active) return;
        setRealtimeStatus("error");
        setError(
          friendlyError(
            reason,
            "Live updates could not start. You can still read the room; refresh to load the latest messages.",
          ),
        );
      });
    }
    })();

    return () => {
      active = false;
      try {
        removeChannel?.();
      } catch {
        // Channel teardown is best-effort; the unique next-mount topic still
        // prevents this cleanup race from affecting a later subscription.
      }
    };
  }, [configured, fetchProfiles, mergeProfiles, mergeReceipts, queueDelivered, realtimeSessionId, scrollToBottom, user]);

  const handleScroll = () => {
    const element = scrollRef.current;
    if (!element) return;
    const nearBottom = element.scrollHeight - element.scrollTop - element.clientHeight < 120;
    nearBottomRef.current = nearBottom;
    scheduleVisibleMessagesRead();
  };

  const jumpToLatest = () => {
    scrollToBottom(true);
    const last = messagesRef.current[messagesRef.current.length - 1];
    if (last) void markRead(last.created_at);
  };

  const send = async (event?: FormEvent) => {
    event?.preventDefault();
    if (!canSend) return;
    setBusy(true);
    setError("");
    try {
      const { data, error: sendError } = await supabaseBrowser().rpc("post_global_chat_message", {
        p_body: normalizedDraft,
        p_reply_to_message_id: replyTarget?.id ?? null,
      });
      if (sendError) throw sendError;
      const row = firstMessageFromUnknown(data);
      if (!row) throw new Error("The message was saved, but its returned row could not be displayed. Refresh the room.");
      const merged = mergeGlobalChatMessages(messagesRef.current, [row]);
      messagesRef.current = merged;
      setMessages(merged);
      setDraft("");
      setReplyTarget(null);
      requestAnimationFrame(() => scrollToBottom(true));
      void markRead(row.created_at);
    } catch (reason) {
      setError(friendlyError(reason, "Unable to send your message."));
    } finally {
      setBusy(false);
    }
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

  const removeMessage = async (message: GlobalChatMessage) => {
    if (busy || message.deleted_at) return;
    if (!window.confirm("Delete this message for everyone in the community room?")) return;
    setBusy(true);
    setError("");
    try {
      const { data, error: deleteError } = await supabaseBrowser().rpc("delete_global_chat_message", {
        p_message_id: message.id,
      });
      if (deleteError) throw deleteError;
      const row = firstMessageFromUnknown(data);
      if (!row) throw new Error("The message was deleted, but its updated row could not be displayed. Refresh the room.");
      const merged = mergeGlobalChatMessages(messagesRef.current, [row]);
      messagesRef.current = merged;
      setMessages(merged);
      setReplyTarget((current) => (current?.id === row.id ? null : current));
      setInfoMessage((current) => (current?.id === row.id ? null : current));
    } catch (reason) {
      setError(friendlyError(reason, "Unable to delete that message."));
    } finally {
      setBusy(false);
    }
  };

  if (checking) {
    return (
      <div role="status" className="flex min-h-[60vh] items-start justify-end bg-ink-950 px-3 py-6 text-xs text-text-muted sm:px-5">
        <div className="grid h-[420px] w-full max-w-[430px] place-items-center rounded-[28px] border border-line-strong bg-ink-900 shadow-panel">
          Checking your secure session…
        </div>
      </div>
    );
  }

  if (!configured) {
    return (
      <main className="flex min-h-[65vh] items-start justify-end bg-ink-950 px-3 py-6 text-text-primary sm:px-5">
        <section className="w-full max-w-[430px] rounded-[28px] border border-state-warning/40 bg-state-warning/10 p-6 text-center shadow-panel">
          <p className="eyebrow text-state-warning">Community chat</p>
          <h1 className="mt-3 text-2xl font-bold">Chat is not configured in this deployment</h1>
          <p className="mt-3 text-sm leading-relaxed text-text-muted">
            Supabase public credentials are missing, so the signed-in community room cannot connect here.
          </p>
          <LocaleLink
            href="/inquiries/chat"
            className="mt-6 inline-flex rounded-xl bg-brand-400 px-5 py-3 text-sm font-bold text-accent-ink"
          >
            Go to private inquiry chat
          </LocaleLink>
        </section>
      </main>
    );
  }

  if (!user) {
    return (
      <main className="flex min-h-[65vh] items-start justify-end bg-ink-950 px-3 py-6 text-text-primary sm:px-5">
        <section className="w-full max-w-[430px] rounded-[28px] border border-line-strong bg-ink-900 p-6 text-center shadow-panel">
          <p className="eyebrow text-brand-300">Community chat</p>
          <h1 className="mt-3 text-2xl font-bold">Sign in to join the ProGlass community room</h1>
          <p className="mt-3 text-sm leading-relaxed text-text-muted">
            This is one live group room for signed-in ProGlass users. Sign in or create an account to read and post.
            Keep private project details, documents, and prices in your private inquiry chat.
          </p>
          {error ? (
            <p
              role="alert"
              className="mx-auto mt-4 max-w-md rounded-xl border border-state-danger/40 bg-state-danger/10 px-4 py-3 text-xs font-semibold leading-relaxed text-state-danger"
            >
              {error}
            </p>
          ) : null}
          <div className="mt-6 flex flex-col justify-center gap-3 sm:flex-row">
            <LocaleLink
              href={accountPathForRedirect(GLOBAL_CHAT_PATH)}
              className="inline-flex justify-center rounded-xl bg-brand-400 px-5 py-3 text-sm font-bold text-accent-ink"
            >
              Sign in
            </LocaleLink>
            <LocaleLink
              href={accountPathForRedirect(GLOBAL_CHAT_PATH, "signup")}
              className="inline-flex justify-center rounded-xl border border-line-strong px-5 py-3 text-sm font-bold text-brand-300"
            >
              Create account
            </LocaleLink>
          </div>
          <LocaleLink href="/inquiries/chat" className="mt-5 inline-flex text-xs font-bold text-brand-300 underline underline-offset-4">
            I need private inquiry chat instead
          </LocaleLink>
        </section>
      </main>
    );
  }

  return (
    <main className="bg-ink-950 px-3 py-4 text-text-primary sm:px-5">
      <div className="mx-auto flex w-full max-w-[1180px] justify-end">
        <section
          aria-label="ProGlass community chat room"
          className="relative flex h-[calc(100dvh-175px)] max-h-[720px] min-h-[500px] w-full max-w-[430px] flex-col overflow-hidden rounded-[28px] border border-line-strong bg-ink-900 shadow-raised"
        >
          <header className="border-b border-line bg-ink-900 px-3 py-3">
            <div className="flex items-center gap-2.5">
              <span className="grid h-10 w-10 shrink-0 place-items-center rounded-full bg-brand-400 text-xs font-black text-accent-ink">
                PG
              </span>
              <div className="min-w-0 flex-1">
                <h1 className="truncate text-[15px] font-bold leading-tight">ProGlass Community</h1>
                <p className="mt-0.5 flex items-center gap-1.5 text-[10px] leading-tight text-text-faint">
                  <span
                    aria-hidden="true"
                    className={`h-2 w-2 rounded-full ${realtimeLabel === "subscribed" ? "bg-emerald-400" : "bg-state-warning"}`}
                  />
                  <span>{realtimeLabel === "subscribed" ? "Live now" : "Auto-refresh on"}</span>
                  <span aria-hidden="true">·</span>
                  <span>{messages.length} loaded</span>
                  {latestMessage ? (
                    <>
                      <span aria-hidden="true">·</span>
                      <time dateTime={latestMessage.created_at}>
                        {formatGlobalChatTime(latestMessage.created_at, locale)}
                      </time>
                    </>
                  ) : null}
                </p>
              </div>
              {unreadCount > 0 ? (
                <span className="grid h-6 min-w-6 shrink-0 place-items-center rounded-full bg-emerald-400 px-1.5 text-[10px] font-black text-accent-ink">
                  {unreadCount > 99 ? "99+" : unreadCount}
                </span>
              ) : null}
              <button
                type="button"
                onClick={() => void refreshLatest()}
                aria-label="Refresh messages now"
                title="Refresh messages now"
                className="grid h-8 w-8 shrink-0 place-items-center rounded-full border border-line-strong text-sm font-black text-brand-300 hover:bg-ink-850"
              >
                ↻
              </button>
            </div>

            <div className="mt-2 flex items-center gap-2 rounded-2xl border border-line bg-ink-950/70 px-2 py-1.5">
              {currentProfile ? <ChatAvatar profile={currentProfile} sizeClassName="h-7 w-7" /> : null}
              <div className="min-w-0 flex-1">
                <p className="truncate text-[11px] font-bold leading-tight">
                  {currentProfile?.display_name ?? user.displayName}
                </p>
                <p className="truncate text-[10px] leading-tight text-text-faint">
                  {user.email ?? "Signed in"}
                  {isModerator ? " · Moderator" : ""}
                </p>
              </div>
              <LocaleLink
                href="/account"
                className="shrink-0 rounded-full border border-line-strong px-2 py-1 text-[10px] font-bold text-brand-300"
              >
                Edit profile
              </LocaleLink>
              <LocaleLink
                href="/inquiries/chat"
                className="shrink-0 rounded-full border border-line-strong px-2 py-1 text-[10px] font-bold text-text-secondary"
              >
                Private chat
              </LocaleLink>
            </div>
          </header>

          <details className="border-b border-line bg-brand-400/10 px-3 py-2 text-[11px] leading-relaxed text-brand-100">
            <summary className="cursor-pointer font-bold">Public room · keep private work private</summary>
            <p className="mt-1 text-text-muted">
              Every signed-in ProGlass user can read this room. Do not share project details, documents, prices, or
              personal contact information here — use{" "}
              <LocaleLink href="/inquiries/chat" className="font-bold text-brand-200 underline underline-offset-4">
                My inquiries &amp; chat
              </LocaleLink>{" "}
              for private work. Read receipts are on: use Message info to see who received and read each message.
            </p>
          </details>

          {error ? (
            <div
              role="alert"
              className="mx-3 mt-2 rounded-2xl border border-state-danger/40 bg-state-danger/10 px-3 py-2 text-[11px] font-semibold leading-relaxed text-state-danger"
            >
              {error}
            </div>
          ) : null}

            <div className="relative flex min-h-0 flex-1 flex-col">
              <div
                ref={scrollRef}
                onScroll={handleScroll}
                role="log"
                aria-live="polite"
                aria-label="Community messages"
                className="min-h-0 flex-1 space-y-3 overflow-y-auto scroll-smooth bg-ink-950/70 px-3 py-3"
              >
                {hasMoreOlder ? (
                  <div className="flex justify-center">
                    <button
                      type="button"
                      disabled={loadingOlder}
                      onClick={() => void loadOlder()}
                      className="rounded-full border border-line-strong bg-ink-900 px-4 py-2 text-xs font-bold text-brand-300 disabled:opacity-50"
                    >
                      {loadingOlder ? "Loading older messages…" : "Load older messages"}
                    </button>
                  </div>
                ) : null}

                {loading && messages.length === 0 ? (
                  <div role="status" className="grid h-full place-items-center text-sm text-text-muted">
                    Loading community messages…
                  </div>
                ) : messages.length === 0 ? (
                  <div className="grid h-full place-items-center px-4 text-center">
                    <div>
                      <div className="mx-auto grid h-14 w-14 place-items-center rounded-full bg-brand-400/15 text-2xl">💬</div>
                      <h3 className="mt-4 text-lg font-bold">No messages yet</h3>
                      <p className="mx-auto mt-2 max-w-sm text-sm leading-relaxed text-text-muted">
                        Start the conversation. General questions, introductions, and glass-work discussion are welcome.
                      </p>
                    </div>
                  </div>
                ) : (
                  messages.map((message, index) => {
                    const previous = messages[index - 1];
                    const day = formatGlobalChatDay(message.created_at, locale);
                    const previousDay = previous ? formatGlobalChatDay(previous.created_at, locale) : null;
                    const showDay = day !== previousDay;
                    const showUnread = message.id === firstUnreadId;
                    const own = message.author_user_id === user.id;
                    const deleted = Boolean(message.deleted_at);
                    const showAuthor =
                      !own && (!previous || previous.author_user_id !== message.author_user_id || showDay || showUnread);
                    const canDelete = !deleted && (own || isModerator);
                    const replyToId = message.reply_to_message_id;
                    const quoted = replyToId
                      ? (messagesById.get(replyToId) ?? quotedMessages.get(replyToId) ?? null)
                      : null;
                    const receiptSummary = summarizeGlobalChatReceipts(receiptsByMessage.get(message.id) ?? []);
                    const highlighted = highlightedMessageId === message.id;
                    const authorProfile = profileFor(message.author_user_id, message.author_display_name);
                    const authorName = authorProfile.display_name || message.author_display_name;
                    const quotedAuthorName = quoted
                      ? quoted.author_user_id === user.id
                        ? "You"
                        : profileFor(quoted.author_user_id, quoted.author_display_name).display_name
                      : "Original message";
                    const receiptActionLabel =
                      receiptSummary.readCount > 0
                        ? `Seen by ${receiptSummary.readCount} · Info`
                        : receiptSummary.deliveredCount > 0
                          ? `Delivered to ${receiptSummary.deliveredCount} · Info`
                          : "Message info";

                    return (
                      <div key={message.id} className="space-y-3">
                        {showDay ? <DayDivider label={day} /> : null}
                        {showUnread ? (
                          <div className="flex items-center gap-3" aria-label="Unread messages start here">
                            <span className="h-px flex-1 bg-emerald-400/60" />
                            <span className="rounded-full bg-emerald-400 px-3 py-1 text-3xs font-black uppercase tracking-wider text-accent-ink">
                              Unread messages
                            </span>
                            <span className="h-px flex-1 bg-emerald-400/60" />
                          </div>
                        ) : null}

                        <div
                          id={`global-chat-message-${message.id}`}
                          className={`flex items-end gap-2 ${own ? "justify-end" : "justify-start"}`}
                        >
                          {!own ? <ChatAvatar profile={authorProfile} sizeClassName="h-7 w-7" /> : null}

                          <div className={`flex max-w-[84%] flex-col ${own ? "items-end" : "items-start"}`}>
                            {showAuthor ? (
                              <div className="mb-1 px-1 text-[10px] font-bold leading-tight text-brand-200">
                                {authorName}
                              </div>
                            ) : null}
                            <article
                              className={`rounded-[20px] px-3 py-2 shadow-panel transition-shadow ${
                                deleted
                                  ? "border border-line bg-ink-950/80 text-text-faint"
                                  : own
                                    ? "rounded-br-md bg-brand-400 text-accent-ink"
                                    : "rounded-bl-md border border-line-strong bg-ink-850 text-text-primary"
                              } ${highlighted ? "ring-2 ring-emerald-300 ring-offset-2 ring-offset-ink-950" : ""}`}
                            >
                              {replyToId && !deleted ? (
                                <button
                                  type="button"
                                  onClick={() => void jumpToQuoted(replyToId)}
                                  aria-label="Jump to the original message"
                                  className={`mb-2 block w-full rounded-2xl border-l-4 px-3 py-2 text-left ${
                                    own
                                      ? "border-accent-ink/60 bg-accent-ink/10 text-accent-ink"
                                      : "border-emerald-400/80 bg-ink-950/70 text-text-primary"
                                  }`}
                                >
                                  <span
                                    className={`block text-3xs font-black uppercase tracking-wider ${
                                      own ? "text-accent-ink/80" : "text-emerald-300"
                                    }`}
                                  >
                                    {quotedAuthorName}
                                  </span>
                                  <span className="mt-0.5 line-clamp-2 block text-xs leading-relaxed opacity-90">
                                    {quoted
                                      ? quoted.deleted_at
                                        ? "This message was deleted."
                                        : globalChatMessageExcerpt(quoted.body, 140)
                                      : "Loading original message…"}
                                  </span>
                                </button>
                              ) : null}
                              <p className={`whitespace-pre-wrap break-words text-[13px] leading-relaxed ${deleted ? "italic" : ""}`}>
                                {deleted ? "This message was deleted." : message.body}
                              </p>
                              <div
                                className={`mt-1 flex items-center justify-end text-[10px] font-semibold ${
                                  deleted ? "text-text-faint" : own ? "text-accent-ink/70" : "text-text-faint"
                                }`}
                              >
                                <time dateTime={message.created_at}>{formatGlobalChatTime(message.created_at, locale)}</time>
                                {own && !deleted ? (
                                  <button
                                    type="button"
                                    onClick={() => openMessageInfo(message)}
                                    title={
                                      receiptSummary.state === "read"
                                        ? `Seen by ${receiptSummary.readCount}`
                                        : receiptSummary.state === "delivered"
                                          ? `Delivered to ${receiptSummary.deliveredCount}`
                                          : "Sent"
                                    }
                                    aria-label={`Message info: ${
                                      receiptSummary.state === "read"
                                        ? `seen by ${receiptSummary.readCount}`
                                        : receiptSummary.state === "delivered"
                                          ? `delivered to ${receiptSummary.deliveredCount}`
                                          : "sent"
                                    }. Show who read this message`}
                                    className={`ml-1.5 text-[11px] font-black leading-none tracking-[-0.18em] ${
                                      receiptSummary.state === "read" ? "text-sky-700" : ""
                                    }`}
                                  >
                                    {receiptSummary.state === "sent" ? "✓" : "✓✓"}
                                  </button>
                                ) : null}
                              </div>
                            </article>
                            {!deleted ? (
                              <div className={`mt-1 flex flex-wrap items-center gap-x-3 gap-y-0.5 px-1 ${own ? "justify-end" : ""}`}>
                                <button
                                  type="button"
                                  disabled={busy}
                                  onClick={() => startReply(message)}
                                  className="text-3xs font-bold text-text-faint underline-offset-4 hover:text-brand-300 hover:underline disabled:opacity-50"
                                >
                                  Reply
                                </button>
                                <button
                                  type="button"
                                  onClick={() => openMessageInfo(message)}
                                  className="text-3xs font-bold text-text-faint underline-offset-4 hover:text-brand-300 hover:underline"
                                >
                                  {receiptActionLabel}
                                </button>
                                {canDelete ? (
                                  <button
                                    type="button"
                                    disabled={busy}
                                    onClick={() => void removeMessage(message)}
                                    className="text-3xs font-bold text-text-faint underline-offset-4 hover:text-state-danger hover:underline disabled:opacity-50"
                                  >
                                    Delete for everyone
                                  </button>
                                ) : null}
                              </div>
                            ) : null}
                          </div>

                          {own && currentProfile ? (
                            <ChatAvatar profile={currentProfile} sizeClassName="h-7 w-7" />
                          ) : null}
                        </div>
                      </div>
                    );
                  })
                )}
              </div>

              {unreadCount > 0 ? (
                <button
                  type="button"
                  onClick={jumpToLatest}
                  className="absolute bottom-3 left-1/2 -translate-x-1/2 rounded-full bg-emerald-400 px-4 py-2 text-xs font-black text-accent-ink shadow-raised"
                >
                  {unreadCount} unread · Jump to latest
                </button>
              ) : null}
            </div>

            <form onSubmit={(event) => void send(event)} className="border-t border-line bg-ink-900 p-2.5">
              {replyTarget ? (
                <div className="mb-2 flex items-start gap-3 rounded-2xl border border-line-strong border-l-4 border-l-emerald-400 bg-ink-950 px-3 py-2">
                  <div className="min-w-0 flex-1">
                    <p className="text-3xs font-black uppercase tracking-wider text-emerald-300">
                      Replying to {
                        replyTarget.author_user_id === user.id
                          ? "yourself"
                          : profileFor(replyTarget.author_user_id, replyTarget.author_display_name).display_name
                      }
                    </p>
                    <p className="mt-0.5 line-clamp-2 text-xs leading-relaxed text-text-muted">
                      {globalChatMessageExcerpt(replyTarget.body, 180)}
                    </p>
                  </div>
                  <button
                    type="button"
                    aria-label="Cancel reply"
                    onClick={() => setReplyTarget(null)}
                    className="grid h-7 w-7 shrink-0 place-items-center rounded-full border border-line-strong text-xs font-black text-text-muted hover:text-text-primary"
                  >
                    ✕
                  </button>
                </div>
              ) : null}
              <div className="flex items-end gap-2">
                <label htmlFor="global-chat-composer" className="sr-only">
                  Message the ProGlass community
                </label>
                <textarea
                  ref={composerRef}
                  id="global-chat-composer"
                  value={draft}
                  onChange={(event) => setDraft(event.target.value)}
                  onKeyDown={onComposerKeyDown}
                  rows={1}
                  placeholder={replyTarget ? "Type your reply…" : "Message…"}
                  className="max-h-28 min-h-10 flex-1 resize-none rounded-full border border-line-strong bg-ink-950 px-4 py-2.5 text-[13px] leading-relaxed text-text-primary placeholder:text-text-faint focus:border-brand-400 focus:outline-none focus:ring-2 focus:ring-brand-400/40"
                />
                <button
                  type="submit"
                  disabled={!canSend}
                  className="h-10 shrink-0 rounded-full bg-brand-400 px-4 text-[13px] font-black text-accent-ink transition-opacity disabled:cursor-not-allowed disabled:opacity-50"
                >
                  {busy ? "Sending…" : "Send"}
                </button>
              </div>
              <div className="mt-2 flex items-center justify-between gap-3 px-1 text-3xs text-text-faint">
                <span>Enter to send · Shift+Enter for a new line</span>
                <span className={draftTooLong ? "font-bold text-state-danger" : undefined}>
                  {normalizedDraft.length}/{GLOBAL_CHAT_MAX_BODY}
                </span>
              </div>
              {draftTooLong ? (
                <p role="alert" className="mt-2 px-1 text-xs font-semibold text-state-danger">
                  Message is too long. Please keep it within {GLOBAL_CHAT_MAX_BODY} characters.
                </p>
              ) : null}
            </form>
          </section>
      </div>

      {infoTarget ? (
        <div
          className="fixed inset-0 z-50 flex items-end justify-end p-3 sm:p-5"
          role="dialog"
          aria-modal="true"
          aria-label="Message info"
        >
          <button
            type="button"
            aria-label="Close message info"
            onClick={() => setInfoMessage(null)}
            className="absolute inset-0 bg-ink-950/70"
          />
          <aside className="relative flex max-h-[min(720px,calc(100dvh-2rem))] w-full max-w-[430px] flex-col overflow-hidden rounded-[28px] border border-line-strong bg-ink-900 shadow-raised">
            <header className="flex items-center justify-between gap-3 border-b border-line px-4 py-3">
              <h2 className="text-base font-bold">Message info</h2>
              <button
                type="button"
                aria-label="Close"
                onClick={() => setInfoMessage(null)}
                className="grid h-8 w-8 place-items-center rounded-full border border-line-strong text-sm font-black text-text-muted hover:text-text-primary"
              >
                ✕
              </button>
            </header>
            <div className="flex-1 space-y-4 overflow-y-auto px-4 py-3">
              <div className="rounded-3xl border border-line-strong bg-ink-950/70 p-3">
                <div className="flex items-center gap-2">
                  <ChatAvatar
                    profile={profileFor(infoTarget.author_user_id, infoTarget.author_display_name)}
                    sizeClassName="h-7 w-7"
                  />
                  <p className="text-[10px] font-black uppercase tracking-wider text-brand-300">
                    {infoTarget.author_user_id === user.id
                      ? "You"
                      : profileFor(infoTarget.author_user_id, infoTarget.author_display_name).display_name}
                  </p>
                </div>
                <p className="mt-2 whitespace-pre-wrap break-words text-sm leading-relaxed">
                  {globalChatMessageExcerpt(infoTarget.body, 300)}
                </p>
                <p className="mt-2 text-3xs text-text-faint">
                  Sent · {formatGlobalChatDateTime(infoTarget.created_at, locale)}
                </p>
              </div>

              <section aria-label="Read by">
                <h3 className="flex items-center gap-2 text-sm font-bold">
                  <span aria-hidden="true" className="font-black tracking-[-0.18em] text-sky-400">
                    ✓✓
                  </span>
                  Read by{infoReaders.length ? ` (${infoReaders.length})` : ""}
                </h3>
                {infoReaders.length ? (
                  <ul className="mt-3 space-y-2">
                    {infoReaders.map((receipt) => {
                      const receiptProfile = profileFor(receipt.user_id, receipt.display_name);
                      return (
                        <li
                          key={receipt.user_id}
                          className="flex items-center gap-3 rounded-2xl border border-line bg-ink-950/60 px-3 py-2.5"
                        >
                          <ChatAvatar profile={receiptProfile} sizeClassName="h-9 w-9" />
                          <span className="min-w-0">
                            <span className="block truncate text-sm font-bold">{receiptProfile.display_name}</span>
                            <span className="block text-[10px] leading-relaxed text-text-faint">
                              Read · {formatGlobalChatDateTime(receipt.read_at, locale)}
                              {receipt.delivered_at
                                ? ` · Delivered · ${formatGlobalChatDateTime(receipt.delivered_at, locale)}`
                                : ""}
                            </span>
                          </span>
                        </li>
                      );
                    })}
                  </ul>
                ) : (
                  <p className="mt-2 text-xs leading-relaxed text-text-muted">Not read by anyone yet.</p>
                )}
              </section>

              <section aria-label="Delivered to">
                <h3 className="flex items-center gap-2 text-sm font-bold">
                  <span aria-hidden="true" className="font-black tracking-[-0.18em] text-text-faint">
                    ✓✓
                  </span>
                  Delivered to{infoDeliveredOnly.length ? ` (${infoDeliveredOnly.length})` : ""}
                </h3>
                {infoDeliveredOnly.length ? (
                  <ul className="mt-3 space-y-2">
                    {infoDeliveredOnly.map((receipt) => {
                      const receiptProfile = profileFor(receipt.user_id, receipt.display_name);
                      return (
                        <li
                          key={receipt.user_id}
                          className="flex items-center gap-3 rounded-2xl border border-line bg-ink-950/60 px-3 py-2.5"
                        >
                          <ChatAvatar profile={receiptProfile} sizeClassName="h-9 w-9" />
                          <span className="min-w-0">
                            <span className="block truncate text-sm font-bold">{receiptProfile.display_name}</span>
                            <span className="block text-[10px] leading-relaxed text-text-faint">
                              Delivered · {formatGlobalChatDateTime(receipt.delivered_at, locale)}
                            </span>
                          </span>
                        </li>
                      );
                    })}
                  </ul>
                ) : (
                  <p className="mt-2 text-xs leading-relaxed text-text-muted">
                    No one is only at “delivered” — anyone who received it has also read it.
                  </p>
                )}
              </section>

              <p className="text-3xs leading-relaxed text-text-faint">
                Delivered means the message reached that signed-in user’s open chat. Receipts are visible to signed-in
                room participants, like a WhatsApp group.
              </p>
            </div>
          </aside>
        </div>
      ) : null}
    </main>
  );
}

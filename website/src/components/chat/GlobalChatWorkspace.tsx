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
  authorInitials,
  countUnreadGlobalChatMessages,
  firstUnreadGlobalChatMessageId,
  formatGlobalChatDay,
  formatGlobalChatTime,
  globalChatDisplayName,
  mergeGlobalChatMessages,
  normalizeGlobalChatDraft,
} from "@/lib/global-chat";
import { isSupabaseConfigured, supabaseBrowser } from "@/lib/supabase";
import type { GlobalChatMessage, GlobalChatReadState } from "@/types/global-chat";

type ChatUser = {
  id: string;
  email: string | null;
  displayName: string;
};

type ErrorLike = {
  message?: unknown;
  code?: unknown;
  details?: unknown;
};

const AVATAR_CLASSES = [
  "bg-brand-500/20 text-brand-200 border-brand-400/40",
  "bg-emerald-500/20 text-emerald-200 border-emerald-400/40",
  "bg-sky-500/20 text-sky-200 border-sky-400/40",
  "bg-amber-500/20 text-amber-200 border-amber-400/40",
  "bg-rose-500/20 text-rose-200 border-rose-400/40",
] as const;

function toChatUser(user: User | null): ChatUser | null {
  if (!user) return null;
  return {
    id: user.id,
    email: user.email ?? null,
    displayName: globalChatDisplayName({
      email: user.email,
      metadata: user.user_metadata as Record<string, unknown> | null,
    }),
  };
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

function avatarClass(authorId: string): string {
  let hash = 0;
  for (const char of authorId) hash = (hash * 31 + char.charCodeAt(0)) >>> 0;
  return AVATAR_CLASSES[hash % AVATAR_CLASSES.length] ?? AVATAR_CLASSES[0];
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
  const [isNearBottom, setIsNearBottom] = useState(true);

  const scrollRef = useRef<HTMLDivElement | null>(null);
  const nearBottomRef = useRef(true);
  const messagesRef = useRef<GlobalChatMessage[]>([]);
  const lastReadAtRef = useRef<string | null>(null);
  const markReadRef = useRef<(at?: string | null) => Promise<void>>(async () => {});

  const normalizedDraft = useMemo(() => normalizeGlobalChatDraft(draft), [draft]);
  const draftTooLong = normalizedDraft.length > GLOBAL_CHAT_MAX_BODY;
  const canSend = Boolean(user && configured && !busy && normalizedDraft.length > 0 && !draftTooLong);
  const firstUnreadId = useMemo(
    () => firstUnreadGlobalChatMessageId(messages, user?.id ?? null, lastReadAt),
    [lastReadAt, messages, user?.id],
  );
  const latestMessage = messages[messages.length - 1] ?? null;
  const latestPreview = useMemo(() => {
    const latestVisible = [...messages].reverse().find((message) => !message.deleted_at);
    return latestVisible?.body ?? "No messages yet. Say hello to the community.";
  }, [messages]);

  const scrollToBottom = useCallback((smooth = true) => {
    const element = scrollRef.current;
    if (!element) return;
    element.scrollTo({ top: element.scrollHeight, behavior: smooth ? "smooth" : "auto" });
    nearBottomRef.current = true;
    setIsNearBottom(true);
  }, []);

  const scrollToMessage = useCallback((messageId: string) => {
    document.getElementById(`global-chat-message-${messageId}`)?.scrollIntoView({
      behavior: "smooth",
      block: "center",
    });
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

  const markRead = useCallback(
    async (at?: string | null) => {
      if (!user || !configured) return;
      const target =
        at ?? messagesRef.current[messagesRef.current.length - 1]?.created_at ?? new Date().toISOString();
      if (lastReadAtRef.current && Date.parse(lastReadAtRef.current) >= Date.parse(target)) {
        setUnreadCount(0);
        return;
      }
      try {
        const { data, error: readError } = await supabaseBrowser().rpc("mark_global_chat_read", {
          p_last_read_at: target,
        });
        if (readError) throw readError;
        const nextReadAt = (data as GlobalChatReadState | null)?.last_read_at ?? target;
        lastReadAtRef.current = nextReadAt;
        setLastReadAt(nextReadAt);
        setUnreadCount(0);
      } catch (reason) {
        setError(friendlyError(reason, "Unable to update your read position."));
      }
    },
    [configured, user],
  );

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
            .select("id,author_user_id,author_display_name,body,deleted_at,deleted_by,created_at")
            .order("created_at", { ascending: false })
            .order("id", { ascending: false })
            .limit(GLOBAL_CHAT_PAGE_SIZE),
          supabase.from("inquiry_operator_roles").select("role,enabled").eq("user_id", user.id).maybeSingle(),
        ]);

        if (readResult.error) throw readResult.error;
        if (messageResult.error) throw messageResult.error;

        const rowsDescending = (messageResult.data ?? []) as GlobalChatMessage[];
        const nextMessages = [...rowsDescending].reverse();
        const readAt = (readResult.data as GlobalChatReadState | null)?.last_read_at ?? null;

        messagesRef.current = nextMessages;
        lastReadAtRef.current = readAt;
        setMessages(nextMessages);
        setLastReadAt(readAt);
        setHasMoreOlder(rowsDescending.length >= GLOBAL_CHAT_PAGE_SIZE);

        if (!roleResult.error) {
          const role = roleResult.data as { role?: string; enabled?: boolean } | null;
          setIsModerator(Boolean(role?.enabled && role.role === "inquiry_admin"));
        }

        const databaseUnread = await fetchUnreadCount(readAt);
        const unread = databaseUnread ?? countUnreadGlobalChatMessages(nextMessages, user.id, readAt);
        setUnreadCount(unread);

        if (scroll) {
          const firstUnread = firstUnreadGlobalChatMessageId(nextMessages, user.id, readAt);
          requestAnimationFrame(() => {
            if (unread > 0 && firstUnread) scrollToMessage(firstUnread);
            else scrollToBottom(false);
          });
        }
      } catch (reason) {
        setError(friendlyError(reason, "Unable to load community chat."));
      } finally {
        setLoading(false);
      }
    },
    [configured, fetchUnreadCount, scrollToBottom, scrollToMessage, user],
  );

  const loadOlder = useCallback(async () => {
    const oldest = messagesRef.current[0];
    if (!user || !configured || !oldest || loadingOlder) return;

    const element = scrollRef.current;
    const oldHeight = element?.scrollHeight ?? 0;
    const oldTop = element?.scrollTop ?? 0;

    setLoadingOlder(true);
    setError("");
    try {
      const { data, error: olderError } = await supabaseBrowser()
        .from("global_chat_messages")
        .select("id,author_user_id,author_display_name,body,deleted_at,deleted_by,created_at")
        .lt("created_at", oldest.created_at)
        .order("created_at", { ascending: false })
        .order("id", { ascending: false })
        .limit(GLOBAL_CHAT_PAGE_SIZE);
      if (olderError) throw olderError;

      const olderRows = ((data ?? []) as GlobalChatMessage[]).slice().reverse();
      const merged = mergeGlobalChatMessages(messagesRef.current, olderRows);
      messagesRef.current = merged;
      setMessages(merged);
      setHasMoreOlder((data ?? []).length >= GLOBAL_CHAT_PAGE_SIZE);
      requestAnimationFrame(() => {
        const nextElement = scrollRef.current;
        if (nextElement) nextElement.scrollTop = nextElement.scrollHeight - oldHeight + oldTop;
      });
    } catch (reason) {
      setError(friendlyError(reason, "Unable to load older community messages."));
    } finally {
      setLoadingOlder(false);
    }
  }, [configured, loadingOlder, user]);

  useEffect(() => {
    messagesRef.current = messages;
  }, [messages]);

  useEffect(() => {
    lastReadAtRef.current = lastReadAt;
  }, [lastReadAt]);

  useEffect(() => {
    markReadRef.current = markRead;
  }, [markRead]);

  useEffect(() => {
    if (!configured) {
      queueMicrotask(() => setChecking(false));
      return;
    }

    let alive = true;
    const supabase = supabaseBrowser();
    void supabase.auth.getSession().then(({ data }) => {
      if (!alive) return;
      setUser(toChatUser(data.session?.user ?? null));
      setChecking(false);
    });

    const {
      data: { subscription },
    } = supabase.auth.onAuthStateChange((_event, session) => {
      if (!alive) return;
      setUser(toChatUser(session?.user ?? null));
      setChecking(false);
    });

    return () => {
      alive = false;
      subscription.unsubscribe();
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

    const supabase = supabaseBrowser();
    const channel = supabase
      .channel(`global-community-chat-${user.id}`)
      .on(
        "postgres_changes",
        { event: "*", schema: "public", table: "global_chat_messages" },
        (payload) => {
          const next = payload.new as unknown as GlobalChatMessage;
          const previous = payload.old as unknown as Partial<GlobalChatMessage>;

          if (payload.eventType === "DELETE" && previous?.id) {
            const filtered = messagesRef.current.filter((message) => message.id !== previous.id);
            messagesRef.current = filtered;
            setMessages(filtered);
            return;
          }

          if (!next?.id) return;
          const merged = mergeGlobalChatMessages(messagesRef.current, [next]);
          messagesRef.current = merged;
          setMessages(merged);

          const isMine = next.author_user_id === user.id;
          if (payload.eventType === "INSERT" && isMine) {
            requestAnimationFrame(() => scrollToBottom(true));
          } else if (payload.eventType === "INSERT" && !next.deleted_at) {
            if (nearBottomRef.current) {
              requestAnimationFrame(() => scrollToBottom(true));
              void markReadRef.current(next.created_at);
            } else {
              setUnreadCount((count) => count + 1);
            }
          }
        },
      )
      .subscribe((status) => setRealtimeStatus(status));

    return () => {
      void supabase.removeChannel(channel);
    };
  }, [configured, scrollToBottom, user]);

  const handleScroll = () => {
    const element = scrollRef.current;
    if (!element) return;
    const nearBottom = element.scrollHeight - element.scrollTop - element.clientHeight < 120;
    nearBottomRef.current = nearBottom;
    setIsNearBottom(nearBottom);
    if (nearBottom && unreadCount > 0) {
      const last = messagesRef.current[messagesRef.current.length - 1];
      if (last) void markRead(last.created_at);
    }
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
      });
      if (sendError) throw sendError;
      const row = data as GlobalChatMessage;
      const merged = mergeGlobalChatMessages(messagesRef.current, [row]);
      messagesRef.current = merged;
      setMessages(merged);
      setDraft("");
      requestAnimationFrame(() => scrollToBottom(true));
      void markRead(row.created_at);
    } catch (reason) {
      setError(friendlyError(reason, "Unable to send your message."));
    } finally {
      setBusy(false);
    }
  };

  const onComposerKeyDown = (event: KeyboardEvent<HTMLTextAreaElement>) => {
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
      const row = data as GlobalChatMessage;
      const merged = mergeGlobalChatMessages(messagesRef.current, [row]);
      messagesRef.current = merged;
      setMessages(merged);
    } catch (reason) {
      setError(friendlyError(reason, "Unable to delete that message."));
    } finally {
      setBusy(false);
    }
  };

  if (checking) {
    return (
      <div role="status" className="grid min-h-[60vh] place-items-center bg-ink-950 text-xs text-text-muted">
        Checking your secure session…
      </div>
    );
  }

  if (!configured) {
    return (
      <main className="min-h-[65vh] bg-ink-950 px-4 py-20 text-text-primary">
        <section className="mx-auto max-w-xl rounded-3xl border border-state-warning/40 bg-state-warning/10 p-6 text-center shadow-panel sm:p-8">
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
      <main className="min-h-[65vh] bg-ink-950 px-4 py-20 text-text-primary">
        <section className="mx-auto max-w-xl rounded-3xl border border-line-strong bg-ink-900 p-6 text-center shadow-panel sm:p-8">
          <p className="eyebrow text-brand-300">Community chat</p>
          <h1 className="mt-3 text-2xl font-bold">Sign in to join the ProGlass community room</h1>
          <p className="mt-3 text-sm leading-relaxed text-text-muted">
            This is one live group room for signed-in ProGlass users. Sign in or create an account to read and post.
            Keep private project details, documents, and prices in your private inquiry chat.
          </p>
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
    <main className="min-h-screen bg-ink-950 pb-20 text-text-primary">
      <div className="shell py-8">
        <div className="flex flex-wrap items-end justify-between gap-4">
          <div className="max-w-3xl">
            <p className="eyebrow text-brand-300">Community</p>
            <h1 className="mt-2 text-3xl font-bold tracking-tight">ProGlass Community Chat</h1>
            <p className="mt-2 text-sm leading-relaxed text-text-muted">
              One live, WhatsApp-style group room for signed-in users to chat normally. Private inquiry work stays in
              My inquiries &amp; chat.
            </p>
            <p className="mt-2 text-xs text-text-faint">
              Signed in as {user.displayName}
              {user.email ? ` · ${user.email}` : ""}
              {isModerator ? " · Community moderator" : ""}
            </p>
          </div>
          <div className="flex flex-wrap gap-2">
            <button
              type="button"
              onClick={() => void loadChat()}
              className="rounded-xl border border-line-strong bg-ink-900 px-4 py-2.5 text-xs font-bold text-brand-300"
            >
              Refresh
            </button>
            <LocaleLink
              href="/inquiries/chat"
              className="rounded-xl border border-line-strong bg-ink-900 px-4 py-2.5 text-xs font-bold text-text-secondary"
            >
              Private inquiry chat
            </LocaleLink>
            <LocaleLink
              href="/inquiries/submit"
              className="rounded-xl bg-brand-400 px-4 py-2.5 text-xs font-bold text-accent-ink"
            >
              Submit inquiry
            </LocaleLink>
          </div>
        </div>

        {error ? (
          <div role="alert" className="mt-5 rounded-2xl border border-state-danger/40 bg-state-danger/10 p-4 text-sm text-state-danger">
            {error}
          </div>
        ) : null}

        <div className="mt-6 grid items-start gap-5 lg:grid-cols-[320px_minmax(0,1fr)]">
          <aside aria-label="Community rooms" className="hidden space-y-4 lg:block">
            <section className="rounded-3xl border border-line-strong bg-ink-900 p-4 shadow-panel">
              <div className="flex items-center justify-between gap-3">
                <h2 className="text-sm font-bold">Chats</h2>
                <span className="rounded-full border border-emerald-400/50 bg-emerald-400/10 px-2 py-0.5 text-3xs font-bold uppercase text-emerald-200">
                  {realtimeStatus.toLowerCase()}
                </span>
              </div>

              <div className="mt-4 rounded-2xl border border-brand-400/70 bg-brand-400/10 p-3" aria-current="true">
                <div className="flex items-start gap-3">
                  <span className="grid h-11 w-11 shrink-0 place-items-center rounded-full bg-brand-400 text-sm font-black text-accent-ink">
                    PG
                  </span>
                  <span className="min-w-0 flex-1">
                    <span className="flex items-center justify-between gap-2">
                      <strong className="truncate text-sm">ProGlass Community</strong>
                      {latestMessage ? (
                        <time className="shrink-0 text-3xs text-text-faint">
                          {formatGlobalChatTime(latestMessage.created_at, locale)}
                        </time>
                      ) : null}
                    </span>
                    <span className="mt-1 line-clamp-2 block text-xs leading-relaxed text-text-muted">{latestPreview}</span>
                  </span>
                  {unreadCount > 0 ? (
                    <span className="grid h-6 min-w-6 shrink-0 place-items-center rounded-full bg-emerald-400 px-1.5 text-3xs font-black text-accent-ink">
                      {unreadCount > 99 ? "99+" : unreadCount}
                    </span>
                  ) : null}
                </div>
              </div>

              <dl className="mt-4 grid grid-cols-3 gap-2 text-center">
                <div className="rounded-2xl border border-line bg-ink-950/70 p-3">
                  <dt className="text-3xs uppercase tracking-wider text-text-faint">Loaded</dt>
                  <dd className="mt-1 text-lg font-bold">{messages.length}</dd>
                </div>
                <div className="rounded-2xl border border-line bg-ink-950/70 p-3">
                  <dt className="text-3xs uppercase tracking-wider text-text-faint">Unread</dt>
                  <dd className="mt-1 text-lg font-bold">{unreadCount}</dd>
                </div>
                <div className="rounded-2xl border border-line bg-ink-950/70 p-3">
                  <dt className="text-3xs uppercase tracking-wider text-text-faint">Room</dt>
                  <dd className="mt-1 text-lg font-bold">1</dd>
                </div>
              </dl>
            </section>

            <section className="rounded-3xl border border-line-strong bg-ink-900 p-5 shadow-panel">
              <h2 className="text-sm font-bold">Room guidelines</h2>
              <ul className="mt-3 list-disc space-y-2 pl-5 text-xs leading-relaxed text-text-muted">
                <li>Every signed-in ProGlass user can read this room.</li>
                <li>Do not post private project details, documents, prices, phone numbers, or customer data.</li>
                <li>Use My inquiries &amp; chat for quotations, drawings, files, and account-specific help.</li>
                <li>Admins can delete messages that break these rules.</li>
              </ul>
            </section>
          </aside>

          <section
            aria-label="ProGlass community chat room"
            className="overflow-hidden rounded-3xl border border-line-strong bg-ink-900 shadow-panel"
          >
            <header className="flex items-center gap-3 border-b border-line bg-ink-900 px-4 py-3 sm:px-5">
              <span className="grid h-11 w-11 shrink-0 place-items-center rounded-full bg-brand-400 text-sm font-black text-accent-ink">
                PG
              </span>
              <div className="min-w-0 flex-1">
                <h2 className="truncate text-base font-bold">ProGlass Community</h2>
                <p className="mt-0.5 flex items-center gap-1.5 text-3xs text-text-faint">
                  <span
                    aria-hidden="true"
                    className={`h-2 w-2 rounded-full ${realtimeStatus === "SUBSCRIBED" ? "bg-emerald-400" : "bg-state-warning"}`}
                  />
                  Signed-in group room · realtime {realtimeStatus.toLowerCase()}
                  {isNearBottom ? " · viewing latest" : " · reading history"}
                </p>
              </div>
              {unreadCount > 0 ? (
                <span className="rounded-full bg-emerald-400 px-2.5 py-1 text-3xs font-black text-accent-ink">
                  {unreadCount} unread
                </span>
              ) : null}
            </header>

            <div className="border-b border-line bg-brand-400/10 px-4 py-3 text-xs leading-relaxed text-brand-100 sm:px-5">
              This room is visible to all signed-in ProGlass users. Please do not share private project details,
              documents, prices, or personal contact information here — use{" "}
              <LocaleLink href="/inquiries/chat" className="font-bold text-brand-200 underline underline-offset-4">
                My inquiries &amp; chat
              </LocaleLink>{" "}
              for private work.
            </div>

            <div className="relative">
              <div
                ref={scrollRef}
                onScroll={handleScroll}
                role="log"
                aria-live="polite"
                aria-label="Community messages"
                className="h-[58vh] space-y-4 overflow-y-auto scroll-smooth bg-ink-950/70 px-3 py-4 sm:px-5 lg:h-[64vh]"
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
                          {!own ? (
                            <span
                              aria-hidden="true"
                              className={`grid h-8 w-8 shrink-0 place-items-center rounded-full border text-3xs font-black ${avatarClass(message.author_user_id)}`}
                            >
                              {authorInitials(message.author_display_name)}
                            </span>
                          ) : null}

                          <div className={`flex max-w-[84%] flex-col sm:max-w-[72%] ${own ? "items-end" : "items-start"}`}>
                            {showAuthor ? (
                              <div className="mb-1 px-1 text-3xs font-bold text-brand-200">
                                {message.author_display_name}
                              </div>
                            ) : null}
                            <article
                              className={`rounded-3xl px-3.5 py-2.5 shadow-panel ${
                                deleted
                                  ? "border border-line bg-ink-950/80 text-text-faint"
                                  : own
                                    ? "rounded-br-md bg-brand-400 text-accent-ink"
                                    : "rounded-bl-md border border-line-strong bg-ink-850 text-text-primary"
                              }`}
                            >
                              <p className={`whitespace-pre-wrap break-words text-sm leading-relaxed ${deleted ? "italic" : ""}`}>
                                {deleted ? "This message was deleted." : message.body}
                              </p>
                              <div
                                className={`mt-1 flex items-center justify-end text-[10px] font-semibold ${
                                  deleted ? "text-text-faint" : own ? "text-accent-ink/70" : "text-text-faint"
                                }`}
                              >
                                <time dateTime={message.created_at}>{formatGlobalChatTime(message.created_at, locale)}</time>
                              </div>
                            </article>
                            {canDelete ? (
                              <button
                                type="button"
                                disabled={busy}
                                onClick={() => void removeMessage(message)}
                                className="mt-1 px-1 text-3xs font-bold text-text-faint underline-offset-4 hover:text-state-danger hover:underline disabled:opacity-50"
                              >
                                Delete for everyone
                              </button>
                            ) : null}
                          </div>

                          {own ? (
                            <span
                              aria-hidden="true"
                              className="hidden h-8 w-8 shrink-0 place-items-center rounded-full border border-brand-400/50 bg-brand-400/15 text-3xs font-black text-brand-200 sm:grid"
                            >
                              {authorInitials(user.displayName)}
                            </span>
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

            <form onSubmit={(event) => void send(event)} className="border-t border-line bg-ink-900 p-3 sm:p-4">
              <div className="flex items-end gap-2">
                <label htmlFor="global-chat-composer" className="sr-only">
                  Message the ProGlass community
                </label>
                <textarea
                  id="global-chat-composer"
                  value={draft}
                  onChange={(event) => setDraft(event.target.value)}
                  onKeyDown={onComposerKeyDown}
                  rows={2}
                  placeholder="Message the community…"
                  className="min-h-12 flex-1 resize-none rounded-3xl border border-line-strong bg-ink-950 px-4 py-3 text-sm leading-relaxed text-text-primary placeholder:text-text-faint focus:border-brand-400 focus:outline-none focus:ring-2 focus:ring-brand-400/40"
                />
                <button
                  type="submit"
                  disabled={!canSend}
                  className="h-12 shrink-0 rounded-3xl bg-brand-400 px-5 text-sm font-black text-accent-ink transition-opacity disabled:cursor-not-allowed disabled:opacity-50"
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
      </div>
    </main>
  );
}

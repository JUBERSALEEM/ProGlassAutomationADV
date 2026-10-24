"use client";

import {
  useCallback,
  useEffect,
  useMemo,
  useRef,
  useState,
  type FormEvent,
} from "react";
import { useRouter, useSearchParams } from "next/navigation";
import type { User } from "@supabase/supabase-js";
import { ReportsAuthForm, safeAuthRedirect } from "@/components/reports/ReportsAuthForm";
import { LocaleLink } from "@/components/ui/LocaleLink";
import { customerInquiryChatPath } from "@/lib/customer-inquiry-chat";
import { globalChatDisplayName } from "@/lib/global-chat";
import { isSupabaseConfigured, supabaseBrowser } from "@/lib/supabase";
import { useWebsiteSession } from "@/lib/website-session";
import {
  AVATAR_COLOR_OPTIONS,
  AVATAR_ICON_OPTIONS,
  avatarColorClass,
  avatarSymbol,
  normalizeAvatarColor,
  normalizeAvatarIcon,
  normalizeUserProfile,
  profileFromMetadata,
} from "@/lib/user-profile";
import type { UserProfile } from "@/types/user-profile";

const PROFILE_SELECT = "user_id,display_name,avatar_icon,avatar_color,updated_at";
const inputClassName =
  "h-11 w-full rounded-xl border border-line-strong bg-ink-950 px-3.5 text-sm text-text-primary outline-none transition focus:border-brand-400 focus:ring-2 focus:ring-brand-400/30";
const labelClassName = "text-xs font-semibold uppercase tracking-[0.12em] text-text-secondary";

type AccountUser = {
  id: string;
  email: string | null;
  user_metadata: Record<string, unknown>;
};

function recordFrom(value: unknown): Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value)
    ? (value as Record<string, unknown>)
    : {};
}

function toAccountUser(user: User | null | undefined): AccountUser | null {
  if (!user || typeof user.id !== "string" || !user.id) return null;
  return {
    id: user.id,
    email: typeof user.email === "string" ? user.email : null,
    user_metadata: recordFrom(user.user_metadata),
  };
}

function cleanProfileName(value: string, fallback: string): string {
  return (value.replace(/\s+/g, " ").trim() || fallback || "ProGlass user").slice(0, 60);
}

function friendlyError(reason: unknown, fallback: string): string {
  if (reason instanceof Error && reason.message.trim()) return reason.message;
  const record = recordFrom(reason);
  return typeof record.message === "string" && record.message.trim() ? record.message : fallback;
}

function fallbackProfileFor(user: AccountUser): UserProfile {
  const displayName = globalChatDisplayName({ email: user.email, metadata: user.user_metadata });
  return profileFromMetadata({ userId: user.id, displayName, metadata: user.user_metadata });
}

function AccountAvatar({
  profile,
  sizeClassName = "h-14 w-14 text-xl",
}: {
  profile: UserProfile;
  sizeClassName?: string;
}) {
  return (
    <span
      aria-hidden="true"
      className={`grid shrink-0 place-items-center rounded-full border font-black shadow-panel ${sizeClassName} ${avatarColorClass(
        profile.avatar_color,
        profile.user_id,
      )}`}
    >
      {avatarSymbol(profile)}
    </span>
  );
}

function Benefit({ title, description }: { title: string; description: string }) {
  return (
    <li className="rounded-2xl border border-line bg-ink-900/70 p-4">
      <p className="text-sm font-bold text-text-primary">{title}</p>
      <p className="mt-1 text-xs leading-relaxed text-text-muted">{description}</p>
    </li>
  );
}

function QuickLink({
  href,
  title,
  description,
}: {
  href: string;
  title: string;
  description: string;
}) {
  return (
    <LocaleLink
      href={href}
      className="group block rounded-2xl border border-line bg-ink-950/60 p-4 transition hover:border-brand-400/60 hover:bg-brand-500/10"
    >
      <span className="flex items-center justify-between gap-3 text-sm font-bold text-text-primary">
        {title}
        <span aria-hidden="true" className="text-brand-300 transition-transform group-hover:translate-x-0.5">→</span>
      </span>
      <span className="mt-1 block text-xs leading-relaxed text-text-muted">{description}</span>
    </LocaleLink>
  );
}

export function CustomerAccountWorkspace() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const configured = isSupabaseConfigured();
  const requestedRedirect = searchParams.get("redirect");
  const redirectTo = safeAuthRedirect(requestedRedirect, "/account");
  const hasExplicitRedirect = Boolean(requestedRedirect && redirectTo !== "/account");

  const websiteSession = useWebsiteSession({ enabled: configured });
  const checking = configured ? websiteSession.checking : false;
  const user = useMemo(() => toAccountUser(websiteSession.user), [websiteSession.user]);
  const [profile, setProfile] = useState<UserProfile | null>(null);
  const [profileLoading, setProfileLoading] = useState(false);
  const [displayName, setDisplayName] = useState("");
  const [selectedIcon, setSelectedIcon] = useState("initials");
  const [selectedColor, setSelectedColor] = useState("brand");
  const [savingProfile, setSavingProfile] = useState(false);
  const [profileError, setProfileError] = useState("");
  const [profileNotice, setProfileNotice] = useState("");
  const [authError, setAuthError] = useState("");
  const visibleAuthError = authError || (!user ? websiteSession.error ?? "" : "");
  const [newPassword, setNewPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [savingPassword, setSavingPassword] = useState(false);
  const [passwordError, setPasswordError] = useState("");
  const [passwordNotice, setPasswordNotice] = useState("");
  const [signingOut, setSigningOut] = useState(false);
  const [stayOnAccount, setStayOnAccount] = useState(false);
  const activeUserIdRef = useRef<string | null>(null);

  const syncProfileForm = useCallback((nextProfile: UserProfile) => {
    setProfile(nextProfile);
    setDisplayName(nextProfile.display_name);
    setSelectedIcon(normalizeAvatarIcon(nextProfile.avatar_icon));
    setSelectedColor(normalizeAvatarColor(nextProfile.avatar_color, nextProfile.user_id));
  }, []);

  const loadProfile = useCallback(
    async (accountUser: AccountUser) => {
      const fallback = fallbackProfileFor(accountUser);
      setProfileLoading(true);
      setProfileError("");
      try {
        const { data, error } = await supabaseBrowser()
          .from("user_profiles")
          .select(PROFILE_SELECT)
          .eq("user_id", accountUser.id)
          .maybeSingle();
        if (error) throw error;
        if (activeUserIdRef.current !== accountUser.id) return;
        syncProfileForm(
          normalizeUserProfile(data, { userId: accountUser.id, displayName: fallback.display_name }) ?? fallback,
        );
      } catch (reason) {
        if (activeUserIdRef.current !== accountUser.id) return;
        syncProfileForm(fallback);
        setProfileError(
          friendlyError(reason, "Your saved chat profile could not be loaded. Your initials are shown for now."),
        );
      } finally {
        if (activeUserIdRef.current === accountUser.id) setProfileLoading(false);
      }
    },
    [syncProfileForm],
  );

  useEffect(() => {
    if (!configured) return;
    const nextUserId = user?.id ?? null;
    if (activeUserIdRef.current === nextUserId) return;
    activeUserIdRef.current = nextUserId;
    queueMicrotask(() => {
      setProfile(null);
      setDisplayName("");
      setSelectedIcon("initials");
      setSelectedColor("brand");
      setProfileError("");
      setProfileNotice("");
      setPasswordError("");
      setPasswordNotice("");
    });
  }, [configured, user?.id]);

  useEffect(() => {
    if (!user) return;
    queueMicrotask(() => {
      void loadProfile(user);
    });
  }, [loadProfile, user]);

  useEffect(() => {
    if (!user || !hasExplicitRedirect || stayOnAccount) return;
    // A redirect query means this page was used as an authentication gate.
    // If the browser already has a saved session, honor that destination
    // automatically instead of asking the customer to sign in again.
    const timer = window.setTimeout(() => router.replace(redirectTo), 650);
    return () => window.clearTimeout(timer);
  }, [hasExplicitRedirect, redirectTo, router, stayOnAccount, user]);

  const currentProfile = useMemo<UserProfile | null>(() => {
    if (!user) return null;
    return profile ?? fallbackProfileFor(user);
  }, [profile, user]);

  const previewProfile = useMemo<UserProfile | null>(() => {
    if (!user || !currentProfile) return null;
    return {
      user_id: user.id,
      display_name: cleanProfileName(displayName, currentProfile.display_name),
      avatar_icon: normalizeAvatarIcon(selectedIcon),
      avatar_color: normalizeAvatarColor(selectedColor, user.id),
    };
  }, [currentProfile, displayName, selectedColor, selectedIcon, user]);

  const profileDirty = Boolean(
    currentProfile &&
      previewProfile &&
      (previewProfile.display_name !== currentProfile.display_name ||
        previewProfile.avatar_icon !== currentProfile.avatar_icon ||
        previewProfile.avatar_color !== currentProfile.avatar_color),
  );

  const selectedIconLabel =
    AVATAR_ICON_OPTIONS.find((option) => option.id === normalizeAvatarIcon(selectedIcon))?.label ?? "My initials";
  const selectedColorLabel =
    AVATAR_COLOR_OPTIONS.find((option) => option.id === normalizeAvatarColor(selectedColor, user?.id))?.label ??
    "ProGlass";

  const saveProfile = async (event: FormEvent) => {
    event.preventDefault();
    if (!user || !previewProfile) return;
    setSavingProfile(true);
    setProfileError("");
    setProfileNotice("");
    try {
      const supabase = supabaseBrowser();
      const { data, error } = await supabase
        .from("user_profiles")
        .upsert(previewProfile, { onConflict: "user_id" })
        .select(PROFILE_SELECT)
        .single();
      if (error) throw error;
      const saved =
        normalizeUserProfile(data, { userId: user.id, displayName: previewProfile.display_name }) ?? previewProfile;
      syncProfileForm(saved);

      const { error: updateError } = await supabase.auth.updateUser({
        data: {
          ...user.user_metadata,
          full_name: saved.display_name,
          name: saved.display_name,
          avatar_icon: saved.avatar_icon,
          avatar_color: saved.avatar_color,
        },
      });
      if (updateError) {
        setProfileNotice("Profile saved for chat. Your sign-in details will refresh the next time you sign in.");
      } else {
        setProfileNotice("Profile saved. Your name and icon now update live in community chat.");
      }
      router.refresh();
    } catch (reason) {
      setProfileError(friendlyError(reason, "Your profile could not be saved. Please try again."));
    } finally {
      setSavingProfile(false);
    }
  };

  const resetProfile = () => {
    if (currentProfile) syncProfileForm(currentProfile);
    setProfileError("");
    setProfileNotice("");
  };

  const savePassword = async (event: FormEvent) => {
    event.preventDefault();
    setPasswordError("");
    setPasswordNotice("");
    if (newPassword.length < 8) {
      setPasswordError("Use at least 8 characters for your password.");
      return;
    }
    if (newPassword !== confirmPassword) {
      setPasswordError("The two passwords do not match.");
      return;
    }
    setSavingPassword(true);
    try {
      const { error } = await supabaseBrowser().auth.updateUser({ password: newPassword });
      if (error) throw error;
      setNewPassword("");
      setConfirmPassword("");
      setPasswordNotice("Password updated. This device stays signed in, and the new password works for future sign-ins.");
    } catch (reason) {
      setPasswordError(friendlyError(reason, "Your password could not be updated."));
    } finally {
      setSavingPassword(false);
    }
  };

  const signOut = async () => {
    setSigningOut(true);
    setAuthError("");
    try {
      const { error } = await supabaseBrowser().auth.signOut();
      if (error) throw error;
      activeUserIdRef.current = null;
      setProfile(null);
      router.replace("/account");
      router.refresh();
    } catch (reason) {
      setAuthError(friendlyError(reason, "Sign-out could not be completed. Please try again."));
    } finally {
      setSigningOut(false);
    }
  };

  if (checking) {
    return (
      <div className="shell grid min-h-[520px] w-full place-items-center py-14">
        <div className="w-full max-w-md rounded-[28px] border border-line-strong bg-ink-900 p-8 text-center shadow-panel">
          <div className="mx-auto h-12 w-12 animate-spin rounded-full border-2 border-brand-400 border-t-transparent" />
          <h1 className="mt-5 text-xl font-bold">Checking your saved sign-in…</h1>
          <p className="mt-2 text-sm leading-relaxed text-text-muted">
            ProGlass keeps this browser signed in securely, so returning to your account should only take a moment.
          </p>
        </div>
      </div>
    );
  }

  if (configured && websiteSession.unavailable && !websiteSession.user) {
    return (
      <div className="shell grid min-h-[520px] w-full place-items-center py-14">
        <section className="w-full max-w-md rounded-[28px] border border-state-warning/40 bg-ink-900 p-8 text-center shadow-panel">
          <p className="eyebrow text-state-warning">Saved sign-in</p>
          <h1 className="mt-3 text-2xl font-black">We could not confirm your saved sign-in yet</h1>
          <p className="mt-3 text-sm leading-relaxed text-text-muted">
            {websiteSession.error ?? "The authentication service is temporarily unreachable. Your saved sign-in has not been discarded."}
          </p>
          <button
            type="button"
            onClick={() => void websiteSession.refreshSession()}
            className="mt-6 inline-flex h-11 items-center justify-center rounded-xl bg-brand-400 px-5 text-sm font-bold text-accent-ink"
          >
            Retry saved sign-in
          </button>
        </section>
      </div>
    );
  }

  if (!user || !previewProfile || !currentProfile) {
    return (
      <div className="shell grid w-full items-center gap-10 py-12 lg:grid-cols-[minmax(0,1fr)_480px] lg:py-20">
        <section aria-labelledby="account-intro" className="max-w-2xl">
          <p className="eyebrow text-brand-300">Customer account</p>
          <h2 id="account-intro" className="mt-3 text-4xl font-black leading-tight tracking-tight sm:text-5xl">
            One saved sign-in for your projects and chats.
          </h2>
          <p className="mt-4 max-w-xl text-base leading-relaxed text-text-secondary">
            Sign in once on this device. Your secure ProGlass session renews automatically, so you can return to private
            inquiry chats and the community room without entering your password every visit.
          </p>
          {visibleAuthError ? (
            <p role="alert" className="mt-5 rounded-2xl border border-state-danger/40 bg-state-danger/10 p-4 text-sm text-state-danger">
              {visibleAuthError}
            </p>
          ) : null}
          <ul className="mt-7 grid gap-3 sm:grid-cols-2">
            <Benefit title="Saved secure session" description="Your sign-in stays active on this browser until you sign out or clear browser data." />
            <Benefit title="Private inquiry chat" description="Follow status, documents, and messages only on inquiries that belong to you." />
            <Benefit title="Community chat profile" description="After sign-in, choose the name, icon, and colour other customers see in chat." />
            <Benefit title="Password or email code" description="Use your saved password, or verify by email and set a password for next time." />
          </ul>
        </section>
        <ReportsAuthForm variant="account" defaultRedirect="/account" />
      </div>
    );
  }

  if (hasExplicitRedirect && !stayOnAccount) {
    return (
      <div className="shell grid min-h-[520px] w-full place-items-center py-14">
        <section className="w-full max-w-md rounded-[28px] border border-line-strong bg-ink-900 p-8 text-center shadow-panel">
          <div className="flex justify-center"><AccountAvatar profile={previewProfile} sizeClassName="h-16 w-16 text-2xl" /></div>
          <p className="eyebrow mt-5 text-brand-300">Saved sign-in found</p>
          <h1 className="mt-2 text-2xl font-black">Welcome back, {previewProfile.display_name}</h1>
          <p className="mt-3 text-sm leading-relaxed text-text-muted">
            You are already signed in as {user.email ?? "your ProGlass account"}. Continuing to your requested page…
          </p>
          <div className="mt-6 flex flex-col gap-2 sm:flex-row sm:justify-center">
            <LocaleLink href={redirectTo} className="inline-flex h-11 items-center justify-center rounded-xl bg-brand-400 px-4 text-sm font-bold text-accent-ink">
              Continue now
            </LocaleLink>
            <button type="button" onClick={() => setStayOnAccount(true)} className="inline-flex h-11 items-center justify-center rounded-xl border border-line-strong px-4 text-sm font-semibold">
              Manage account instead
            </button>
          </div>
        </section>
      </div>
    );
  }

  return (
    <div className="shell w-full py-8 sm:py-12">
      {hasExplicitRedirect ? (
        <section className="mb-5 flex flex-col gap-4 rounded-[24px] border border-brand-400/40 bg-brand-500/10 p-5 sm:flex-row sm:items-center sm:justify-between">
          <div>
            <h2 className="text-base font-bold text-brand-100">You are already signed in</h2>
            <p className="mt-1 text-sm text-brand-100/75">Continue to the page you requested, or stay here to manage your profile.</p>
          </div>
          <div className="flex flex-wrap gap-2">
            <LocaleLink href={redirectTo} className="inline-flex h-10 items-center rounded-xl bg-brand-400 px-4 text-sm font-bold text-accent-ink">
              Continue
            </LocaleLink>
            <LocaleLink href="/account" className="inline-flex h-10 items-center rounded-xl border border-brand-300/40 px-4 text-sm font-semibold text-brand-100">
              Stay on account
            </LocaleLink>
          </div>
        </section>
      ) : null}

      <header className="overflow-hidden rounded-[28px] border border-line-strong bg-ink-900 shadow-panel">
        <div className="bg-gradient-to-r from-brand-500/25 via-ink-900 to-ink-900 p-6 sm:p-8">
          <div className="flex flex-col gap-6 lg:flex-row lg:items-center lg:justify-between">
            <div className="flex min-w-0 items-center gap-4">
              <AccountAvatar profile={previewProfile} sizeClassName="h-20 w-20 text-3xl" />
              <div className="min-w-0">
                <p className="eyebrow text-brand-300">Signed in securely</p>
                <h1 className="mt-1 truncate text-3xl font-black tracking-tight">{previewProfile.display_name}</h1>
                <p className="mt-1 truncate font-mono text-sm text-text-muted">{user.email ?? "Email not shown"}</p>
                <div className="mt-3 flex flex-wrap gap-2 text-[11px] font-bold">
                  <span className="rounded-full border border-state-success/40 bg-state-success/10 px-2.5 py-1 text-state-success">✓ Saved sign-in active</span>
                  <span className="rounded-full border border-brand-400/40 bg-brand-500/10 px-2.5 py-1 text-brand-100">{selectedIconLabel} icon · {selectedColorLabel}</span>
                </div>
              </div>
            </div>
            <div className="flex flex-wrap gap-2">
              <LocaleLink href="/chat" className="inline-flex h-11 items-center rounded-xl bg-brand-400 px-4 text-sm font-bold text-accent-ink">
                Open community chat
              </LocaleLink>
              <LocaleLink href={customerInquiryChatPath()} className="inline-flex h-11 items-center rounded-xl border border-line-strong px-4 text-sm font-semibold">
                My inquiry chats
              </LocaleLink>
              <button type="button" disabled={signingOut} onClick={() => void signOut()} className="inline-flex h-11 items-center rounded-xl border border-state-danger/40 px-4 text-sm font-semibold text-state-danger disabled:opacity-50">
                {signingOut ? "Signing out…" : "Sign out"}
              </button>
            </div>
          </div>
        </div>
      </header>

      {visibleAuthError ? (
        <p role="alert" className="mt-5 rounded-2xl border border-state-danger/40 bg-state-danger/10 p-4 text-sm text-state-danger">{visibleAuthError}</p>
      ) : null}

      <div className="mt-6 grid gap-6 lg:grid-cols-[minmax(0,1fr)_350px]">
        <div className="space-y-6">
          <section className="rounded-[28px] border border-line-strong bg-ink-900 p-6 shadow-panel sm:p-7" aria-labelledby="profile-heading">
            <div className="flex flex-wrap items-start justify-between gap-4">
              <div>
                <p className="eyebrow text-brand-300">Profile & avatar</p>
                <h2 id="profile-heading" className="mt-1 text-2xl font-black">Your WhatsApp-style chat profile</h2>
                <p className="mt-2 max-w-2xl text-sm leading-relaxed text-text-muted">
                  Choose how you appear in the ProGlass community room and in Read by / Delivered to lists. Only your
                  display name, icon, and colour are shared there—your email stays private.
                </p>
              </div>
              <div className="flex items-center gap-3 rounded-2xl border border-line bg-ink-950/70 px-4 py-3">
                <AccountAvatar profile={previewProfile} />
                <div>
                  <p className="text-sm font-bold">{previewProfile.display_name}</p>
                  <p className="text-xs text-text-muted">Chat preview</p>
                </div>
              </div>
            </div>

            <form onSubmit={(event) => void saveProfile(event)} className="mt-7 space-y-7">
              <div>
                <label htmlFor="account-display-name" className={labelClassName}>Display name</label>
                <input
                  id="account-display-name"
                  name="display-name"
                  autoComplete="name"
                  maxLength={60}
                  value={displayName}
                  onChange={(event) => setDisplayName(event.target.value)}
                  className={`${inputClassName} mt-2`}
                  placeholder="Your name in community chat"
                />
                <p className="mt-2 text-xs text-text-faint">{displayName.trim().length}/60 characters</p>
              </div>

              <fieldset>
                <legend className={labelClassName}>Choose your profile icon</legend>
                <div className="mt-3 grid grid-cols-3 gap-2 sm:grid-cols-4 lg:grid-cols-5">
                  {AVATAR_ICON_OPTIONS.map((option) => {
                    const selected = normalizeAvatarIcon(selectedIcon) === option.id;
                    const optionProfile = { ...previewProfile, avatar_icon: option.id };
                    return (
                      <button
                        key={option.id}
                        type="button"
                        aria-pressed={selected}
                        onClick={() => setSelectedIcon(option.id)}
                        className={`rounded-2xl border p-3 text-center transition ${
                          selected
                            ? "border-brand-300 bg-brand-500/20 ring-2 ring-brand-400/40"
                            : "border-line bg-ink-950/60 hover:border-brand-400/50"
                        }`}
                      >
                        <span className={`mx-auto grid h-11 w-11 place-items-center rounded-full border text-base font-black ${avatarColorClass(selectedColor, user.id)}`}>
                          {avatarSymbol(optionProfile)}
                        </span>
                        <span className="mt-2 block text-xs font-semibold">{option.label}</span>
                        {selected ? <span className="mt-1 block text-[10px] font-bold text-brand-200">Selected</span> : null}
                      </button>
                    );
                  })}
                </div>
              </fieldset>

              <fieldset>
                <legend className={labelClassName}>Avatar colour</legend>
                <div className="mt-3 flex flex-wrap gap-2">
                  {AVATAR_COLOR_OPTIONS.map((option) => {
                    const selected = normalizeAvatarColor(selectedColor, user.id) === option.id;
                    return (
                      <button
                        key={option.id}
                        type="button"
                        aria-pressed={selected}
                        onClick={() => setSelectedColor(option.id)}
                        className={`inline-flex items-center gap-2 rounded-full border px-3 py-2 text-xs font-bold transition ${
                          selected ? "border-brand-300 bg-brand-500/20 ring-2 ring-brand-400/30" : "border-line bg-ink-950/60 hover:border-brand-400/50"
                        }`}
                      >
                        <span aria-hidden="true" className={`h-4 w-4 rounded-full ${option.swatchClassName}`} />
                        {option.label}
                        {selected ? <span aria-hidden="true">✓</span> : null}
                      </button>
                    );
                  })}
                </div>
              </fieldset>

              {profileError ? <p role="alert" className="rounded-2xl border border-state-danger/40 bg-state-danger/10 p-3 text-sm text-state-danger">{profileError}</p> : null}
              {profileNotice ? <p role="status" className="rounded-2xl border border-state-success/40 bg-state-success/10 p-3 text-sm text-state-success">{profileNotice}</p> : null}

              <div className="flex flex-wrap items-center gap-3">
                <button type="submit" disabled={!profileDirty || savingProfile || profileLoading} className="inline-flex h-11 items-center rounded-xl bg-brand-400 px-5 text-sm font-bold text-accent-ink disabled:cursor-not-allowed disabled:opacity-45">
                  {savingProfile ? "Saving profile…" : profileLoading ? "Loading profile…" : "Save profile"}
                </button>
                {profileDirty ? (
                  <button type="button" onClick={resetProfile} className="inline-flex h-11 items-center rounded-xl border border-line-strong px-4 text-sm font-semibold">
                    Reset changes
                  </button>
                ) : (
                  <span className="text-xs text-text-faint">All profile changes are saved.</span>
                )}
              </div>
            </form>
          </section>

          <section className="rounded-[28px] border border-line-strong bg-ink-900 p-6 shadow-panel sm:p-7" aria-labelledby="security-heading">
            <p className="eyebrow text-brand-300">Security</p>
            <h2 id="security-heading" className="mt-1 text-2xl font-black">Password & saved sign-in</h2>
            <div className="mt-4 rounded-2xl border border-state-success/30 bg-state-success/10 p-4 text-sm leading-relaxed text-state-success">
              This browser is signed in and renews automatically. You do not need to log in again on every visit. On a
              shared computer, use <strong>Sign out</strong> when finished.
            </div>
            <form onSubmit={(event) => void savePassword(event)} className="mt-6 grid gap-4 sm:grid-cols-2">
              <div>
                <label htmlFor="account-new-password" className={labelClassName}>New password</label>
                <input id="account-new-password" name="new-password" type="password" autoComplete="new-password" minLength={8} value={newPassword} onChange={(event) => setNewPassword(event.target.value)} className={`${inputClassName} mt-2`} />
              </div>
              <div>
                <label htmlFor="account-confirm-password" className={labelClassName}>Confirm password</label>
                <input id="account-confirm-password" name="confirm-password" type="password" autoComplete="new-password" minLength={8} value={confirmPassword} onChange={(event) => setConfirmPassword(event.target.value)} className={`${inputClassName} mt-2`} />
              </div>
              <div className="sm:col-span-2">
                {passwordError ? <p role="alert" className="mb-3 rounded-2xl border border-state-danger/40 bg-state-danger/10 p-3 text-sm text-state-danger">{passwordError}</p> : null}
                {passwordNotice ? <p role="status" className="mb-3 rounded-2xl border border-state-success/40 bg-state-success/10 p-3 text-sm text-state-success">{passwordNotice}</p> : null}
                <button type="submit" disabled={savingPassword || !newPassword || !confirmPassword} className="inline-flex h-11 items-center rounded-xl border border-brand-400/50 bg-brand-500/10 px-5 text-sm font-bold text-brand-100 disabled:cursor-not-allowed disabled:opacity-45">
                  {savingPassword ? "Updating password…" : "Update password"}
                </button>
              </div>
            </form>
          </section>
        </div>

        <aside className="space-y-6">
          <section className="rounded-[28px] border border-line-strong bg-ink-900 p-5 shadow-panel" aria-labelledby="quick-access-heading">
            <p className="eyebrow text-brand-300">Quick access</p>
            <h2 id="quick-access-heading" className="mt-1 text-xl font-black">Continue working</h2>
            <div className="mt-4 space-y-3">
              <QuickLink href="/chat" title="Community chat" description="Compact live room with replies, delivery ticks, Read by, and Delivered to." />
              <QuickLink href={customerInquiryChatPath()} title="My inquiry chats" description="Private conversations and status for inquiries that belong to your account." />
              <QuickLink href="/account/proformas" title="Proforma invoices" description="The proforma invoices shared with your account, with the figures that were shared." />
              <QuickLink href="/inquiries/submit" title="Submit a new inquiry" description="Send project documents and details for a quotation or technical review." />
            </div>
          </section>

          <section className="rounded-[28px] border border-line-strong bg-ink-900 p-5 shadow-panel" aria-labelledby="session-heading">
            <p className="eyebrow text-brand-300">Session & privacy</p>
            <h2 id="session-heading" className="mt-1 text-xl font-black">Saved on this device</h2>
            <ul className="mt-4 space-y-3 text-sm leading-relaxed text-text-muted">
              <li className="rounded-2xl border border-line bg-ink-950/60 p-3"><strong className="text-text-primary">Account:</strong> <span className="break-all font-mono text-xs">{user.email ?? "Signed-in user"}</span></li>
              <li className="rounded-2xl border border-line bg-ink-950/60 p-3">The secure session automatically renews while this browser keeps its stored sign-in data.</li>
              <li className="rounded-2xl border border-line bg-ink-950/60 p-3">Community chat shows your display name and icon only. Inquiry documents and private chats remain account-protected.</li>
            </ul>
            <button type="button" disabled={signingOut} onClick={() => void signOut()} className="mt-4 inline-flex h-10 w-full items-center justify-center rounded-xl border border-state-danger/40 px-4 text-sm font-semibold text-state-danger disabled:opacity-50">
              {signingOut ? "Signing out…" : "Sign out of this browser"}
            </button>
          </section>
        </aside>
      </div>
    </div>
  );
}

"use client";

import Link from "next/link";
import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { site } from "@/lib/site";
import { useT, useHref, useLocale } from "@/i18n/provider";
import { Icon } from "@/components/ui/Icon";
import { InquiryCommunicationPanel } from "@/components/inquiries/InquiryCommunicationPanel";
import {
  accountPathForRedirect,
  countCustomerInquiriesByStatus,
  customerInquiryChatPath,
  customerInquiryDetailPath,
  customerInquiryStatusLabel,
  customerInquiryTypeLabel,
  formatCustomerInquiryDate,
  inquiryCommunicationStats,
  type InquiryCommunicationStats,
} from "@/lib/customer-inquiry-chat";
import { useCustomerInquiries } from "@/lib/useCustomerInquiries";
import { useWebsiteSession } from "@/lib/website-session";
import type { InquiryCommunication } from "@/types/inquiry-communications";

/**
 * The global support bubble.
 *
 * Customer inquiry chat is account-bound: a signed-in customer sees only the
 * completed inquiries returned for their own token and messages are posted to
 * the selected submission ID through the existing Phase 9D communication API.
 * WhatsApp, email, and the demo form remain available as general contact
 * channels, but they are deliberately secondary to the private inquiry thread.
 */
const OPEN_EVENT = "proglass:open-support";
const EMPTY_COMMUNICATION_STATS: InquiryCommunicationStats = {
  messageCount: 0,
  openInformationRequests: 0,
  lastActivityAt: null,
};

function MiniStat({ label, value }: { label: string; value: string | number }) {
  return (
    <div className="rounded-card border border-line-strong/60 bg-ink-950/70 px-2.5 py-2 text-center">
      <div className="text-base font-bold leading-none text-text-primary">{value}</div>
      <div className="mt-1 text-3xs font-mono uppercase tracking-wide text-text-faint">{label}</div>
    </div>
  );
}

export function SupportBubble() {
  const t = useT();
  const href = useHref();
  const locale = useLocale();
  const [open, setOpen] = useState(false);
  const websiteSession = useWebsiteSession({ enabled: open });
  const sessionChecked = !websiteSession.checking;
  const authToken = websiteSession.accessToken;
  const userEmail = websiteSession.userEmail;
  const [selectedInquiryId, setSelectedInquiryId] = useState("");
  const [communicationStatsState, setCommunicationStatsState] = useState<{
    inquiryId: string;
    stats: InquiryCommunicationStats;
  } | null>(null);
  const rootRef = useRef<HTMLDivElement>(null);
  const triggerRef = useRef<HTMLButtonElement>(null);
  const wasOpenRef = useRef(false);

  useEffect(() => {
    const show = () => setOpen(true);
    window.addEventListener(OPEN_EVENT, show);
    return () => window.removeEventListener(OPEN_EVENT, show);
  }, []);

  useEffect(() => {
    if (!open) {
      if (wasOpenRef.current) {
        wasOpenRef.current = false;
        triggerRef.current?.focus();
      }
      return;
    }
    wasOpenRef.current = true;
    const onKey = (event: KeyboardEvent) => {
      if (event.key === "Escape") setOpen(false);
    };
    const onPointer = (event: MouseEvent | TouchEvent) => {
      if (!rootRef.current?.contains(event.target as Node)) setOpen(false);
    };
    document.addEventListener("keydown", onKey);
    document.addEventListener("mousedown", onPointer);
    document.addEventListener("touchstart", onPointer);
    rootRef.current?.querySelector<HTMLElement>("a, button")?.focus();
    return () => {
      document.removeEventListener("keydown", onKey);
      document.removeEventListener("mousedown", onPointer);
      document.removeEventListener("touchstart", onPointer);
    };
  }, [open]);

  const handleUnauthorized = websiteSession.requireAuth;

  const {
    inquiries,
    loading: inquiriesLoading,
    error: inquiriesError,
    refresh: refreshInquiries,
  } = useCustomerInquiries(authToken, {
    enabled: open && Boolean(authToken),
    onUnauthorized: handleUnauthorized,
  });

  const selectedInquiry = useMemo(
    () => inquiries.find((inquiry) => inquiry.id === selectedInquiryId) ?? inquiries[0] ?? null,
    [inquiries, selectedInquiryId],
  );
  const counts = useMemo(() => countCustomerInquiriesByStatus(inquiries), [inquiries]);
  const communicationStats =
    communicationStatsState && communicationStatsState.inquiryId === selectedInquiry?.id
      ? communicationStatsState.stats
      : EMPTY_COMMUNICATION_STATS;

  const handleCommunicationsChanged = useCallback(
    (rows: InquiryCommunication[]) => {
      const inquiryId = rows[0]?.submission_id ?? selectedInquiry?.id;
      if (!inquiryId) return;
      setCommunicationStatsState({ inquiryId, stats: inquiryCommunicationStats(rows) });
    },
    [selectedInquiry?.id],
  );

  const whatsappHref = site.whatsappNumber
    ? `https://wa.me/${site.whatsappNumber}?text=${encodeURIComponent(
        "Hello — I am on the ProGlass Automation site and have a question.",
      )}`
    : "";

  const mailtoHref = `mailto:${site.developerEmail}?subject=${encodeURIComponent("Support — ProGlass Automation")}`;
  const signedIn = Boolean(authToken);

  return (
    <div ref={rootRef} className="no-print fixed bottom-[max(1.25rem,env(safe-area-inset-bottom))] left-[max(1.25rem,env(safe-area-inset-left))] z-40">
      {open ? (
        <div
          id="support-dialog"
          role="dialog"
          aria-label={t("Support")}
          className="menu-card animate-scale-in mb-3 max-h-[calc(100dvh-7rem)] w-[min(24rem,calc(100vw-2.5rem))] overflow-y-auto overscroll-contain rounded-panel border-2 border-line-strong/60 bg-ink-900/95 p-4 shadow-raised"
        >
          <div className="flex items-start justify-between gap-3">
            <div>
              <p className="text-sm font-bold text-text-primary">{t("Inquiry chat & support")}</p>
              <p className="mt-1 text-xs leading-relaxed text-text-muted">
                {t("Signed-in customers can chat privately on their own inquiry reference or join the signed-in community room for general discussion.")}
              </p>
            </div>
            <button
              type="button"
              onClick={() => setOpen(false)}
              aria-label={t("Close support panel")}
              className="grid h-7 w-7 shrink-0 place-items-center rounded-card border border-line-strong/60 text-text-muted transition-colors hover:border-brand-400/50 hover:text-text-primary"
            >
              <svg viewBox="0 0 14 14" className="h-3 w-3" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round">
                <path d="M3 3l8 8M11 3l-8 8" />
              </svg>
            </button>
          </div>

          <section aria-label={t("Community chat")} className="mt-4 rounded-card border-2 border-emerald-400/40 bg-emerald-500/10 p-3.5">
            <div className="flex items-start justify-between gap-3">
              <div>
                <h2 className="text-sm font-bold text-text-primary">{t("Community chat")}</h2>
                <p className="mt-1 text-xs leading-relaxed text-text-muted">
                  {t("One signed-in group room for normal general discussion. Keep private project details in inquiry chat.")}
                </p>
              </div>
              <span className="shrink-0 rounded-full border border-emerald-400/50 bg-emerald-400/10 px-2 py-0.5 text-3xs font-bold uppercase text-emerald-200">
                {t("Live")}
              </span>
            </div>
            <Link
              href={href("/chat")}
              onClick={() => setOpen(false)}
              className="mt-3 block rounded-card bg-emerald-400 px-3 py-2.5 text-center text-xs font-bold text-accent-ink"
            >
              {t("Open community chat")}
            </Link>
          </section>

          <section aria-label={t("Customer inquiry chat")} className="mt-4 rounded-card border-2 border-brand-400/40 bg-ink-950/60 p-3.5">
            {!sessionChecked ? (
              <p role="status" className="text-xs text-text-muted">{t("Checking your account…")}</p>
            ) : websiteSession.unavailable && !signedIn ? (
              <>
                <h2 className="text-sm font-bold text-text-primary">{t("Saved sign-in could not be confirmed")}</h2>
                <p className="mt-1 text-xs leading-relaxed text-text-muted">
                  {t("The authentication service is temporarily unreachable. Your saved sign-in has not been discarded.")}
                </p>
                <button
                  type="button"
                  onClick={() => void websiteSession.refreshSession()}
                  className="mt-3 block w-full rounded-card bg-brand-400 px-3 py-2.5 text-center text-xs font-bold text-accent-ink"
                >
                  {t("Retry saved sign-in")}
                </button>
              </>
            ) : !signedIn ? (
              <>
                <h2 className="text-sm font-bold text-text-primary">{t("Customer inquiry chat")}</h2>
                <p className="mt-1 text-xs leading-relaxed text-text-muted">
                  {t("Sign in or create one website account to submit inquiries, follow current status, and chat privately on your own inquiry.")}
                </p>
                <div className="mt-3 grid grid-cols-2 gap-2">
                  <Link
                    href={href(accountPathForRedirect("/inquiries/chat"))}
                    onClick={() => setOpen(false)}
                    className="rounded-card bg-brand-400 px-3 py-2.5 text-center text-xs font-bold text-accent-ink"
                  >
                    {t("Sign in")}
                  </Link>
                  <Link
                    href={href(accountPathForRedirect("/inquiries/chat", "signup"))}
                    onClick={() => setOpen(false)}
                    className="rounded-card border border-line-strong px-3 py-2.5 text-center text-xs font-bold text-brand-300"
                  >
                    {t("Create account")}
                  </Link>
                </div>
                <Link
                  href={href(accountPathForRedirect("/inquiries/submit"))}
                  onClick={() => setOpen(false)}
                  className="mt-2 block rounded-card border border-line-strong/70 px-3 py-2.5 text-center text-xs font-semibold text-text-secondary"
                >
                  {t("Start an inquiry after sign-in")}
                </Link>
              </>
            ) : (
              <>
                <div className="flex items-start justify-between gap-3">
                  <div>
                    <h2 className="text-sm font-bold text-text-primary">{t("Your inquiry chat")}</h2>
                    {userEmail ? <p className="mt-0.5 break-all text-3xs text-text-faint">{userEmail}</p> : null}
                  </div>
                  <button
                    type="button"
                    onClick={refreshInquiries}
                    className="shrink-0 rounded-card border border-line-strong px-2.5 py-1.5 text-3xs font-bold text-brand-300"
                  >
                    {t("Refresh")}
                  </button>
                </div>

                {inquiriesError ? (
                  <p role="alert" className="mt-3 text-xs leading-relaxed text-state-danger">{inquiriesError}</p>
                ) : inquiriesLoading && inquiries.length === 0 ? (
                  <p role="status" className="mt-3 text-xs text-text-muted">{t("Loading your inquiries…")}</p>
                ) : inquiries.length === 0 ? (
                  <>
                    <p className="mt-2 text-xs leading-relaxed text-text-muted">
                      {t("You have no completed inquiry yet. Submit one first; its reference will connect the status, documents, and chat.")}
                    </p>
                    <Link
                      href={href("/inquiries/submit")}
                      onClick={() => setOpen(false)}
                      className="mt-3 block rounded-card bg-brand-400 px-3 py-2.5 text-center text-xs font-bold text-accent-ink"
                    >
                      {t("Submit an inquiry")}
                    </Link>
                  </>
                ) : selectedInquiry ? (
                  <>
                    <div className="mt-3 grid grid-cols-4 gap-2">
                      <MiniStat label={t("Total")} value={counts.total} />
                      <MiniStat label={t("Submitted")} value={counts.submitted} />
                      <MiniStat label={t("Review")} value={counts.under_review} />
                      <MiniStat label={t("Accepted")} value={counts.acceptedOrDownloaded} />
                    </div>
                    <p className="mt-2 text-center text-3xs text-text-faint">
                      {t.template`${counts.closedOrRejected} closed/rejected · ${counts.active} active`}
                    </p>

                    <label htmlFor="support-inquiry-select" className="mt-3 block text-3xs font-mono uppercase tracking-wider text-text-faint">
                      {t("Chat about inquiry")}
                    </label>
                    <select
                      id="support-inquiry-select"
                      value={selectedInquiry.id}
                      onChange={(event) => setSelectedInquiryId(event.target.value)}
                      className="mt-1 h-10 w-full rounded-card border border-line-strong bg-ink-950 px-2 text-xs text-text-primary"
                    >
                      {inquiries.map((inquiry) => (
                        <option key={inquiry.id} value={inquiry.id}>
                          {inquiry.submission_reference} — {inquiry.project_name} ({customerInquiryStatusLabel(inquiry.status)})
                        </option>
                      ))}
                    </select>

                    <div className="mt-3 rounded-card border border-line-strong/60 bg-ink-900/70 p-3">
                      <div className="flex items-start justify-between gap-2">
                        <span className="font-mono text-xs font-bold text-brand-300">{selectedInquiry.submission_reference}</span>
                        <span className="rounded-full border border-brand-400/50 px-2 py-0.5 text-3xs font-bold uppercase text-brand-200">
                          {t(customerInquiryStatusLabel(selectedInquiry.status))}
                        </span>
                      </div>
                      <p className="mt-1 text-sm font-semibold text-text-primary">{selectedInquiry.project_name}</p>
                      <dl className="mt-3 grid grid-cols-2 gap-x-3 gap-y-2 text-3xs">
                        <div>
                          <dt className="text-text-faint">{t("Type")}</dt>
                          <dd className="mt-0.5 font-semibold text-text-secondary">{t(customerInquiryTypeLabel(selectedInquiry.inquiry_type))}</dd>
                        </div>
                        <div>
                          <dt className="text-text-faint">{t("Documents")}</dt>
                          <dd className="mt-0.5 font-semibold text-text-secondary">{selectedInquiry.document_count}</dd>
                        </div>
                        <div>
                          <dt className="text-text-faint">{t("Submitted")}</dt>
                          <dd className="mt-0.5 font-semibold text-text-secondary">{formatCustomerInquiryDate(selectedInquiry.submitted_at, locale)}</dd>
                        </div>
                        <div>
                          <dt className="text-text-faint">{t("Response estimate")}</dt>
                          <dd className="mt-0.5 font-semibold text-text-secondary">
                            {selectedInquiry.estimated_response_at
                              ? formatCustomerInquiryDate(selectedInquiry.estimated_response_at, locale)
                              : t("Pending review")}
                          </dd>
                        </div>
                        <div>
                          <dt className="text-text-faint">{t("Chat messages")}</dt>
                          <dd className="mt-0.5 font-semibold text-text-secondary">{communicationStats.messageCount}</dd>
                        </div>
                        <div>
                          <dt className="text-text-faint">{t("Open info requests")}</dt>
                          <dd className="mt-0.5 font-semibold text-text-secondary">{communicationStats.openInformationRequests}</dd>
                        </div>
                        <div className="col-span-2">
                          <dt className="text-text-faint">{t("Last chat activity")}</dt>
                          <dd className="mt-0.5 font-semibold text-text-secondary">{formatCustomerInquiryDate(communicationStats.lastActivityAt, locale)}</dd>
                        </div>
                      </dl>
                      <div className="mt-3 grid grid-cols-2 gap-2">
                        <Link
                          href={href(customerInquiryChatPath(selectedInquiry.id))}
                          onClick={() => setOpen(false)}
                          className="rounded-card border border-line-strong px-2 py-2 text-center text-3xs font-bold text-brand-300"
                        >
                          {t("Full chat workspace")}
                        </Link>
                        <Link
                          href={href(customerInquiryDetailPath(selectedInquiry.id))}
                          onClick={() => setOpen(false)}
                          className="rounded-card border border-line-strong px-2 py-2 text-center text-3xs font-bold text-text-secondary"
                        >
                          {t("Inquiry details")}
                        </Link>
                      </div>
                    </div>

                    <p className="mt-3 text-3xs leading-relaxed text-text-faint">
                      {t.template`Messages are saved against ${selectedInquiry.submission_reference} and visible only to you and authorized ProGlass staff.`}
                    </p>
                    <div className="mt-3">
                      <InquiryCommunicationPanel
                        key={selectedInquiry.id}
                        submissionId={selectedInquiry.id}
                        token={authToken ?? ""}
                        internalAccess={false}
                        compact
                        onCommunicationsChanged={handleCommunicationsChanged}
                        onUnauthorized={handleUnauthorized}
                      />
                    </div>
                  </>
                ) : null}
              </>
            )}
          </section>

          <p className="mt-4 text-3xs font-mono uppercase tracking-wider text-text-faint">{t("Other ways to reach us")}</p>
          <div className="mt-2 space-y-2">
            {site.whatsappNumber ? (
              <a
                href={whatsappHref}
                target="_blank"
                rel="noreferrer noopener"
                onClick={() => setOpen(false)}
                className="group flex items-center gap-3 rounded-card border-2 border-line-strong/60 bg-ink-950/60 px-3.5 py-3 transition-colors hover:border-emerald-400/60"
              >
                <span className="grid h-9 w-9 shrink-0 place-items-center rounded-card bg-emerald-500/15 text-emerald-300">
                  <Icon path="M8 2.5a5.5 5.5 0 1 0 2.9 10.1L14 13.5l-1.1-3.2A5.5 5.5 0 0 0 8 2.5zM6.4 5.2c.9 0 1.9 2.1 2.3 1.7.4-.3.7-.9 1.2-.9.6 0 1.6 1 1.4 1.5-.2.4-.8.8-.7 1.3.1.8 1 2.6-.4 3.4-.5.3-1 .1-1.5-.4L6.5 9.6c-.4-.5-.6-1-.4-1.4.2-.6.9-1.4.3-1.9-.4-.4-.6-1-0-1.1z" className="h-5 w-5" />
                </span>
                <span className="min-w-0">
                  <span className="block text-sm font-bold text-text-primary">{t("WhatsApp")}</span>
                  <span className="block text-xs text-text-muted">{t("Instant conversation — press send, add the question, done.")}</span>
                </span>
                <svg viewBox="0 0 16 16" className="ms-auto h-3.5 w-3.5 shrink-0 text-text-faint transition-transform group-hover:translate-x-0.5 group-hover:text-emerald-300" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round">
                  <path d="M2.5 8h11M9.5 4l4 4-4 4" />
                </svg>
              </a>
            ) : (
              <div className="flex items-center gap-3 rounded-card border border-dashed border-line-strong/60 px-3.5 py-3">
                <span className="grid h-9 w-9 shrink-0 place-items-center rounded-card bg-ink-800/70 text-text-faint">
                  <Icon path="M8 2.5a5.5 5.5 0 1 0 2.9 10.1L14 13.5l-1.1-3.2A5.5 5.5 0 0 0 8 2.5z" className="h-5 w-5" />
                </span>
                <span className="text-xs leading-relaxed text-text-faint">
                  {t("WhatsApp arrives as soon as the number is set in the site config.")}
                </span>
              </div>
            )}

            <a
              href={mailtoHref}
              onClick={() => setOpen(false)}
              className="group flex items-center gap-3 rounded-card border-2 border-line-strong/60 bg-ink-950/60 px-3.5 py-3 transition-colors hover:border-brand-400/60"
            >
              <span className="grid h-9 w-9 shrink-0 place-items-center rounded-card bg-brand-500/15 text-brand-300">
                <Icon path="M2.5 4.5h11v7h-11zM3 5l5 3.5L13 5" className="h-5 w-5" />
              </span>
              <span className="min-w-0">
                <span className="block text-sm font-bold text-text-primary">{t("Email")}</span>
                <span className="block truncate text-xs text-text-muted">{site.developerEmail}</span>
              </span>
            </a>

            <Link
              href={href("/contact")}
              onClick={() => setOpen(false)}
              className="group flex items-center gap-3 rounded-card border-2 border-line-strong/60 bg-ink-950/60 px-3.5 py-3 transition-colors hover:border-brand-400/60"
            >
              <span className="grid h-9 w-9 shrink-0 place-items-center rounded-card bg-brand-500/15 text-brand-300">
                <Icon path="M4 2.5h8a1 1 0 0 1 1 1v9a1 1 0 0 1-1 1H4a1 1 0 0 1-1-1v-9a1 1 0 0 1 1-1zM6 6h4M6 8.5h4M6 11h2.5" className="h-5 w-5" />
              </span>
              <span className="min-w-0">
                <span className="block text-sm font-semibold text-text-primary">{t("Demo request")}</span>
                <span className="block text-xs text-text-muted">{t("Written quote on your sizes and rates — straight to the Gmail inbox.")}</span>
              </span>
            </Link>
          </div>

          <p className="mt-3 border-t border-line/60 pt-2.5 text-2xs leading-relaxed text-text-faint">
            {t("WhatsApp and email open outside this website. Inquiry-chat messages are stored only against your selected inquiry after you press send.")}
          </p>
        </div>
      ) : null}

      <button
        ref={triggerRef}
        type="button"
        onClick={() => setOpen((value) => !value)}
        aria-expanded={open}
        aria-controls="support-dialog"
        aria-label={open ? t("Close chat support") : t("Chat with support")}
        className="menu-card support-launcher group flex items-center gap-2.5 rounded-full border-2 border-line-strong/70 bg-ink-900/90 px-4 py-3 text-xs font-bold text-text-primary shadow-raised transition-transform hover:-translate-y-0.5"
      >
        <span className="relative grid h-6 w-6 place-items-center rounded-full bg-emerald-500/20 text-emerald-300">
          <svg viewBox="0 0 20 20" className="h-4 w-4" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round">
            <path d="M17 12.5a1.5 1.5 0 0 1-1.5 1.5H8l-4.5 3.5v-3.5A1.5 1.5 0 0 1 2 12.5v-7A1.5 1.5 0 0 1 3.5 4h12A1.5 1.5 0 0 1 17 5.5z" />
            <path d="M6 8.5h8M6 11h5" />
          </svg>
          <span aria-hidden="true" className="absolute -top-0.5 -right-0.5 h-2.5 w-2.5 rounded-full border-2 border-ink-950 bg-emerald-400" />
        </span>
        {t("Chat")}
      </button>
    </div>
  );
}

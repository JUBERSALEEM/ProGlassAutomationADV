"use client";

import { useCallback, useMemo, useState } from "react";
import { LocaleLink } from "@/components/ui/LocaleLink";
import { InquiryCommunicationPanel } from "@/components/inquiries/InquiryCommunicationPanel";
import { useLocale } from "@/i18n/provider";
import {
  accountPathForRedirect,
  countCustomerInquiriesByStatus,
  customerInquiryDetailPath,
  customerInquiryStatusLabel,
  customerInquiryTypeLabel,
  formatCustomerInquiryDate,
  inquiryCommunicationStats,
  type InquiryCommunicationStats,
} from "@/lib/customer-inquiry-chat";
import { useCustomerInquiries } from "@/lib/useCustomerInquiries";
import { useOperatorSession } from "@/lib/useOperatorSession";
import type { InquiryCommunication } from "@/types/inquiry-communications";
import type { CustomerInquiryListItem } from "@/types/inquiry-submissions";

const EMPTY_COMMUNICATION_STATS: InquiryCommunicationStats = {
  messageCount: 0,
  openInformationRequests: 0,
  lastActivityAt: null,
};

function StatCard({ label, value, detail }: { label: string; value: string | number; detail?: string }) {
  return (
    <div className="rounded-2xl border border-line-strong bg-ink-900 p-4 shadow-panel">
      <div className="text-3xs font-mono uppercase tracking-wider text-text-faint">{label}</div>
      <div className="mt-2 text-2xl font-bold text-text-primary">{value}</div>
      {detail ? <div className="mt-1 text-3xs leading-relaxed text-text-muted">{detail}</div> : null}
    </div>
  );
}

function DetailItem({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <dt className="text-3xs font-mono uppercase tracking-wider text-text-faint">{label}</dt>
      <dd className="mt-1 text-sm font-semibold text-text-primary">{value}</dd>
    </div>
  );
}

function InquiryListButton({
  inquiry,
  selected,
  locale,
  onSelect,
}: {
  inquiry: CustomerInquiryListItem;
  selected: boolean;
  locale: string;
  onSelect: (id: string) => void;
}) {
  return (
    <button
      type="button"
      aria-current={selected ? "true" : undefined}
      onClick={() => onSelect(inquiry.id)}
      className={`w-full rounded-2xl border p-4 text-left transition-colors ${
        selected
          ? "border-brand-400/80 bg-brand-400/10"
          : "border-line-strong bg-ink-950/70 hover:border-brand-400/50"
      }`}
    >
      <span className="flex items-start justify-between gap-3">
        <span className="font-mono text-xs font-bold text-brand-300">{inquiry.submission_reference}</span>
        <span className="rounded-full border border-line-strong px-2 py-0.5 text-3xs uppercase text-text-muted">
          {customerInquiryStatusLabel(inquiry.status)}
        </span>
      </span>
      <span className="mt-2 block text-sm font-semibold text-text-primary">{inquiry.project_name}</span>
      <span className="mt-1 block text-3xs leading-relaxed text-text-faint">
        {customerInquiryTypeLabel(inquiry.inquiry_type)} · {inquiry.document_count} document
        {inquiry.document_count === 1 ? "" : "s"} · submitted {formatCustomerInquiryDate(inquiry.submitted_at, locale)}
      </span>
    </button>
  );
}

export function CustomerInquiryChatWorkspace({ initialInquiryId }: { initialInquiryId?: string }) {
  const locale = useLocale();
  const {
    checking,
    authToken,
    isAuthRequired,
    userEmail,
    requireAuth,
    unavailable: sessionUnavailable,
    error: sessionError,
    refreshSession,
  } = useOperatorSession();
  const { inquiries, loading, error, refresh } = useCustomerInquiries(authToken, {
    enabled: Boolean(authToken),
    onUnauthorized: requireAuth,
  });
  const [selectedId, setSelectedId] = useState(initialInquiryId ?? "");
  const [communicationStatsState, setCommunicationStatsState] = useState<{
    inquiryId: string;
    stats: InquiryCommunicationStats;
  } | null>(null);
  const [chatVersion, setChatVersion] = useState(0);

  const selected = useMemo(
    () => inquiries.find((inquiry) => inquiry.id === selectedId) ?? inquiries[0] ?? null,
    [inquiries, selectedId],
  );
  const counts = useMemo(() => countCustomerInquiriesByStatus(inquiries), [inquiries]);
  const requestedUnavailable = Boolean(
    initialInquiryId && inquiries.length && !inquiries.some((inquiry) => inquiry.id === initialInquiryId),
  );
  const communicationStats =
    communicationStatsState && communicationStatsState.inquiryId === selected?.id
      ? communicationStatsState.stats
      : EMPTY_COMMUNICATION_STATS;

  const handleCommunicationsChanged = useCallback(
    (rows: InquiryCommunication[]) => {
      const inquiryId = rows[0]?.submission_id ?? selected?.id;
      if (!inquiryId) return;
      setCommunicationStatsState({ inquiryId, stats: inquiryCommunicationStats(rows) });
    },
    [selected?.id],
  );

  const refreshAll = useCallback(() => {
    refresh();
    setChatVersion((version) => version + 1);
  }, [refresh]);

  if (checking) {
    return (
      <div role="status" className="grid min-h-[60vh] place-items-center bg-ink-950 text-xs text-text-muted">
        Checking your secure session…
      </div>
    );
  }

  if (sessionUnavailable && !authToken) {
    return (
      <main className="min-h-[65vh] bg-ink-950 px-4 py-20 text-text-primary">
        <section className="mx-auto max-w-xl rounded-3xl border border-state-warning/40 bg-ink-900 p-6 text-center shadow-panel sm:p-8">
          <p className="eyebrow text-state-warning">Saved sign-in</p>
          <h1 className="mt-3 text-2xl font-bold">We could not confirm your saved sign-in yet</h1>
          <p className="mt-3 text-sm leading-relaxed text-text-muted">
            {sessionError ?? "The authentication service is temporarily unreachable. Your saved sign-in has not been discarded."}
          </p>
          <button
            type="button"
            onClick={() => void refreshSession()}
            className="mt-6 inline-flex justify-center rounded-xl bg-brand-400 px-5 py-3 text-sm font-bold text-accent-ink"
          >
            Retry saved sign-in
          </button>
        </section>
      </main>
    );
  }

  if (isAuthRequired || !authToken) {
    return (
      <main className="min-h-[65vh] bg-ink-950 px-4 py-20 text-text-primary">
        <section className="mx-auto max-w-xl rounded-3xl border border-line-strong bg-ink-900 p-6 text-center shadow-panel sm:p-8">
          <p className="eyebrow text-brand-300">Customer account</p>
          <h1 className="mt-3 text-2xl font-bold">Sign in to your inquiries &amp; chat</h1>
          <p className="mt-3 text-sm leading-relaxed text-text-muted">
            Your inquiry references, documents, current status, and messages are private to your account. Sign in or
            create an account before opening an inquiry conversation.
          </p>
          <div className="mt-6 flex flex-col justify-center gap-3 sm:flex-row">
            <LocaleLink
              href={accountPathForRedirect("/inquiries/chat")}
              className="inline-flex justify-center rounded-xl bg-brand-400 px-5 py-3 text-sm font-bold text-accent-ink"
            >
              Sign in
            </LocaleLink>
            <LocaleLink
              href={accountPathForRedirect("/inquiries/chat", "signup")}
              className="inline-flex justify-center rounded-xl border border-line-strong px-5 py-3 text-sm font-bold text-brand-300"
            >
              Create account
            </LocaleLink>
          </div>
          <p className="mt-4 text-xs text-text-faint">New here? Create the account first, then submit your first inquiry.</p>
        </section>
      </main>
    );
  }

  return (
    <main className="min-h-screen bg-ink-950 pb-20 text-text-primary">
      <div className="shell py-8">
        <div className="flex flex-wrap items-end justify-between gap-4">
          <div className="max-w-3xl">
            <p className="eyebrow text-brand-300">Customer account</p>
            <h1 className="mt-2 text-3xl font-bold tracking-tight">My inquiries &amp; chat</h1>
            <p className="mt-2 text-sm leading-relaxed text-text-muted">
              Follow the current status of every inquiry submitted by this account and chat with the estimator/admin
              team on the selected inquiry reference.
            </p>
            {userEmail ? <p className="mt-2 text-xs text-text-faint">Signed in as {userEmail}</p> : null}
          </div>
          <div className="flex flex-wrap gap-2">
            <button
              type="button"
              onClick={refreshAll}
              className="rounded-xl border border-line-strong bg-ink-900 px-4 py-2.5 text-xs font-bold text-brand-300"
            >
              Refresh status
            </button>
            <LocaleLink
              href="/chat"
              className="rounded-xl border border-line-strong bg-ink-900 px-4 py-2.5 text-xs font-bold text-text-secondary"
            >
              Community chat
            </LocaleLink>
            <LocaleLink
              href="/inquiries/submit"
              className="rounded-xl bg-brand-400 px-4 py-2.5 text-xs font-bold text-accent-ink"
            >
              Submit new inquiry
            </LocaleLink>
          </div>
        </div>

        {error ? (
          <div role="alert" className="mt-5 rounded-2xl border border-state-danger/40 bg-state-danger/10 p-4 text-sm text-state-danger">
            {error}
          </div>
        ) : null}
        {requestedUnavailable ? (
          <div role="status" className="mt-5 rounded-2xl border border-state-warning/40 bg-state-warning/10 p-4 text-sm text-state-warning">
            That inquiry is not available in this account. Showing your most recent inquiry instead.
          </div>
        ) : null}

        <section aria-label="Your inquiry status summary" className="mt-6 grid gap-3 sm:grid-cols-2 lg:grid-cols-5">
          <StatCard label="Your inquiries" value={counts.total} detail={`${counts.active} still active`} />
          <StatCard label="Submitted" value={counts.submitted} />
          <StatCard label="Under review" value={counts.under_review} />
          <StatCard
            label="Accepted / downloaded"
            value={counts.acceptedOrDownloaded}
            detail={`${counts.accepted} accepted · ${counts.downloaded} downloaded`}
          />
          <StatCard
            label="Closed / rejected"
            value={counts.closedOrRejected}
            detail={`${counts.closed} closed · ${counts.rejected} rejected`}
          />
        </section>

        {loading && inquiries.length === 0 ? (
          <div role="status" className="mt-8 rounded-2xl border border-line-strong bg-ink-900 p-6 text-sm text-text-muted">
            Loading your inquiries…
          </div>
        ) : inquiries.length === 0 ? (
          <section className="mt-8 rounded-3xl border border-line-strong bg-ink-900 p-6 text-center shadow-panel sm:p-8">
            <h2 className="text-xl font-bold">No inquiry to chat about yet</h2>
            <p className="mx-auto mt-2 max-w-xl text-sm leading-relaxed text-text-muted">
              Inquiry chat opens after you submit a completed inquiry. Your submission reference will connect the
              conversation, documents, and current status in one private place.
            </p>
            <LocaleLink
              href="/inquiries/submit"
              className="mt-5 inline-flex rounded-xl bg-brand-400 px-5 py-3 text-sm font-bold text-accent-ink"
            >
              Submit your first inquiry
            </LocaleLink>
          </section>
        ) : selected ? (
          <div className="mt-6 grid items-start gap-5 lg:grid-cols-[340px_minmax(0,1fr)]">
            <section aria-label="Choose one of your inquiries" className="space-y-3">
              <div className="flex items-center justify-between gap-3">
                <h2 className="text-sm font-bold">Choose inquiry</h2>
                <span className="text-3xs text-text-faint">Only this account’s inquiries are listed.</span>
              </div>
              {inquiries.map((inquiry) => (
                <InquiryListButton
                  key={inquiry.id}
                  inquiry={inquiry}
                  selected={inquiry.id === selected.id}
                  locale={locale}
                  onSelect={setSelectedId}
                />
              ))}
            </section>

            <div className="min-w-0">
              <section className="rounded-3xl border border-line-strong bg-ink-900 p-5 shadow-panel">
                <div className="flex flex-wrap items-start justify-between gap-4">
                  <div>
                    <div className="font-mono text-sm font-bold text-brand-300">{selected.submission_reference}</div>
                    <h2 className="mt-1 text-2xl font-bold">{selected.project_name}</h2>
                    <p className="mt-1 text-xs text-text-muted">
                      {selected.company_name} · {selected.project_location || "Location not supplied"}
                    </p>
                  </div>
                  <span className="rounded-full border border-brand-400/60 bg-brand-400/10 px-3 py-1 text-xs font-bold uppercase text-brand-200">
                    Current status: {customerInquiryStatusLabel(selected.status)}
                  </span>
                </div>

                <dl className="mt-5 grid gap-4 border-t border-line/50 pt-5 sm:grid-cols-2 lg:grid-cols-3">
                  <DetailItem label="Inquiry type" value={customerInquiryTypeLabel(selected.inquiry_type)} />
                  <DetailItem label="Submitted" value={formatCustomerInquiryDate(selected.submitted_at, locale)} />
                  <DetailItem label="Status updated" value={formatCustomerInquiryDate(selected.status_updated_at, locale)} />
                  <DetailItem
                    label="Estimated response"
                    value={
                      selected.estimated_response_at
                        ? formatCustomerInquiryDate(selected.estimated_response_at, locale)
                        : "Pending operator review"
                    }
                  />
                  <DetailItem label="Private documents" value={String(selected.document_count)} />
                  <DetailItem label="Your reference" value={selected.customer_reference || "—"} />
                </dl>

                {selected.operator_note ? (
                  <div className="mt-5 rounded-2xl border border-line-strong bg-ink-950 p-4">
                    <div className="text-3xs font-mono uppercase tracking-wider text-text-faint">Operator note</div>
                    <p className="mt-2 whitespace-pre-wrap text-sm leading-relaxed text-text-secondary">
                      {selected.operator_note}
                    </p>
                  </div>
                ) : null}

                <div className="mt-5 grid gap-3 sm:grid-cols-3">
                  <StatCard label="Chat messages" value={communicationStats.messageCount} />
                  <StatCard label="Open info requests" value={communicationStats.openInformationRequests} />
                  <StatCard
                    label="Last chat activity"
                    value={formatCustomerInquiryDate(communicationStats.lastActivityAt, locale)}
                  />
                </div>

                <div className="mt-5 flex flex-wrap gap-2">
                  <LocaleLink
                    href={customerInquiryDetailPath(selected.id)}
                    className="rounded-xl border border-line-strong px-4 py-2.5 text-xs font-bold text-brand-300"
                  >
                    Open full inquiry &amp; documents
                  </LocaleLink>
                  <LocaleLink
                    href="/inquiries/submit"
                    className="rounded-xl border border-line-strong px-4 py-2.5 text-xs font-bold text-text-secondary"
                  >
                    Submit another inquiry
                  </LocaleLink>
                </div>
              </section>

              <p className="mt-4 rounded-2xl border border-line bg-ink-900/60 p-4 text-xs leading-relaxed text-text-muted">
                Messages below are saved against <strong className="text-text-primary">{selected.submission_reference}</strong>{" "}
                and are visible only to this customer account and authorized ProGlass estimator/admin staff.
              </p>

              <InquiryCommunicationPanel
                key={`${selected.id}-${chatVersion}`}
                submissionId={selected.id}
                token={authToken}
                internalAccess={false}
                onCommunicationsChanged={handleCommunicationsChanged}
                onUnauthorized={requireAuth}
              />
            </div>
          </div>
        ) : null}
      </div>
    </main>
  );
}

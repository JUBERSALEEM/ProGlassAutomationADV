"use client";

import { Children, useEffect, useRef, useState, type ReactNode } from "react";

type Hint = { title: string; body: string; example?: string; group?: string };

/**
 * Accessible one-question-at-a-time intake inside a single HTML form.
 * Inactive prompts stay mounted (but hidden) so native FormData keeps every
 * answer when the customer advances or goes back.
 */
export function InquiryPromptFlow({
  children,
  hints,
}: {
  children: ReactNode;
  hints: Hint[];
}) {
  const prompts = Children.toArray(children);
  const [active, setActive] = useState(0);
  const promptRoot = useRef<HTMLDivElement>(null);
  const didMount = useRef(false);
  const currentHint = hints[active] ?? hints[0]!;
  const group = currentHint.group ?? "Project request";

  useEffect(() => {
    if (!didMount.current) {
      didMount.current = true;
      return;
    }
    const activePrompt = promptRoot.current?.querySelector<HTMLElement>(`[data-intake-prompt="${active}"]`);
    const field = activePrompt?.querySelector<HTMLElement>("input:not([type='hidden']), select, textarea, button");
    field?.focus({ preventScroll: true });
  }, [active]);

  function advance() {
    const activePrompt = promptRoot.current?.querySelector<HTMLElement>(`[data-intake-prompt="${active}"]`);
    const incompleteGenerator = activePrompt?.querySelector<HTMLElement>("[data-intake-complete='false']");
    if (incompleteGenerator) {
      incompleteGenerator.dispatchEvent(new CustomEvent("intake-advance-blocked", { bubbles: true }));
      return;
    }
    const requiredField = activePrompt?.querySelector<HTMLInputElement | HTMLSelectElement | HTMLTextAreaElement>("input[required], select[required], textarea[required]");
    if (requiredField && !requiredField.checkValidity()) {
      requiredField.reportValidity();
      requiredField.focus();
      return;
    }
    setActive((current) => Math.min(prompts.length - 1, current + 1));
  }

  function goBack() {
    setActive((current) => Math.max(0, current - 1));
  }

  function onPromptKeyDown(event: React.KeyboardEvent<HTMLDivElement>) {
    if (event.key !== "Enter" || event.shiftKey || event.altKey || event.ctrlKey || event.metaKey) return;
    const target = event.target;
    if (!(target instanceof HTMLElement) || target instanceof HTMLTextAreaElement || target.closest("button") || target.closest("[data-no-intake-advance]")) return;
    if (active < prompts.length - 1) {
      event.preventDefault();
      advance();
    }
  }

  return (
    <section className="mt-5" aria-label="Guided inquiry form">
      <div className="mb-5 flex items-center justify-between gap-3">
        <div>
          <p className="text-[.68rem] font-bold uppercase tracking-[.18em] text-brand-200">{group}</p>
          <p className="mt-1 text-xs text-text-faint">One connected flow · no separate company, client, or project pages</p>
        </div>
        <span className="shrink-0 rounded-full border border-white/10 bg-white/[.04] px-3 py-1.5 font-mono text-xs text-text-muted" aria-live="polite">
          {active + 1} <span className="text-text-faint">/ {prompts.length}</span>
        </span>
      </div>

      <div className="mb-5 flex gap-1.5" aria-hidden="true">
        {prompts.map((_, index) => (
          <span key={index} className={`h-1 flex-1 rounded-full transition-colors duration-300 ${index <= active ? "bg-brand-300" : "bg-white/10"}`} />
        ))}
      </div>

      <div className="grid gap-5 lg:grid-cols-[minmax(0,1fr)_minmax(15rem,.62fr)]">
        <div className="relative min-h-[190px] rounded-2xl border border-brand-300/20 bg-ink-950/65 p-4 sm:p-6" ref={promptRoot} onKeyDown={onPromptKeyDown}>
          {prompts.map((prompt, index) => (
            <div
              key={index}
              data-intake-prompt={index}
              hidden={index !== active}
              aria-hidden={index !== active}
              className={index === active ? "animate-[intake-prompt-in_.26s_cubic-bezier(.2,.8,.2,1)] motion-reduce:animate-none" : ""}
            >
              {prompt}
            </div>
          ))}

          <div className="mt-7 flex items-center justify-between gap-3 border-t border-white/10 pt-4">
            <button
              type="button"
              onClick={goBack}
              disabled={active === 0}
              aria-label="Previous question"
              className="inline-flex h-10 items-center gap-2 rounded-xl border border-white/10 px-3 text-xs font-semibold text-text-muted transition hover:border-white/20 hover:text-white disabled:cursor-not-allowed disabled:opacity-35"
            >
              <span aria-hidden="true">←</span><span>Back</span>
            </button>
            <p className="hidden text-center text-[.68rem] text-text-faint sm:block">Press Enter or use the arrow to continue</p>
            {active < prompts.length - 1 ? (
              <button
                type="button"
                onClick={advance}
                aria-label="Next question"
                className="inline-flex h-11 w-11 items-center justify-center rounded-xl bg-brand-400 text-lg font-bold text-ink-950 shadow-md shadow-brand-950/30 transition hover:-translate-y-0.5 hover:bg-brand-300 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-brand-200"
              >
                <span aria-hidden="true">→</span>
              </button>
            ) : (
              <span className="rounded-xl border border-emerald-300/20 bg-emerald-300/[.06] px-3 py-2 text-xs font-semibold text-emerald-100">Ready to send</span>
            )}
          </div>
        </div>

        <aside className="flex flex-col justify-between rounded-2xl border border-white/10 bg-gradient-to-br from-white/[.055] to-white/[.02] p-4 sm:p-5" aria-live="polite" aria-atomic="true">
          <div>
            <p className="text-[.66rem] font-bold uppercase tracking-[.18em] text-text-faint">Quick guide</p>
            <h3 className="mt-2 text-base font-bold text-text-primary">{currentHint.title}</h3>
            <p className="mt-2 text-sm leading-6 text-text-muted">{currentHint.body}</p>
          </div>
          {currentHint.example ? (
            <div className="mt-5 rounded-xl border border-brand-300/15 bg-brand-400/[.06] p-3.5">
              <p className="text-[.62rem] font-bold uppercase tracking-[.16em] text-brand-200">Example</p>
              <p className="mt-1.5 text-sm font-semibold text-text-primary">{currentHint.example}</p>
            </div>
          ) : (
            <p className="mt-5 rounded-xl border border-white/10 bg-black/10 p-3.5 text-xs leading-5 text-text-faint">Your information is saved to your account and routed with this request.</p>
          )}
        </aside>
      </div>
    </section>
  );
}

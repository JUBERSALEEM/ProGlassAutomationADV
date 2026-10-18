"use client";

import { useEffect, useLayoutEffect, useRef } from "react";

const FOCUSABLE = [
  "a[href]", "button:not([disabled])", "input:not([disabled])", "select:not([disabled])",
  "textarea:not([disabled])", "[tabindex]:not([tabindex='-1'])",
].join(",");

/** Shared keyboard/focus contract for retained operational dialogs. */
export function useModalDialog<T extends HTMLElement>({
  open,
  onClose,
  closeDisabled = false,
}: {
  open: boolean;
  onClose: () => void;
  closeDisabled?: boolean;
}) {
  const ref = useRef<T | null>(null);
  const closeRef = useRef(onClose);
  const disabledRef = useRef(closeDisabled);
  useLayoutEffect(() => {
    closeRef.current = onClose;
    disabledRef.current = closeDisabled;
  }, [onClose, closeDisabled]);

  useEffect(() => {
    if (!open) return;
    const previous = document.activeElement instanceof HTMLElement ? document.activeElement : null;
    const node = ref.current;
    const first = node?.querySelector<HTMLElement>(FOCUSABLE);
    (first ?? node)?.focus({ preventScroll: true });

    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape" && !disabledRef.current) {
        event.preventDefault();
        closeRef.current();
        return;
      }
      if (event.key !== "Tab" || !node) return;
      const items = [...node.querySelectorAll<HTMLElement>(FOCUSABLE)].filter(
        (item) => !item.hidden && item.getAttribute("aria-hidden") !== "true",
      );
      if (!items.length) {
        event.preventDefault();
        node.focus();
        return;
      }
      const firstItem = items[0]!;
      const lastItem = items.at(-1)!;
      if (event.shiftKey && (document.activeElement === firstItem || document.activeElement === node)) {
        event.preventDefault();
        lastItem.focus();
      } else if (!event.shiftKey && document.activeElement === lastItem) {
        event.preventDefault();
        firstItem.focus();
      }
    };

    document.addEventListener("keydown", onKeyDown);
    return () => {
      document.removeEventListener("keydown", onKeyDown);
      if (previous?.isConnected) previous.focus({ preventScroll: true });
    };
  }, [open]);

  return ref;
}

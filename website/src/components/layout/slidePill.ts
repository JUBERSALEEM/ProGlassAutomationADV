/*
 * The sliding highlight pill (§AE/§AF) — one moulded card that
 * travels between the rows of a menu, the same pattern the top bar
 * uses between its entries (Navbar.moveIndicator owns the bar; this
 * owns lists).
 *
 * Written imperatively, like the bar's: pointer movement across rows
 * must never re-render the header (see the Navbar comment on why the
 * bar's pill left React state). All this module does is measure the
 * target row, size/translate the list's `.menu-slide-pill` element to
 * it, and tag the row `data-pill-on` so the Clay engine can ink its
 * text for the lavender it now sits on. In families without pill
 * styling the span is inert and these writes are no-ops.
 */

export function slidePillTo(list: HTMLElement, target: HTMLElement | null) {
  const pill = list.querySelector<HTMLElement>(".menu-slide-pill");
  if (!pill) return;
  list
    .querySelectorAll("[data-pill-on]")
    .forEach((el) => el.removeAttribute("data-pill-on"));
  if (!target) {
    pill.style.opacity = "0";
    return;
  }
  const listRect = list.getBoundingClientRect();
  const rect = target.getBoundingClientRect();
  if (rect.width === 0 || rect.height === 0) {
    pill.style.opacity = "0";
    return;
  }
  target.setAttribute("data-pill-on", "true");
  pill.style.width = `${rect.width}px`;
  pill.style.height = `${rect.height}px`;
  pill.style.transform = `translate(${rect.left - listRect.left}px, ${rect.top - listRect.top}px)`;
  pill.style.opacity = "1";
}

/* The pill's resting place in a list: the row this page (or choice)
   already is — the menu's own current destination / selected row. */
export function restSlidePill(list: HTMLElement) {
  slidePillTo(
    list,
    list.querySelector<HTMLElement>('[aria-current="page"], [aria-selected="true"]'),
  );
}

/* Pointer/focus travel inside a list: hand the pill to the row under
   the gesture, and back to its resting row when the gesture leaves.
   Spread onto the list element itself; the list is always
   `event.currentTarget`, so no ref is read while rendering. */
export const listPillEvents = {
  onPointerOver: (event: React.PointerEvent<HTMLElement>) => {
    const target = (event.target as HTMLElement).closest<HTMLElement>("a, button");
    if (target && event.currentTarget.contains(target)) {
      slidePillTo(event.currentTarget, target);
    }
  },
  onPointerLeave: (event: React.PointerEvent<HTMLElement>) => {
    restSlidePill(event.currentTarget);
  },
  onFocusCapture: (event: React.FocusEvent<HTMLElement>) => {
    const target = (event.target as HTMLElement).closest<HTMLElement>("a, button");
    if (target && event.currentTarget.contains(target)) {
      slidePillTo(event.currentTarget, target);
    }
  },
  onBlurCapture: (event: React.FocusEvent<HTMLElement>) => {
    if (!event.currentTarget.contains(event.relatedTarget as Node)) {
      restSlidePill(event.currentTarget);
    }
  },
};

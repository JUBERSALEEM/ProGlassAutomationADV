// @vitest-environment jsdom

/*
 * G5 behavioral: the catch-light listener, driven by synthetic pointer
 * traffic — the geometry, the gating, and the cleanup, against a DOM the
 * engine would actually paint on.
 */

import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { initGlassLife } from "@/lib/glassLife";

let stop: (() => void) | null = null;

const setAppear = (v = "crystal") =>
  document.documentElement.setAttribute("data-appearance", v);

const makeCard = () => {
  const el = document.createElement("div");
  el.className = "bg-ink-900/60 rounded-xl p-4";
  /* jsdom has no layout: gift it a box */
  vi.spyOn(el, "getBoundingClientRect").mockReturnValue({
    left: 100, top: 50, width: 200, height: 100,
    right: 300, bottom: 150, x: 100, y: 50,
    toJSON: () => ({}),
  } as DOMRect);
  document.body.appendChild(el);

  const child = document.createElement("span");
  el.appendChild(child);
  return { el, child };
};


const probePoint = (target: Element | null) => {
  document.elementFromPoint = (() => target) as unknown as Document["elementFromPoint"];
};

const fire = (x: number, y: number) => {
  /* jsdom has no PointerEvent constructor: a real pointermove IS a
     MouseEvent-shaped Event; clientX/clientY are what the listener reads. */
  const e = new Event("pointermove", { bubbles: true }) as PointerEvent;
  Object.defineProperties(e, {
    clientX: { value: x },
    clientY: { value: y },
  });
  window.dispatchEvent(e);
};

const settleFrames = async () => {
  await new Promise((r) => setTimeout(r, 30));
};

beforeEach(() => {
  document.documentElement.style.removeProperty("--mx");
  document.body.innerHTML = "";
  setAppear();
  /* jsdom matchMedia exists but matches=false everywhere; the hover gate
     must be opened by hand — reduced-motion stays closed (jsdom's default)
     which is precisely the shipped desktop profile. */
  vi.spyOn(window, "matchMedia").mockImplementation((q: string) => {
    const isHover = q.includes("hover: hover");
    return {
      matches: isHover,
      media: q,
      addEventListener: () => {},
      removeEventListener: () => {},
      addListener: () => {},
      removeListener: () => {},
      onchange: null,
      dispatchEvent: () => false,
    } as MediaQueryList;
  });
});

afterEach(() => {
  stop?.();
  stop = null;
  vi.restoreAllMocks();
});

describe("glassLife — the cursor's light", () => {
  it("writes --mx/--my as percentages of the card's own box", async () => {
    const { el, child } = makeCard();
    probePoint(child); // hover the child: closest() must find the card
    stop = initGlassLife();

    fire(200, 100); // card spans (100,50)-(300,150) → 50% / 50%
    await settleFrames();

    expect(el.style.getPropertyValue("--mx")).toBe("50.00%");
    expect(el.style.getPropertyValue("--my")).toBe("50.00%");
  });

  it("clamps at the edge — a pointer just past the card does not write 104%", async () => {
    const { el } = makeCard();
    probePoint(el);
    stop = initGlassLife();

    fire(1000, -40);
    await settleFrames();
    expect(el.style.getPropertyValue("--mx")).toBe("100.00%");
    expect(el.style.getPropertyValue("--my")).toBe("0.00%");
  });

  it("writes nothing under a non-crystal family (clay must never wear glass)", async () => {
    setAppear("clay");
    const { el } = makeCard();
    probePoint(el);
    stop = initGlassLife();

    fire(200, 100);
    await settleFrames();
    expect(el.style.getPropertyValue("--mx")).toBe("");
  });

  it("writes nothing off-card, and clears a previously lit card it has left", async () => {
    const { el } = makeCard();
    const dead = document.createElement("div");
    document.body.appendChild(dead);
    
    stop = initGlassLife();

    probePoint(el);
    fire(200, 100);
    await settleFrames();
    expect(el.style.getPropertyValue("--mx")).toBe("50.00%");

    probePoint(dead);
    fire(600, 600);
    await settleFrames();
    expect(el.style.getPropertyValue("--mx")).toBe("");
    expect(el.style.getPropertyValue("--my")).toBe("");
  });

  it("fires ONE rAF per frame even under an event storm", async () => {
    const { el } = makeCard();
    probePoint(el);
    stop = initGlassLife();

    const frames: FrameRequestCallback[] = [];
    const rafSpy = vi.spyOn(window, "requestAnimationFrame").mockImplementation((cb) => {
      frames.push(cb);
      return frames.length;
    });

    for (let i = 0; i < 60; i++) fire(150 + i, 90);
    expect(rafSpy.mock.calls.length).toBe(1); // the storm coalesced
    for (const cb of frames) cb(performance.now());
    await settleFrames();
    expect(Number.parseFloat(el.style.getPropertyValue("--mx"))).toBeGreaterThanOrEqual(0);
  });

  it("cleanup removes the listener and unlits — a studio toggle leaves no ghost", async () => {
    const { el } = makeCard();
    probePoint(el);
    stop = initGlassLife();

    fire(200, 100);
    await settleFrames();
    expect(el.style.getPropertyValue("--mx")).toBe("50.00%");
    stop();
    stop = null;

    /* cleanup un-lights: no ghost light after the owner leaves */
    expect(el.style.getPropertyValue("--mx")).toBe("");

    fire(250, 120);
    await settleFrames();
    expect(el.style.getPropertyValue("--mx")).toBe(""); // and no new writes — the listener is gone
  });
});

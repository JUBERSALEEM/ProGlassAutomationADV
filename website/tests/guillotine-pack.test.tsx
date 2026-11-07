import { describe, expect, it } from "vitest";
import { packGuillotine } from "@/lib/guillotinePack";

const sheet = { width: 1000, height: 700 };
const noMargins = { left: 0, right: 0, top: 0, bottom: 0, kerf: 3 };

describe("browser demo guillotine packer", () => {
  it("returns no empty sheet for an empty batch", () => {
    const result = packGuillotine([], sheet, noMargins);

    expect(result.plates).toBe(0);
    expect(result.placed).toHaveLength(0);
    expect(result.unplaced).toHaveLength(0);
  });

  it("does not open an empty sheet for a piece that cannot fit in either orientation", () => {
    const result = packGuillotine([{ id: "oversize", width: 1200, height: 800 }], sheet, noMargins);

    expect(result.plates).toBe(0);
    expect(result.placed).toHaveLength(0);
    expect(result.unplaced.map((piece) => piece.id)).toEqual(["oversize"]);
  });

  it("honours an explicit no-rotation constraint while retaining the default rotate behavior", () => {
    const rotatable = packGuillotine([{ id: "directional", width: 700, height: 1000 }], sheet, noMargins);
    const locked = packGuillotine([{ id: "directional", width: 700, height: 1000, allowRotate: false }], sheet, noMargins);

    expect(rotatable.placed[0]?.rotated).toBe(true);
    expect(locked.placed).toHaveLength(0);
    expect(locked.unplaced).toHaveLength(1);
    expect(locked.plates).toBe(0);
  });
});

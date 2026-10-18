// @vitest-environment jsdom
import { fireEvent, render, screen } from "@testing-library/react";
import { useState } from "react";
import { describe, expect, it } from "vitest";
import { useModalDialog } from "@/lib/useModalDialog";

function Fixture() {
  const [open, setOpen] = useState(false);
  const ref = useModalDialog<HTMLDivElement>({ open, onClose: () => setOpen(false) });
  return <><button onClick={() => setOpen(true)}>Open</button>{open && <div ref={ref} tabIndex={-1} role="dialog" aria-modal="true"><button>First</button><button onClick={() => setOpen(false)}>Last</button></div>}</>;
}

describe("shared modal focus contract", () => {
  it("focuses entry, traps Tab, closes on Escape, and restores the trigger", () => {
    render(<Fixture />);
    const trigger = screen.getByRole("button", { name: "Open" });
    trigger.focus(); fireEvent.click(trigger);
    const first = screen.getByRole("button", { name: "First" });
    const last = screen.getByRole("button", { name: "Last" });
    expect(document.activeElement).toBe(first);
    last.focus(); fireEvent.keyDown(document, { key: "Tab" });
    expect(document.activeElement).toBe(first);
    fireEvent.keyDown(document, { key: "Escape" });
    expect(screen.queryByRole("dialog")).toBeNull();
    expect(document.activeElement).toBe(trigger);
  });
});

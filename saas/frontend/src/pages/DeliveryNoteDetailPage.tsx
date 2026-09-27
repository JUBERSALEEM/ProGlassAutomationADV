import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Link, useParams } from "react-router-dom";
import {
  getDeliveryNote,
  getMasterDn,
  transitionDn,
  updateDispatch,
  voidDn,
} from "../api/endpoints";
import { Card, ErrorNote, Spinner, StatusBadge, date } from "../components/ui";
import { errorText } from "../auth/AuthContext";

/**
 * The delivery note workspace: the dispatch edit block + line quantities
 * (open notes only, 0 ≤ qty ≤ balance — the BUG-05 guard is server-side),
 * transitions and void. The master DN link shows the balance tracker.
 */
export default function DeliveryNoteDetailPage() {
  const { id = "" } = useParams();
  const queryClient = useQueryClient();
  const [actionError, setActionError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  const note = useQuery({
    queryKey: ["delivery-note", id],
    queryFn: () => getDeliveryNote(id),
    enabled: id !== "",
  });

  const master = useQuery({
    queryKey: ["master-dn", note.data?.jobOrderId],
    queryFn: () => getMasterDn(note.data!.jobOrderId),
    enabled: Boolean(note.data?.jobOrderId),
  });

  const invalidate = async () => {
    await queryClient.invalidateQueries({ queryKey: ["delivery-note", id] });
    await queryClient.invalidateQueries({ queryKey: ["master-dn"] });
    await queryClient.invalidateQueries({ queryKey: ["delivery-notes"] });
  };

  const transition = useMutation({
    mutationFn: (args: { to: string; reason?: string }) =>
      transitionDn(id, args.to, args.reason),
    onSuccess: async (next) => {
      setActionError(null);
      setNotice(`Status → ${next.status}.`);
      await invalidate();
    },
    onError: (err) => setActionError(errorText(err)),
  });

  const voidIt = useMutation({
    mutationFn: (reason?: string) => voidDn(id, reason),
    onSuccess: async () => {
      setActionError(null);
      setNotice("Note voided (soft-close; the master balance was refreshed).");
      await invalidate();
    },
    onError: (err) => setActionError(errorText(err)),
  });

  if (note.isLoading) return <Spinner label="Loading note…" />;
  if (note.error) return <ErrorNote error={note.error} />;
  const dn = note.data!;
  const open = !dn.isVoid && dn.status !== "Received";
  const editable = open && dn.status === "Dispatching" || open && dn.status === "Draft";

  const nextStatuses = ["Dispatched", "Delivered", "Signed", "Received"].filter(
    (s) => s !== dn.status,
  );

  return (
    <div className="stack">
      <Card
        title={`${dn.deliveryNo} — ${dn.customerName}`}
        actions={
          <Link className="btn ghost" to="/delivery-notes">
            ← All delivery notes
          </Link>
        }
      >
        <div className="kv-grid">
          <div>
            <span className="kv-label">Status</span>
            <StatusBadge value={dn.status} />
            {dn.isVoid && <span className="sub"> void</span>}
          </div>
          <div>
            <span className="kv-label">Job order</span>
            <Link to={`/job-orders/${dn.jobOrderId}`}>{dn.jobOrderNo}</Link>
          </div>
          <div>
            <span className="kv-label">Type</span>
            {dn.deliveryType}
            {dn.isFoc ? " · FOC" : ""}
          </div>
          <div>
            <span className="kv-label">Delivery</span>
            {date(dn.deliveryDate)} {dn.deliveryTime}
          </div>
          <div>
            <span className="kv-label">Driver / vehicle</span>
            {dn.driver || "—"} · {dn.vehicle || "—"}
          </div>
          <div>
            <span className="kv-label">Receiver</span>
            {dn.receiverName || "—"}
          </div>
          {master.data && (
            <div>
              <span className="kv-label">Master balance</span>
              {master.data.totalOrderedQty} ordered · {master.data.totalDeliveredQty}{" "}
              confirmed · {master.data.totalBalanceQty} remaining
            </div>
          )}
        </div>
        {dn.notes && <p className="note">{dn.notes}</p>}

        {editable && <DispatchEditor noteId={id} note={dn} onSaved={invalidate} />}

        <div className="actions bar">
          {dn.isVoid || dn.status === "Received" ? (
            <span className="sub">
              {dn.isVoid ? "Voided — history only." : "Received — terminal."}
            </span>
          ) : (
            <span className="actions">
              <select
                defaultValue={nextStatuses[0] ?? ""}
                onChange={() => setNotice(null)}
                id="dn-next"
              >
                {nextStatuses.map((s) => (
                  <option key={s}>{s}</option>
                ))}
              </select>
              <button
                className="btn small primary"
                disabled={transition.isPending}
                onClick={() => {
                  const select = document.getElementById("dn-next") as HTMLSelectElement;
                  if (select?.value) transition.mutate({ to: select.value });
                }}
              >
                Transition
              </button>
            </span>
          )}
          <button
            className="btn small bad"
            disabled={voidIt.isPending || dn.isVoid}
            onClick={() => {
              const reason = window.prompt("Void reason (optional):") ?? "";
              voidIt.mutate(reason || undefined);
            }}
          >
            {dn.isVoid ? "Voided" : "Void"}
          </button>
        </div>
        {actionError && <div className="note bad">{actionError}</div>}
        {notice && <div className="note ok">{notice}</div>}
      </Card>

      <Card title="Lines">
        <table>
          <thead>
            <tr>
              <th>Spec group</th>
              <th>Glass</th>
              <th className="num">Size (mm)</th>
              <th className="num">Ordered</th>
              <th className="num">Prev. delivered</th>
              <th className="num">Delivered</th>
              <th className="num">Balance</th>
              <th className="num">Rework</th>
              <th className="num">Returned</th>
            </tr>
          </thead>
          <tbody>
            {dn.items.map((item) => (
              <tr key={item.id}>
                <td>
                  #{item.specGroupNumber} · S{item.serialNumber} {item.specGroupName}
                </td>
                <td>{item.glassRef}</td>
                <td className="num">
                  {item.w1}×{item.h1}
                  {item.w2 || item.h2 ? ` / ${item.w2}×${item.h2}` : ""}
                </td>
                <td className="num">{item.orderedQty}</td>
                <td className="num">{item.previouslyDeliveredQty}</td>
                <td className="num">{item.deliveredQty}</td>
                <td className="num">
                  <strong>{item.balanceQty}</strong>
                </td>
                <td className="num">{item.reworkQty}</td>
                <td className="num">{item.returnedQty}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </Card>
    </div>
  );
}

function DispatchEditor(props: {
  noteId: string;
  note: import("../api/types").DeliveryNote;
  onSaved: () => Promise<void>;
}) {
  const { noteId, note, onSaved } = props;
  const [driver, setDriver] = useState(note.driver);
  const [vehicle, setVehicle] = useState(note.vehicle);
  const [receiver, setReceiver] = useState(note.receiverName);
  const [deliveryDate, setDeliveryDate] = useState(note.deliveryDate ?? "");
  const [deliveryTime, setDeliveryTime] = useState(note.deliveryTime);
  const [notes, setNotes] = useState(note.notes);
  const [isFoc, setIsFoc] = useState(note.isFoc);
  const [qty, setQty] = useState<Record<string, number>>(
    Object.fromEntries(note.items.map((item) => [item.id, item.deliveredQty])),
  );
  const [error, setError] = useState<string | null>(null);

  const save = useMutation({
    mutationFn: () =>
      updateDispatch(noteId, {
        driver,
        vehicle,
        receiverName: receiver,
        deliveryDate: deliveryDate || undefined,
        deliveryTime,
        notes,
        isFoc,
        items: note.items.map((item) => ({
          itemId: item.id,
          deliveredQty: qty[item.id] ?? 0,
        })),
        expectedRowVersion: note.rowVersion,
      }),
    onSuccess: async () => {
      setError(null);
      await onSaved();
    },
    onError: (err) => setError(errorText(err)),
  });

  return (
    <form
      className="create-form"
      onSubmit={(e) => {
        e.preventDefault();
        save.mutate();
      }}
    >
      <h3>Dispatch edit (open note)</h3>
      <div className="form-grid">
        <label>
          Driver
          <input value={driver} onChange={(e) => setDriver(e.target.value)} />
        </label>
        <label>
          Vehicle
          <input value={vehicle} onChange={(e) => setVehicle(e.target.value)} />
        </label>
        <label>
          Receiver
          <input value={receiver} onChange={(e) => setReceiver(e.target.value)} />
        </label>
        <label>
          Delivery date
          <input
            type="date"
            value={deliveryDate}
            onChange={(e) => setDeliveryDate(e.target.value)}
          />
        </label>
        <label>
          Delivery time
          <input
            value={deliveryTime}
            onChange={(e) => setDeliveryTime(e.target.value)}
            placeholder="e.g. 09:30"
          />
        </label>
        <label className="check" style={{ alignSelf: "end" }}>
          <input
            type="checkbox"
            checked={isFoc}
            onChange={(e) => setIsFoc(e.target.checked)}
          />
          FOC note
        </label>
      </div>
      <label>
        Notes
        <input value={notes} onChange={(e) => setNotes(e.target.value)} />
      </label>
      <table>
        <thead>
          <tr>
            <th>Line</th>
            <th className="num">Balance</th>
            <th className="num">Delivered qty (0…balance)</th>
          </tr>
        </thead>
        <tbody>
          {note.items.map((item) => (
            <tr key={item.id}>
              <td>
                #{item.specGroupNumber} · S{item.serialNumber} {item.glassRef}
              </td>
              <td className="num">{item.balanceQty}</td>
              <td className="num">
                <input
                  type="number"
                  min={0}
                  max={item.balanceQty}
                  value={qty[item.id] ?? 0}
                  onChange={(e) =>
                    setQty((prev) => ({ ...prev, [item.id]: Number(e.target.value) }))
                  }
                />
              </td>
            </tr>
          ))}
        </tbody>
      </table>
      {error && <div className="note bad">{error}</div>}
      <div className="form-actions">
        <button type="submit" className="btn primary" disabled={save.isPending}>
          {save.isPending ? "Saving…" : "Save dispatch"}
        </button>
      </div>
    </form>
  );
}

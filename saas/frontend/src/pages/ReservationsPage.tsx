import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  cancelReservation,
  consumeReservation,
  getSheetAvailability,
  listJobOrders,
  listReservations,
  listSheets,
  releaseReservation,
  reserveSheet,
} from "../api/endpoints";
import { Card, Empty, ErrorNote, Spinner, StatusBadge, date } from "../components/ui";
import { errorText } from "../auth/AuthContext";

/**
 * Material reservations — the Sheet-Store hold ledger (Phase 9c family):
 * per-job holds with release/cancel/consume, the idempotent reserve, and
 * the live availability check (Balance − active holds).
 */
export default function ReservationsPage() {
  const [jobOrderId, setJobOrderId] = useState("");
  const [showReserve, setShowReserve] = useState(false);
  const [notice, setNotice] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const queryClient = useQueryClient();

  const jobs = useQuery({
    queryKey: ["job-orders", { pageSize: 100 }],
    queryFn: () => listJobOrders({ pageSize: 100 }),
  });

  const reservations = useQuery({
    queryKey: ["reservations", jobOrderId],
    queryFn: () => listReservations(jobOrderId),
    enabled: jobOrderId !== "",
  });

  const invalidate = async () => {
    await queryClient.invalidateQueries({ queryKey: ["reservations", jobOrderId] });
  };

  const guard = async (fn: () => Promise<unknown>, message: string) => {
    try {
      await fn();
      setError(null);
      setNotice(message);
      await invalidate();
    } catch (err) {
      setNotice(null);
      setError(errorText(err));
    }
  };

  return (
    <Card
      title="Material reservations"
      actions={
        <div className="filters">
          <select
            value={jobOrderId}
            onChange={(e) => {
              setJobOrderId(e.target.value);
              setNotice(null);
              setError(null);
            }}
          >
            <option value="">Choose a job order…</option>
            {(jobs.data?.items ?? []).map((job) => (
              <option key={job.id} value={job.id}>
                {job.jobOrderNo} — {job.customerName}
              </option>
            ))}
          </select>
          <button
            className="btn primary"
            disabled={jobOrderId === "" || showReserve}
            onClick={() => setShowReserve(true)}
          >
            Reserve sheet stock
          </button>
        </div>
      }
    >
      <p className="sub">
        Reserve is idempotent on the deterministic (job, spec, serial, sheet, session)
        key. Availability = sheet Balance (Stock − Used) − other active holds. Consumed
        material is terminal — it is never released or cancelled.
      </p>
      {notice && <div className="note ok">{notice}</div>}
      {error && <div className="note bad">{error}</div>}

      {showReserve && jobOrderId !== "" && (
        <ReserveForm
          jobOrderId={jobOrderId}
          onCancel={() => setShowReserve(false)}
          onReserved={async (message) => {
            setShowReserve(false);
            setError(null);
            setNotice(message);
            await invalidate();
          }}
        />
      )}

      {jobOrderId === "" ? (
        <Empty label="Pick a job order to see its hold ledger." />
      ) : (
        <>
          <ErrorNote error={reservations.error} />
          {reservations.isLoading ? (
            <Spinner />
          ) : (reservations.data?.length ?? 0) === 0 ? (
            <Empty label="No reservations for this job — the cutting workflow creates them automatically." />
          ) : (
            <table>
              <thead>
                <tr>
                  <th>Reservation key</th>
                  <th>Sheet</th>
                  <th>Spec</th>
                  <th className="num">Size (mm)</th>
                  <th className="num">Qty</th>
                  <th>Status</th>
                  <th>Notes</th>
                  <th>Released</th>
                  <th>Actions</th>
                </tr>
              </thead>
              <tbody>
                {reservations.data!.map((row) => (
                  <tr key={row.id}>
                    <td className="sub">{row.reservationKey}</td>
                    <td>{row.stockSizeLabel}</td>
                    <td>
                      #{row.specGroupNumber}/S{row.serialNumber} {row.glassRef}
                    </td>
                    <td className="num">
                      {row.widthMm}×{row.heightMm} · {row.thicknessMm}mm
                      {row.color ? ` · ${row.color}` : ""}
                    </td>
                    <td className="num">{row.quantity}</td>
                    <td>
                      <StatusBadge value={row.status} />
                    </td>
                    <td>{row.notes || "—"}</td>
                    <td>
                      {row.releasedAt
                        ? `${date(row.releasedAt)}${row.releaseReason ? ` (${row.releaseReason})` : ""}`
                        : "—"}
                    </td>
                    <td className="actions">
                      {row.status === "Active" && (
                        <>
                          <button
                            className="btn small"
                            onClick={() => {
                              const reason = window.prompt("Release reason (optional):") ?? "";
                              guard(
                                () => releaseReservation(row.id, reason || undefined),
                                `Released ${row.reservationKey}.`,
                              );
                            }}
                          >
                            Release
                          </button>
                          <button
                            className="btn small"
                            onClick={() => {
                              const reason = window.prompt("Cancel reason (optional):") ?? "";
                              guard(
                                () => cancelReservation(row.id, reason || undefined),
                                `Cancelled ${row.reservationKey}.`,
                              );
                            }}
                          >
                            Cancel
                          </button>
                          <button
                            className="btn small primary"
                            onClick={() =>
                              guard(
                                () => consumeReservation(row.id),
                                `Consumed ${row.reservationKey} (terminal).`,
                              )
                            }
                          >
                            Consume
                          </button>
                        </>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </>
      )}
    </Card>
  );
}

function ReserveForm(props: {
  jobOrderId: string;
  onCancel: () => void;
  onReserved: (message: string) => Promise<void>;
}) {
  const [sheetId, setSheetId] = useState("");
  const [specGroupNumber, setSpecGroupNumber] = useState("1");
  const [serialNumber, setSerialNumber] = useState("1");
  const [glassRef, setGlassRef] = useState("");
  const [specName, setSpecName] = useState("");
  const [quantity, setQuantity] = useState("1");
  const [notes, setNotes] = useState("");
  const [error, setError] = useState<string | null>(null);

  const sheets = useQuery({
    queryKey: ["stock-sheets", { pageSize: 100 }],
    queryFn: () => listSheets({ pageSize: 100 }),
  });

  const availability = useQuery({
    queryKey: ["sheet-availability", sheetId],
    queryFn: () => getSheetAvailability(sheetId),
    enabled: sheetId !== "",
  });

  const reserve = useMutation({
    mutationFn: () =>
      reserveSheet({
        jobOrderId: props.jobOrderId,
        glassSheetId: sheetId,
        specGroupNumber: Number(specGroupNumber),
        serialNumber: Number(serialNumber),
        glassRef: glassRef || undefined,
        specName: specName || undefined,
        quantity: Number(quantity),
        notes: notes || undefined,
      }),
    onSuccess: (row) =>
      props.onReserved(`Reserved ${row.quantity} on ${row.stockSizeLabel} (${row.reservationKey}).`),
    onError: (err) => setError(errorText(err)),
  });

  return (
    <form
      className="create-form"
      onSubmit={(e) => {
        e.preventDefault();
        reserve.mutate();
      }}
    >
      <h3>Reserve sheet stock</h3>
      <div className="form-grid">
        <label>
          Glass sheet
          <select value={sheetId} onChange={(e) => setSheetId(e.target.value)} required>
            <option value="">Choose a sheet…</option>
            {(sheets.data?.items ?? []).map((sheet) => (
              <option key={sheet.id} value={sheet.id}>
                {sheet.category} {sheet.thicknessMm}mm {sheet.widthMm}×{sheet.heightMm}
                {sheet.color ? ` ${sheet.color}` : ""} · balance {sheet.balance}
              </option>
            ))}
          </select>
        </label>
        {availability.data && (
          <div style={{ alignSelf: "end" }}>
            <span className="kv-label">Availability</span>
            <span className="sub">
              balance {availability.data.balance} − holds {availability.data.activeHolds} ={" "}
              <strong>{availability.data.available}</strong>
            </span>
          </div>
        )}
        <label>
          Spec group # *
          <input
            type="number"
            min={1}
            value={specGroupNumber}
            onChange={(e) => setSpecGroupNumber(e.target.value)}
            required
          />
        </label>
        <label>
          Serial # *
          <input
            type="number"
            min={1}
            value={serialNumber}
            onChange={(e) => setSerialNumber(e.target.value)}
            required
          />
        </label>
        <label>
          Glass ref
          <input value={glassRef} onChange={(e) => setGlassRef(e.target.value)} />
        </label>
        <label>
          Spec name
          <input value={specName} onChange={(e) => setSpecName(e.target.value)} />
        </label>
        <label>
          Quantity *
          <input
            type="number"
            min={1}
            value={quantity}
            onChange={(e) => setQuantity(e.target.value)}
            required
          />
        </label>
        <label>
          Notes
          <input value={notes} onChange={(e) => setNotes(e.target.value)} />
        </label>
      </div>
      {error && <div className="note bad">{error}</div>}
      <div className="form-actions">
        <button type="button" className="btn ghost" onClick={props.onCancel}>
          Cancel
        </button>
        <button
          type="submit"
          className="btn primary"
          disabled={reserve.isPending || sheetId === ""}
        >
          {reserve.isPending ? "Reserving…" : "Reserve"}
        </button>
      </div>
    </form>
  );
}

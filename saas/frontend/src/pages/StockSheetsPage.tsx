import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { listSheets } from "../api/endpoints";
import { Card, Empty, ErrorNote, Spinner, money, sqm } from "../components/ui";

const PAGE_SIZE = 20;

/** Stock sheets — GET /api/v1/stock/sheets (filters + paging + low stock). */
export default function StockSheetsPage() {
  const [search, setSearch] = useState("");
  const [color, setColor] = useState("");
  const [thicknessMm, setThicknessMm] = useState("");
  const [page, setPage] = useState(1);

  const query = useQuery({
    queryKey: ["sheets", { search, color, thicknessMm, page }],
    queryFn: () =>
      listSheets({
        search: search || undefined,
        color: color || undefined,
        thicknessMm: thicknessMm ? Number(thicknessMm) : undefined,
        page,
        pageSize: PAGE_SIZE,
      }),
    placeholderData: (prev) => prev,
  });

  const totalCount = query.data?.totalCount ?? 0;
  const pageCount = Math.max(1, Math.ceil(totalCount / PAGE_SIZE));

  return (
    <Card
      title="Glass sheets"
      actions={
        <div className="filters">
          <input
            placeholder="Search…"
            value={search}
            onChange={(e) => {
              setSearch(e.target.value);
              setPage(1);
            }}
          />
          <input
            placeholder="Color"
            value={color}
            onChange={(e) => {
              setColor(e.target.value);
              setPage(1);
            }}
          />
          <input
            type="number"
            placeholder="Thickness (mm)"
            value={thicknessMm}
            onChange={(e) => {
              setThicknessMm(e.target.value);
              setPage(1);
            }}
          />
        </div>
      }
    >
      <ErrorNote error={query.error} />
      {query.isLoading ? (
        <Spinner />
      ) : (query.data?.items.length ?? 0) === 0 ? (
        <Empty label="No sheet types match." />
      ) : (
        <>
          <table>
            <thead>
              <tr>
                <th>Sheet</th>
                <th>Supplier</th>
                <th className="num">Stock</th>
                <th className="num">Used</th>
                <th className="num">Balance</th>
                <th className="num">Balance m²</th>
                <th className="num">Buy / m²</th>
                <th className="num">Sell / m²</th>
              </tr>
            </thead>
            <tbody>
              {query.data!.items.map((sheet) => (
                <tr key={sheet.id} className={sheet.balance <= 0 ? "void" : undefined}>
                  <td>
                    {sheet.color} {sheet.thicknessMm}mm {sheet.category} —{" "}
                    {sheet.widthMm}×{sheet.heightMm}
                  </td>
                  <td>{sheet.supplier || "—"}</td>
                  <td className="num">{sheet.stock}</td>
                  <td className="num">{sheet.used}</td>
                  <td className="num">
                    <strong className={sheet.balance <= 0 ? "bad-text" : undefined}>
                      {sheet.balance}
                    </strong>
                  </td>
                  <td className="num">{sqm(sheet.totalSqmBalance)}</td>
                  <td className="num">{money(sheet.purchasePricePerSqm)}</td>
                  <td className="num">{money(sheet.sellPricePerSqm)}</td>
                </tr>
              ))}
            </tbody>
          </table>
          <div className="pager">
            <button
              className="btn ghost"
              disabled={page <= 1 || query.isFetching}
              onClick={() => setPage((p) => p - 1)}
            >
              ← Previous
            </button>
            <span>
              Page {page} / {pageCount} · {totalCount} sheet type(s)
            </span>
            <button
              className="btn ghost"
              disabled={page >= pageCount || query.isFetching}
              onClick={() => setPage((p) => p + 1)}
            >
              Next →
            </button>
          </div>
        </>
      )}
    </Card>
  );
}

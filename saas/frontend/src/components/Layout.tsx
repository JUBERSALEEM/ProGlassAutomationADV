import { NavLink, useNavigate } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { useAuth } from "../auth/AuthContext";
import { listLowStockSheets } from "../api/endpoints";

const NAV = [
  { to: "/", label: "Dashboard", end: true },
  { to: "/proformas", label: "Proformas", end: false },
    { to: "/cutting", label: "Cutting floor", end: false },
  { to: "/production-control", label: "Control room", end: false },
  { to: "/production-back-office", label: "Production office", end: false },
{ to: "/delivery-notes", label: "Delivery", end: false },
  { to: "/fiscal", label: "Fiscal", end: false },
  { to: "/reports", label: "Reports", end: false },
  { to: "/master-data", label: "Master data", end: false },
  { to: "/specifications", label: "Glass library", end: false },
  { to: "/job-orders", label: "Job Orders", end: false },
  { to: "/stock", label: "Stock", end: false },
];

export default function Layout({ children }: { children: React.ReactNode }) {
  const { session, logout } = useAuth();
  const navigate = useNavigate();

  const lowStock = useQuery({
    queryKey: ["low-stock"],
    queryFn: listLowStockSheets,
    staleTime: 60_000,
  });

  return (
    <div className="shell">
      <aside className="sidebar">
        <div className="brand">
          <span className="brand-mark">PG</span>
          <div>
            <div className="brand-name">ProGlass Cloud</div>
            <div className="brand-sub">{session?.tenantSlug ?? "—"}</div>
          </div>
        </div>
        <nav>
          {NAV.map((item) => (
            <NavLink key={item.to} to={item.to} end={item.end}>
              {item.label}
              {item.to === "/stock" && lowStock.data && lowStock.data.length > 0 && (
                <span className="badge warn" title="Low stock sheets">
                  {lowStock.data.length}
                </span>
              )}
            </NavLink>
          ))}
        </nav>
        <div className="sidebar-foot">
          <div className="user-chip" title={session?.role ?? ""}>
            <span className="avatar">{initials(session?.displayName)}</span>
            <div>
              <div className="user-name">{session?.displayName}</div>
              <div className="user-role">{session?.role}</div>
            </div>
          </div>
          <button
            className="btn ghost"
            onClick={async () => {
              await logout();
              navigate("/login");
            }}
          >
            Sign out
          </button>
        </div>
      </aside>
      <main className="content">{children}</main>
    </div>
  );
}

function initials(name: string | undefined): string {
  if (!name) return "?";
  return name
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0]!.toUpperCase())
    .join("");
}

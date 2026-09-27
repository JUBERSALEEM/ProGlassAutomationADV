import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { Navigate, Route, Routes } from "react-router-dom";
import { AuthProvider, useAuth } from "./auth/AuthContext";
import Layout from "./components/Layout";
import DashboardPage from "./pages/DashboardPage";
import JobOrderDetailPage from "./pages/JobOrderDetailPage";
import JobOrdersPage from "./pages/JobOrdersPage";
import LoginPage from "./pages/LoginPage";
import ProformaDetailPage from "./pages/ProformaDetailPage";
import DeliveryNoteDetailPage from "./pages/DeliveryNoteDetailPage";
import DeliveryNotesPage from "./pages/DeliveryNotesPage";
import FiscalPage from "./pages/FiscalPage";
import MasterDataPage from "./pages/MasterDataPage";
import ReportsPage from "./pages/ReportsPage";
import SpecificationsPage from "./pages/SpecificationsPage";
import ProformaInvoicesPage from "./pages/ProformaInvoicesPage";
import StockSheetsPage from "./pages/StockSheetsPage";

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      retry: (failureCount, error) => {
        // Never retry auth failures — the client already attempted one
        // token refresh before surfacing the 401.
        const status = (error as { status?: number }).status;
        if (status === 401 || status === 403) return false;
        return failureCount < 2;
      },
      refetchOnWindowFocus: false,
      staleTime: 15_000,
    },
  },
});

function Protected({ children }: { children: React.ReactNode }) {
  const { session } = useAuth();
  if (!session) return <Navigate to="/login" replace />;
  return <Layout>{children}</Layout>;
}

export default function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <AuthProvider>
        <Routes>
          <Route path="/login" element={<LoginPage />} />
          <Route
            path="/"
            element={
              <Protected>
                <DashboardPage />
              </Protected>
            }
          />
          <Route
            path="/proformas"
            element={
              <Protected>
                <ProformaInvoicesPage />
              </Protected>
            }
          />
          <Route
            path="/proformas/:id"
            element={
              <Protected>
                <ProformaDetailPage />
              </Protected>
            }
          />
          <Route
            path="/delivery-notes"
            element={
              <Protected>
                <DeliveryNotesPage />
              </Protected>
            }
          />
          <Route
            path="/delivery-notes/:id"
            element={
              <Protected>
                <DeliveryNoteDetailPage />
              </Protected>
            }
          />
          <Route
            path="/fiscal"
            element={
              <Protected>
                <FiscalPage />
              </Protected>
            }
          />
          <Route
            path="/reports"
            element={
              <Protected>
                <ReportsPage />
              </Protected>
            }
          />
          <Route
            path="/master-data"
            element={
              <Protected>
                <MasterDataPage />
              </Protected>
            }
          />
          <Route
            path="/specifications"
            element={
              <Protected>
                <SpecificationsPage />
              </Protected>
            }
          />
          <Route
            path="/job-orders"
            element={
              <Protected>
                <JobOrdersPage />
              </Protected>
            }
          />
          <Route
            path="/job-orders/:id"
            element={
              <Protected>
                <JobOrderDetailPage />
              </Protected>
            }
          />
          <Route
            path="/stock"
            element={
              <Protected>
                <StockSheetsPage />
              </Protected>
            }
          />
          <Route path="*" element={<Navigate to="/" replace />} />
        </Routes>
      </AuthProvider>
    </QueryClientProvider>
  );
}

import { ReportsAuthForm, type AuthSearchParams } from "@/components/reports/ReportsAuthForm";

export function CustomerAuthPage({
  mode,
  searchParams,
}: {
  mode: "password" | "otp";
  searchParams: AuthSearchParams;
}) {
  return (
    <main className="min-h-[calc(100svh-72px)] text-text-primary">
      <div className="mx-auto grid min-h-[calc(100svh-72px)] w-full max-w-7xl place-items-center px-5 py-12 sm:px-8 sm:py-16">
        <ReportsAuthForm
          key={mode}
          variant="account"
          defaultRedirect="/account"
          initialMode={mode}
          searchParams={searchParams}
        />
      </div>
    </main>
  );
}

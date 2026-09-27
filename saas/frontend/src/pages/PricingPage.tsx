import { useEffect, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { getPricingConfig, previewPricing, updatePricingConfig } from "../api/endpoints";
import type { PricingConfig } from "../api/types";
import { Card, ErrorNote, Spinner, money } from "../components/ui";
import { errorText } from "../auth/AuthContext";

/**
 * Pricing (the Step 4 module): the tenant-editable config (defaults + the
 * seven catalog lists + VAT) with the WPF panel defaults and fixed
 * catalogs shown read-only, and the rate calculator — a preview over the
 * four glass types with the server's warnings (yield fallback, unpriced
 * interlayer) rendered verbatim.
 */
export default function PricingPage() {
  const [tab, setTab] = useState<"settings" | "calculator">("settings");
  return (
    <Card
      title="Pricing"
      actions={
        <div className="tabs">
          {(
            [
              ["settings", "Settings"],
              ["calculator", "Calculator"],
            ] as ["settings" | "calculator", string][]
          ).map(([key, label]) => (
            <button
              key={key}
              className={`tab ${tab === key ? "active" : ""}`}
              onClick={() => setTab(key)}
            >
              {label}
            </button>
          ))}
        </div>
      }
    >
      {tab === "settings" ? <SettingsTab /> : <CalculatorTab />}
    </Card>
  );
}

const CATALOG_FIELDS: [keyof PricingConfig["catalogs"], string][] = [
  ["thicknessOptions", "Thickness options"],
  ["treatmentOptions", "Treatment options"],
  ["aspWidths", "ASP widths"],
  ["interlayerTypes", "Interlayer types"],
  ["interlayerThicknesses", "Interlayer thicknesses"],
  ["polishOptions", "Polish options"],
  ["panelExtraOptions", "Panel extra options"],
];

function SettingsTab() {
  const queryClient = useQueryClient();
  const config = useQuery({ queryKey: ["pricing-config"], queryFn: getPricingConfig });

  const [yieldFactor, setYieldFactor] = useState("");
  const [margin, setMargin] = useState("");
  const [vatRate, setVatRate] = useState("");
  const [currency, setCurrency] = useState("");
  const [catalogs, setCatalogs] = useState<Record<string, string>>({});
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [loaded, setLoaded] = useState(false);

  useEffect(() => {
    if (config.data && !loaded) {
      setYieldFactor(String(config.data.yieldFactorDefault));
      setMargin(String(config.data.marginDefault));
      setVatRate(String(config.data.vat.rate));
      setCurrency(config.data.vat.currency);
      setCatalogs(
        Object.fromEntries(
          CATALOG_FIELDS.map(([key]) => [key, config.data!.catalogs[key].join("\n")]),
        ),
      );
      setLoaded(true);
    }
  }, [config.data, loaded]);

  const save = useMutation({
    mutationFn: () =>
      updatePricingConfig({
        yieldFactorDefault: Number(yieldFactor),
        marginDefault: Number(margin),
        catalogs: Object.fromEntries(
          CATALOG_FIELDS.map(([key]) => [
            key,
            (catalogs[key] ?? "")
              .split("\n")
              .map((line) => line.trim())
              .filter((line) => line !== ""),
          ]),
        ),
        vat: { rate: Number(vatRate), currency },
      }),
    onSuccess: async () => {
      setError(null);
      setNotice("Pricing settings saved.");
      setLoaded(false);
      await queryClient.invalidateQueries({ queryKey: ["pricing-config"] });
    },
    onError: (err) => {
      setNotice(null);
      setError(errorText(err));
    },
  });

  if (config.isLoading) return <Spinner />;
  if (config.error) return <ErrorNote error={config.error} />;
  const data = config.data!;

  return (
    <>
      <p className="sub">
        Panel opening defaults and the WPF code lists are constants — shown read-only
        below. Only the defaults, the seven catalog lists and VAT are editable.
      </p>
      {notice && <div className="note ok">{notice}</div>}
      {error && <div className="note bad">{error}</div>}
      <form
        className="create-form"
        onSubmit={(e) => {
          e.preventDefault();
          save.mutate();
        }}
      >
        <h3>Tenant settings</h3>
        <div className="form-grid">
          <label>
            Yield factor default
            <input
              type="number"
              step="0.01"
              value={yieldFactor}
              onChange={(e) => setYieldFactor(e.target.value)}
              required
            />
          </label>
          <label>
            Margin default (%)
            <input
              type="number"
              step="0.01"
              value={margin}
              onChange={(e) => setMargin(e.target.value)}
              required
            />
          </label>
          <label>
            VAT rate (%)
            <input
              type="number"
              step="0.01"
              value={vatRate}
              onChange={(e) => setVatRate(e.target.value)}
              required
            />
          </label>
          <label>
            Currency
            <input value={currency} onChange={(e) => setCurrency(e.target.value)} required />
          </label>
        </div>
        <h3 style={{ marginTop: 8 }}>Catalog lists (one option per line)</h3>
        <div className="form-grid">
          {CATALOG_FIELDS.map(([key, label]) => (
            <label key={key}>
              {label}
              <textarea
                rows={4}
                value={catalogs[key] ?? ""}
                onChange={(e) => setCatalogs((prev) => ({ ...prev, [key]: e.target.value }))}
              />
            </label>
          ))}
        </div>
        <div className="form-actions">
          <button type="submit" className="btn primary" disabled={save.isPending}>
            {save.isPending ? "Saving…" : "Save settings"}
          </button>
        </div>
      </form>

      <h3 style={{ margin: "16px 0 6px" }}>Panel opening defaults (read-only, WPF verbatim)</h3>
      <div className="kv-grid">
        {(
          [
            ["SGU", data.panelDefaults.sgu],
            ["DGU", data.panelDefaults.dgu],
            ["LAM", data.panelDefaults.lam],
            ["DGU+LAM", data.panelDefaults.dguLam],
          ] as [string, Record<string, number | string>][]
        ).map(([name, panel]) => (
          <div key={name}>
            <span className="kv-label">{name}</span>
            <span className="sub">
              {Object.entries(panel)
                .map(([k, v]) => `${k}=${v}`)
                .join(" · ")}
            </span>
          </div>
        ))}
      </div>
      <p className="sub" style={{ marginTop: 8 }}>
        Spacer material surcharges:{" "}
        {Object.entries(data.panelDefaults.spacerMaterialSurcharges)
          .map(([k, v]) => `${k} +${v}`)
          .join(" · ") || "—"}
      </p>

      <h3 style={{ margin: "16px 0 6px" }}>Fixed catalogs (read-only, WPF code lists)</h3>
      <div className="kv-grid">
        <div>
          <span className="kv-label">Spacer widths</span>
          <span className="sub">{data.fixedCatalogs.spacerWidthOptions.join(", ")}</span>
        </div>
        <div>
          <span className="kv-label">Spacer materials</span>
          <span className="sub">{data.fixedCatalogs.spacerMaterialOptions.join(", ")}</span>
        </div>
        <div>
          <span className="kv-label">Spacer extras</span>
          <span className="sub">{data.fixedCatalogs.spacerExtraOptions.join(", ")}</span>
        </div>
        <div>
          <span className="kv-label">Edge polish</span>
          <span className="sub">{data.fixedCatalogs.edgePolishOptions.join(", ")}</span>
        </div>
        <div>
          <span className="kv-label">PVB colours</span>
          <span className="sub">{data.fixedCatalogs.pvbColorOptions.join(", ")}</span>
        </div>
        <div>
          <span className="kv-label">Interlayer colours</span>
          <span className="sub">{data.fixedCatalogs.interlayerColorOptions.join(", ")}</span>
        </div>
      </div>
      <p className="sub" style={{ marginTop: 8 }}>
        Interlayer prices:{" "}
        {Object.entries(data.fixedCatalogs.interlayerPrices)
          .map(([k, v]) => `${k} buy ${v.buy} / sell ${v.sell}`)
          .join(" · ") || "—"}
      </p>
    </>
  );
}

const GLASS_TYPES = ["SGU", "DGU", "LAM", "DGU+LAM"] as const;

function CalculatorTab() {
  const config = useQuery({ queryKey: ["pricing-config"], queryFn: getPricingConfig });
  const [glassType, setGlassType] = useState<string>("SGU");
  const [values, setValues] = useState<Record<string, string>>({});
  const [error, setError] = useState<string | null>(null);
  const [prefilled, setPrefilled] = useState("");

  // Prefill from the server's panel defaults when the type changes.
  const prefill = (type: string) => {
    if (!config.data) return;
    const panels = config.data.panelDefaults;
    const panel: Record<string, number | string> =
      type === "SGU"
        ? panels.sgu
        : type === "DGU"
          ? panels.dgu
          : type === "LAM"
            ? panels.lam
            : panels.dguLam;
    setValues(Object.fromEntries(Object.entries(panel).map(([k, v]) => [k, String(v)])));
    setPrefilled(type);
  };

  useEffect(() => {
    prefill(glassType);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [glassType, config.data]);

  const preview = useMutation({
    mutationFn: () => {
      const body: Record<string, unknown> = { glassType };
      const numeric = Object.fromEntries(
        Object.entries(values)
          .filter(([key]) => key !== "interlayerType")
          .map(([key, v]) => [key, Number(v)]),
      );
      if (glassType === "SGU") body.sgu = numeric;
      if (glassType === "DGU") body.dgu = numeric;
      if (glassType === "LAM") body.lam = numeric;
      if (glassType === "DGU+LAM")
        body.dguLam = { ...numeric, interlayerType: values.interlayerType ?? "" };
      return previewPricing(body);
    },
    onSuccess: () => setError(null),
    onError: (err) => setError(errorText(err)),
  });

  if (config.isLoading) return <Spinner />;
  if (config.error) return <ErrorNote error={config.error} />;

  const fields = Object.keys(values);

  return (
    <>
      <p className="sub">
        A Step 4 estimate: the yield factor divides the GLASS term only; processing,
        interlayer and spacer are added after it; the margin multiplies everything. A
        yield ≤ 0 falls back to 1.0 WITH a warning; an unpriced interlayer is called out —
        never silently mispriced.
      </p>
      <form
        className="create-form"
        onSubmit={(e) => {
          e.preventDefault();
          preview.mutate();
        }}
      >
        <h3>
          Calculator {prefilled === glassType && "(prefilled with the panel defaults)"}
        </h3>
        <div className="form-grid">
          <label>
            Glass type
            <select value={glassType} onChange={(e) => setGlassType(e.target.value)}>
              {GLASS_TYPES.map((type) => (
                <option key={type}>{type}</option>
              ))}
            </select>
          </label>
          {fields.map((field) => (
            <label key={field}>
              {labelize(field)}
              {field === "interlayerType" ? (
                <input
                  value={values[field] ?? ""}
                  onChange={(e) =>
                    setValues((prev) => ({ ...prev, [field]: e.target.value }))
                  }
                />
              ) : (
                <input
                  type="number"
                  step="0.01"
                  value={values[field] ?? ""}
                  onChange={(e) =>
                    setValues((prev) => ({ ...prev, [field]: e.target.value }))
                  }
                  required
                />
              )}
            </label>
          ))}
        </div>
        {error && <div className="note bad">{error}</div>}
        <div className="form-actions">
          <button
            type="button"
            className="btn ghost"
            onClick={() => prefill(glassType)}
          >
            Reset to defaults
          </button>
          <button type="submit" className="btn primary" disabled={preview.isPending}>
            {preview.isPending ? "Calculating…" : "Preview rate"}
          </button>
        </div>
      </form>

      {preview.data && (
        <div className="stats" style={{ marginTop: 12 }}>
          <div className="stat">
            <span className="stat-label">Rate / sqm ({preview.data.glassType})</span>
            <span className="stat-value">{money(preview.data.ratePerSqm)}</span>
          </div>
          <div className="stat">
            <span className="stat-label">Cost basis / sqm (pre-margin)</span>
            <span className="stat-value">{money(preview.data.costBasisPerSqm)}</span>
          </div>
        </div>
      )}
      {preview.data && preview.data.warnings.length > 0 && (
        <div className="note bad" style={{ marginTop: 8 }}>
          {preview.data.warnings.map((warning, index) => (
            <div key={index}>⚠ {warning}</div>
          ))}
        </div>
      )}
    </>
  );
}

function labelize(field: string): string {
  const withSpaces = field.replace(/([A-Z])/g, " $1").toLowerCase().trim();
  return withSpaces.charAt(0).toUpperCase() + withSpaces.slice(1);
}

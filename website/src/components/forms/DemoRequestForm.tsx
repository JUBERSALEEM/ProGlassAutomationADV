"use client";

import { useState } from "react";
import { cn } from "@/lib/cn";
import { Button } from "@/components/ui/Button";
import { site } from "@/lib/site";
import { useT } from "@/i18n/provider";

type FieldName = "name" | "company" | "email" | "phone" | "country" | "businessType" | "message";
/** Not a real field: the honeypot the server uses to recognise a bot. */
type TrapName = "website";
type FormFields = FieldName | TrapName;

type Values = Record<FormFields, string>;

const initialValues: Values = {
  name: "",
  company: "",
  email: "",
  phone: "",
  country: "",
  businessType: "",
  message: "",
  website: "",
};

const businessTypes = [
  "Glass fabrication / processing",
  "Glazing contractor / installer",
  "Aluminium & glass contracting",
  "Glass trading / distribution",
  "Architectural / project consultancy",
  "Other",
];

type Status = "idle" | "submitting" | "success" | "error";

/**
 * Demo request form.
 *
 * There is no mail transport wired into this build. Rather than showing a
 * fake success state, `submit` posts to `/api/demo-request`, which validates
 * the payload and reports plainly whether a transport is configured. When no
 * transport exists the form tells the visitor exactly what to do instead —
 * and the same message is logged server-side so the integration point is
 * impossible to miss.
 */
export function DemoRequestForm() {
  const t = useT();
  const [values, setValues] = useState<Values>(initialValues);
  // `Values` is inferred from `initialValues`, so the trap field is part of it.
  const [errors, setErrors] = useState<Partial<Record<FormFields, string>>>({});
  const [status, setStatus] = useState<Status>("idle");
  const [serverMessage, setServerMessage] = useState<string>("");

  const set = (field: FormFields) => (event: React.ChangeEvent<HTMLInputElement | HTMLTextAreaElement | HTMLSelectElement>) => {
    setValues((v) => ({ ...v, [field]: event.target.value }));
    setErrors((e) => (e[field] ? { ...e, [field]: undefined } : e));
  };

  function validate(v: Values) {
    const next: Partial<Record<FieldName, string>> = {};
    if (v.name.trim().length < 2) next.name = "Please enter your name.";
    if (v.company.trim().length < 2) next.company = "Please enter your company name.";
    if (!/^[^\s@]+@[^\s@]+\.[^\s@]{2,}$/.test(v.email.trim())) next.email = "Please enter a valid email address.";
    if (v.country.trim().length < 2) next.country = "Please enter your country.";
    if (v.businessType.trim().length === 0) next.businessType = "Please choose your business type.";
    if (v.message.trim().length < 10) next.message = "A sentence or two about what you need helps us reply usefully.";
    return next;
  }

  async function onSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const found = validate(values);
    setErrors(found);
    if (Object.keys(found).length > 0) {
      setStatus("idle");
      const first = document.querySelector<HTMLElement>(`[data-field="${Object.keys(found)[0]}"]`);
      first?.focus();
      return;
    }

    setStatus("submitting");
    setServerMessage("");

    try {
      const response = await fetch("/api/demo-request", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(values),
      });
      const payload: { ok?: boolean; message?: string; configured?: boolean } = await response.json();

      if (response.ok && payload.ok) {
        setStatus("success");
        setValues(initialValues);
        return;
      }

      setStatus("error");
      setServerMessage(
        payload.message ??
          "The request could not be delivered from this deployment. Please email proglassautomation@gmail.com directly.",
      );
    } catch {
      setStatus("error");
      setServerMessage(
        "The request could not reach the server. Please email proglassautomation@gmail.com directly — the address is monitored.",
      );
    }
  }

  if (status === "success") {
    return (
      <div className="surface-raised p-8" role="status">
        <span
          aria-hidden="true"
          className="grid h-11 w-11 place-items-center rounded-xl border border-state-success/40 bg-state-success/10 text-state-success"
        >
          <svg viewBox="0 0 20 20" className="h-5 w-5" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round">
            <path d="M4 10.5 8 14.5 16 6" />
          </svg>
        </span>
        <h3 className="mt-5 text-lg font-semibold text-text-primary">Request received</h3>
        <p className="mt-2 text-sm leading-relaxed text-text-muted">
          {t("Thank you. Your request has been recorded and will be answered by email. If you would rather not wait, write")}
          to{" "}
          <a className="link-underline" href="mailto:proglassautomation@gmail.com">
            proglassautomation@gmail.com
          </a>{" "}
          directly.
        </p>
        <Button variant="secondary" size="sm" className="mt-6" onClick={() => setStatus("idle")}>
          {t("Send another request")}
        </Button>
      </div>
    );
  }

  return (
    <form onSubmit={onSubmit} noValidate className="surface-raised p-6 sm:p-8">
      {/*
        Honeypot. A field a human cannot reach — off-screen rather than
        `display:none`, because the simplest bots skip hidden inputs — and
        labelled as something only an automated filler would want. It is
        excluded from assistive technology and from the tab order, so it cannot
        cost a real visitor anything.
      */}
      <div aria-hidden="true" className="pointer-events-none absolute -left-[9999px] h-0 w-0 overflow-hidden">
        <label htmlFor="website">{t("Website (leave this empty)")}</label>
        <input
          id="website"
          name="website"
          type="text"
          tabIndex={-1}
          autoComplete="off"
          value={values.website}
          onChange={set("website")}
        />
      </div>

      <div className="grid gap-5 sm:grid-cols-2">
        <Field label={t("Name")} name="name" value={values.name} onChange={set("name")} error={errors.name} required autoComplete="name" />
        <Field label={t("Company")} name="company" value={values.company} onChange={set("company")} error={errors.company} required autoComplete="organization" />
        <Field
          label={t("Email")}
          name="email"
          type="email"
          value={values.email}
          onChange={set("email")}
          error={errors.email}
          required
          autoComplete="email"
        />
        <Field
          label={t("Phone / WhatsApp")}
          name="phone"
          type="tel"
          value={values.phone}
          onChange={set("phone")}
          error={errors.phone}
          hint="Optional"
          autoComplete="tel"
        />
        <Field label={t("Country")} name="country" value={values.country} onChange={set("country")} error={errors.country} required autoComplete="country-name" />

        <div data-field="businessType">
          <label htmlFor="businessType" className="block text-sm font-medium text-text-secondary">
            Business type <span className="text-state-danger">*</span>
          </label>
          <select
            id="businessType"
            name="businessType"
            value={values.businessType}
            onChange={set("businessType")}
            aria-invalid={Boolean(errors.businessType)}
            aria-describedby={errors.businessType ? "businessType-error" : undefined}
            className={cn(
              "mt-2 h-11 w-full rounded-xl border bg-ink-950/60 px-3.5 text-sm text-text-primary transition-colors duration-200",
              errors.businessType ? "border-state-danger/70" : "border-line-strong/70 focus:border-brand-400/70",
            )}
          >
            <option value="">Select…</option>
            {businessTypes.map((type) => (
              <option key={type} value={type} className="bg-ink-900">
                {type}
              </option>
            ))}
          </select>
          {errors.businessType ? (
            <p id="businessType-error" className="mt-1.5 text-xs text-state-danger">
              {errors.businessType}
            </p>
          ) : null}
        </div>
      </div>

      <div className="mt-5" data-field="message">
        <label htmlFor="message" className="block text-sm font-medium text-text-secondary">
          What would you like to see? <span className="text-state-danger">*</span>
        </label>
        <textarea
          id="message"
          name="message"
          rows={5}
          value={values.message}
          onChange={set("message")}
          aria-invalid={Boolean(errors.message)}
          aria-describedby={errors.message ? "message-error" : "message-hint"}
          className={cn(
            "mt-2 w-full rounded-xl border bg-ink-950/60 px-3.5 py-3 text-sm text-text-primary transition-colors duration-200",
            errors.message ? "border-state-danger/70" : "border-line-strong/70 focus:border-brand-400/70",
          )}
          placeholder={t("e.g. We process roughly 40 jobs a week and want to see how the proforma wizard handles a multi-section DGU project.")}
        />
        {errors.message ? (
          <p id="message-error" className="mt-1.5 text-xs text-state-danger">
            {errors.message}
          </p>
        ) : (
          <p id="message-hint" className="mt-1.5 text-xs text-text-faint font-medium">
            {t("Mention your glass types, how many people would use it, and whether cutting optimization matters to you.")}
          </p>
        )}
      </div>

      {status === "error" ? (
        <div role="alert" className="mt-5 rounded-xl border border-state-danger/40 bg-state-danger/10 p-4">
          <p className="text-sm font-medium text-state-danger">The request was not sent</p>
          <p className="mt-1 text-xs leading-relaxed text-text-secondary">{serverMessage}</p>
          <p className="mt-2 text-xs leading-relaxed text-text-secondary">
            You can also write directly to{" "}
            <a className="link-underline" href={`mailto:${site.developerEmail}`}>
              {site.developerEmail}
            </a>
            .
          </p>
        </div>
      ) : null}

      <div className="mt-6 flex flex-wrap items-center gap-4">
        <Button type="submit" size="lg" loading={status === "submitting"} disabled={status === "submitting"}>
          {status === "submitting" ? "Sending…" : t("Send request")}
        </Button>
        <p className="text-xs leading-relaxed text-text-faint font-medium">
          {t("We reply by email. No account is created, and nothing you type here is stored on this website.")}
        </p>
      </div>
    </form>
  );
}

function Field({
  label,
  name,
  value,
  onChange,
  error,
  type = "text",
  required,
  hint,
  autoComplete,
}: {
  label: string;
  name: FieldName;
  value: string;
  onChange: (event: React.ChangeEvent<HTMLInputElement>) => void;
  error?: string;
  type?: string;
  required?: boolean;
  hint?: string;
  autoComplete?: string;
}) {
  return (
    <div data-field={name}>
      <label htmlFor={name} className="flex items-baseline gap-2 text-sm font-medium text-text-secondary">
        {label}
        {required ? <span className="text-state-danger">*</span> : null}
        {hint ? <span className="text-xs font-normal text-text-faint font-medium">{hint}</span> : null}
      </label>
      <input
        id={name}
        name={name}
        type={type}
        value={value}
        onChange={onChange}
        required={required}
        autoComplete={autoComplete}
        aria-invalid={Boolean(error)}
        aria-describedby={error ? `${name}-error` : undefined}
        className={cn(
          "mt-2 h-11 w-full rounded-xl border bg-ink-950/60 px-3.5 text-sm text-text-primary transition-colors duration-200 placeholder:text-text-faint",
          error ? "border-state-danger/70" : "border-line-strong/70 focus:border-brand-400/70",
        )}
      />
      {error ? (
        <p id={`${name}-error`} className="mt-1.5 text-xs text-state-danger">
          {error}
        </p>
      ) : null}
    </div>
  );
}

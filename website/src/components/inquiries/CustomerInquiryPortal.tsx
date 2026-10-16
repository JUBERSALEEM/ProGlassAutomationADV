"use client";

import { useEffect, useRef, useState } from "react";
import { LocaleLink } from "@/components/ui/LocaleLink";
import { useOperatorSession } from "@/lib/useOperatorSession";
import type { CustomerInquiryListItem, CustomerInquirySubmission } from "@/types/inquiry-submissions";

const ACCEPT = ".pdf,.jpg,.jpeg,.png,.gif,.webp,.bmp,.xlsx,.csv";
const MAX_FILES = 15;
const MAX_BYTES = 30 * 1024 * 1024;

function date(value: string) { const parsed = new Date(value); return Number.isNaN(parsed.getTime()) ? "—" : parsed.toLocaleDateString(); }
function filesLabel(count:number){return `${count} document${count===1?"":"s"}`;}

export function CustomerInquiryPortal() {
  const { checking, authToken, isAuthRequired, userEmail, signOut, requireAuth } = useOperatorSession();
  const formRef = useRef<HTMLFormElement>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);
  const replaceFileInputRef = useRef<HTMLInputElement>(null);
  const replaceIndexRef = useRef<number | null>(null);
  const [selectedFiles, setSelectedFiles] = useState<File[]>([]);
  const [dragging, setDragging] = useState(false);
  const [rows, setRows] = useState<CustomerInquiryListItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState("");
  const [notice, setNotice] = useState("");

  async function load() {
    if (!authToken) return;
    setLoading(true);
    try {
      const response = await fetch("/api/inquiry-submissions", { headers: { Authorization: `Bearer ${authToken}` }, cache: "no-store" });
      const body = await response.json();
      if (response.status === 401) { requireAuth(); return; }
      if (!response.ok || !body.ok) throw new Error(body?.error?.message || "Unable to load your inquiry history.");
      setRows(body.data);
    } catch (reason) { setError(reason instanceof Error ? reason.message : "Unable to load your inquiry history."); }
    finally { setLoading(false); }
  }

  useEffect(() => { if (!checking && authToken) queueMicrotask(() => void load()); }, [authToken, checking]); // eslint-disable-line react-hooks/exhaustive-deps

  function validFiles(input: FileList | File[]): File[] | null {
    const files = Array.from(input);
    const oversized = files.find((file) => file.size > MAX_BYTES);
    if (oversized) { setError(`${oversized.name} exceeds the 30 MB per-file limit.`); return null; }
    return files;
  }

  function addFiles(input: FileList | File[]) {
    const files = validFiles(input);
    if (!files) return;
    setSelectedFiles((current) => {
      const unique = files.filter((file) => !current.some((existing) => existing.name === file.name && existing.size === file.size && existing.lastModified === file.lastModified));
      if (current.length + unique.length > MAX_FILES) { setError(`You can attach up to ${MAX_FILES} documents. Remove a file before adding another.`); return current; }
      setError("");
      return [...current, ...unique];
    });
  }

  function replaceFile(file: File) {
    const files = validFiles([file]);
    const index = replaceIndexRef.current;
    if (!files || index === null) return;
    setSelectedFiles((current) => current.map((existing, position) => position === index ? files[0]! : existing));
    setError(""); replaceIndexRef.current = null;
  }

  async function submit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault(); setError(""); setNotice("");
    if (!authToken) { requireAuth(); return; }
    const form = new FormData(event.currentTarget);
    form.delete("documents");
    for (const file of selectedFiles) form.append("documents", file);
    const files = selectedFiles;
    if (files.length > MAX_FILES) { setError(`Select at most ${MAX_FILES} documents.`); return; }
    const oversized = files.find((file) => file.size > MAX_BYTES);
    if (oversized) { setError(`${oversized.name} exceeds the 30 MB per-file limit.`); return; }
    setSubmitting(true);
    try {
      const response = await fetch("/api/inquiry-submissions", { method: "POST", headers: { Authorization: `Bearer ${authToken}` }, body: form });
      const body = await response.json();
      if (response.status === 401) { requireAuth(); return; }
      if (!response.ok || !body.ok) throw new Error(body?.error?.message || "The inquiry could not be submitted.");
      const created = body.data as CustomerInquirySubmission;
      setNotice(`Inquiry ${created.submission_reference} was submitted securely.`);
      formRef.current?.reset();
      setSelectedFiles([]);
      await load();
    } catch (reason) { setError(reason instanceof Error ? reason.message : "The inquiry could not be submitted."); }
    finally { setSubmitting(false); }
  }

  if (checking) return <div role="status" className="grid min-h-[55vh] place-items-center bg-ink-950 text-xs text-text-muted">Checking your secure session…</div>;
  if (isAuthRequired || !authToken) return <main className="min-h-[65vh] bg-ink-950 px-4 py-20 text-text-primary"><section className="mx-auto max-w-md rounded-2xl border border-line-strong bg-ink-900 p-6 text-center"><h1 className="text-xl font-bold">Sign in to submit an inquiry</h1><p className="mt-2 text-xs text-text-muted">Email verification protects your inquiry history and private documents.</p><LocaleLink href="/reports/login?redirect=/inquiries/submit" className="mt-5 inline-flex rounded-xl bg-brand-400 px-5 py-3 text-sm font-bold text-accent-ink">Continue with email</LocaleLink></section></main>;

  return <main className="min-h-screen bg-ink-950 pb-20 text-text-primary">
    <header className="border-b border-line-strong/60"><div className="shell flex flex-wrap items-center justify-between gap-3 py-6"><div><div className="eyebrow text-brand-300">Secure customer inquiry</div><h1 className="mt-1 text-2xl font-bold">Submit project documents</h1><p className="mt-1 text-xs text-text-muted">PDF, images, XLSX and CSV · up to 30 MB each · 15 files maximum</p></div><div className="text-right"><div className="text-3xs text-text-faint">{userEmail}</div><button type="button" onClick={() => void signOut()} className="mt-2 text-xs font-semibold text-brand-300">Sign out</button></div></div></header>
    <div className="shell mt-6 grid gap-6 lg:grid-cols-[minmax(0,1.1fr)_minmax(20rem,.9fr)]">
      <section className="rounded-2xl border border-line-strong/60 bg-ink-900/85 p-5"><h2 className="text-lg font-bold">New inquiry</h2><p className="mt-1 text-xs text-text-muted">Provide enough information for the estimating team to review your request.</p>
        {error && <div role="alert" className="mt-4 rounded-xl border border-state-danger/40 bg-state-danger/10 p-3 text-xs text-state-danger">{error}</div>}
        {notice && <div role="status" className="mt-4 rounded-xl border border-state-success/40 bg-state-success/10 p-3 text-xs text-state-success">{notice}</div>}
        <form ref={formRef} onSubmit={submit} className="mt-5 grid gap-4 sm:grid-cols-2">
          <Field name="contact_name" label="Contact name" required maxLength={120} /><Field name="company_name" label="Company" required maxLength={160} />
          <Field name="phone" label="Phone" maxLength={60} /><Field name="customer_reference" label="Your reference" maxLength={100} />
          <Field name="attention_name" label="Attention" maxLength={120} /><Field name="vat_trn_number" label="VAT / TRN number" maxLength={50} />
          <Field name="salesman_reference" label="Salesman reference" maxLength={120} /><Field name="handover_estimator_name" label="Handover estimator name" maxLength={120} />
          <Field name="project_name" label="Project name" required maxLength={180} /><Field name="project_location" label="Project location" maxLength={180} />
          <label htmlFor="inquiry-description" className="sm:col-span-2 text-sm font-semibold leading-5 text-text-secondary">Requirements<textarea id="inquiry-description" name="description" required minLength={10} maxLength={5000} rows={6} className="mt-2 min-h-36 w-full rounded-xl border border-line-strong bg-ink-950 p-3 text-base leading-6 text-text-primary caret-brand-300 outline-none focus-visible:border-brand-400 focus-visible:ring-2 focus-visible:ring-brand-400/40 sm:text-sm" /></label>
          <div className="sm:col-span-2"><div className="flex items-center justify-between gap-3"><div id="inquiry-documents-label" className="text-sm font-semibold leading-5 text-text-secondary">Documents</div>{selectedFiles.length>0&&<button type="button" onClick={()=>setSelectedFiles([])} className="rounded-lg border border-line-strong px-3 py-1.5 text-xs font-semibold text-state-danger hover:bg-state-danger/10 focus-visible:ring-2 focus-visible:ring-state-danger/40">Remove all</button>}</div><input ref={fileInputRef} id="inquiry-documents" aria-labelledby="inquiry-documents-label" aria-describedby="inquiry-documents-help" name="documents" type="file" multiple accept={ACCEPT} onChange={(event)=>{if(event.target.files)addFiles(event.target.files);event.target.value="";}} className="sr-only" /><input ref={replaceFileInputRef} aria-label="Choose a replacement document" type="file" accept={ACCEPT} onChange={(event)=>{const file=event.target.files?.[0];if(file)replaceFile(file);event.target.value="";}} className="sr-only" />
            <button type="button" aria-labelledby="inquiry-documents-label inquiry-document-picker-copy" aria-describedby="inquiry-documents-help" onClick={()=>fileInputRef.current?.click()} onDragEnter={(event)=>{event.preventDefault();setDragging(true);}} onDragOver={(event)=>event.preventDefault()} onDragLeave={()=>setDragging(false)} onDrop={(event)=>{event.preventDefault();setDragging(false);addFiles(event.dataTransfer.files);}} className={`mt-2 w-full rounded-2xl border-2 border-dashed p-5 text-center outline-none transition focus-visible:border-brand-300 focus-visible:ring-2 focus-visible:ring-brand-400/50 sm:p-6 ${dragging?"border-brand-300 bg-brand-500/15":"border-line-strong bg-ink-950/70 hover:border-brand-400/70 hover:bg-ink-950"}`}>
              <span aria-hidden="true" className="mx-auto grid h-11 w-11 place-items-center rounded-xl bg-brand-500/15 text-xl text-brand-300">+</span><span id="inquiry-document-picker-copy" className="mt-3 block text-sm font-bold leading-5 text-text-primary">{selectedFiles.length ? "Add more documents" : "Choose documents or drop them here"}</span><span id="inquiry-documents-help" className="mt-1 block text-xs leading-5 text-text-muted">PDF, images, XLSX or CSV · 30 MB each · {selectedFiles.length}/{MAX_FILES} selected</span>
            </button>
            {selectedFiles.length>0&&<div aria-live="polite" aria-label={`${selectedFiles.length} documents selected`} className="mt-3 grid gap-2 sm:grid-cols-2">{selectedFiles.map((file,index)=><div key={`${file.name}-${file.size}-${index}`} className="flex items-center justify-between gap-3 rounded-xl border border-line-strong bg-ink-950 px-3 py-2"><div className="min-w-0"><div className="truncate text-xs font-semibold text-text-primary">{file.name}</div><div className="text-3xs text-text-faint">{Math.ceil(file.size/1024).toLocaleString()} KB · ready to attach</div></div><div className="flex shrink-0 gap-1"><button type="button" aria-label={`Replace ${file.name}`} onClick={()=>{replaceIndexRef.current=index;replaceFileInputRef.current?.click();}} className="rounded-lg px-2 py-1 text-xs font-semibold text-brand-300 hover:bg-brand-500/10 focus-visible:ring-2 focus-visible:ring-brand-400/40">Replace</button><button type="button" aria-label={`Remove ${file.name}`} onClick={()=>setSelectedFiles((current)=>current.filter((_,position)=>position!==index))} className="rounded-lg px-2 py-1 text-xs font-semibold text-state-danger hover:bg-state-danger/10 focus-visible:ring-2 focus-visible:ring-state-danger/40">Remove</button></div></div>)}</div>}
            <span className="mt-2 block text-3xs text-text-faint">Files stay private and are checked by content, not filename alone. CAD files must be exported to PDF or image.</span>
          </div>
          <button disabled={submitting} className="sm:col-span-2 rounded-xl bg-brand-400 px-5 py-3 text-sm font-bold text-accent-ink disabled:opacity-50">{submitting ? `Uploading and verifying ${filesLabel(selectedFiles.length)}…` : "Submit inquiry"}</button>
        </form>
      </section>
      <section className="rounded-2xl border border-line-strong/60 bg-ink-900/85 p-5"><div className="flex items-center justify-between"><h2 className="text-lg font-bold">Your submissions</h2><button onClick={() => void load()} className="text-xs font-semibold text-brand-300">Refresh</button></div>
        {loading ? <p role="status" className="mt-5 text-xs text-text-muted">Loading…</p> : rows.length === 0 ? <p className="mt-5 text-xs text-text-muted">No website inquiries submitted by this account.</p> : <div className="mt-4 space-y-3">{rows.map((row) => <LocaleLink key={row.id} href={`/inquiries/submissions/${row.id}`} className="block rounded-xl border border-line-strong bg-ink-950/70 p-4"><div className="flex justify-between gap-3"><span className="font-mono text-xs font-bold text-brand-300">{row.submission_reference}</span><span className="rounded-full border border-line-strong px-2 py-0.5 text-3xs uppercase text-text-muted">{row.status.replaceAll("_", " ")}</span></div><div className="mt-2 text-sm font-semibold">{row.project_name}</div><div className="mt-1 text-3xs text-text-faint">{row.document_count} document{row.document_count === 1 ? "" : "s"} · {date(row.submitted_at)}</div></LocaleLink>)}</div>}
      </section>
    </div>
  </main>;
}

function Field({ name, label, required, maxLength }: { name: string; label: string; required?: boolean; maxLength: number }) {
  const id = `inquiry-${name.replaceAll("_", "-")}`;
  return <label htmlFor={id} className="text-sm font-semibold leading-5 text-text-secondary">{label}<input id={id} name={name} required={required} maxLength={maxLength} className="mt-2 min-h-12 w-full rounded-xl border border-line-strong bg-ink-950 px-3 py-3 text-base leading-6 text-text-primary caret-brand-300 outline-none placeholder:text-text-faint focus-visible:border-brand-400 focus-visible:ring-2 focus-visible:ring-brand-400/40 sm:text-sm" /></label>;
}

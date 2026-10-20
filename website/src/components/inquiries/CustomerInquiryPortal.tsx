"use client";

import { useEffect, useRef, useState } from "react";
import { LocaleLink } from "@/components/ui/LocaleLink";
import { useOperatorSession } from "@/lib/useOperatorSession";
import type { CustomerInquiryListItem, CustomerInquiryProfile, CustomerInquiryProject, CustomerInquirySubmission } from "@/types/inquiry-submissions";

const ACCEPT = ".pdf,.jpg,.jpeg,.png,.gif,.webp,.bmp,.xlsx,.csv";
const MAX_FILES = 15;
const MAX_BYTES = 30 * 1024 * 1024;

function date(value: string) { const parsed = new Date(value); return Number.isNaN(parsed.getTime()) ? "—" : parsed.toLocaleDateString(); }
function filesLabel(count:number){return `${count} document${count===1?"":"s"}`;}
type UploadState = { status: "waiting" | "uploading" | "verifying" | "complete" | "failed"; percent: number; detail: string };
function uploadDocument(token: string, draftId: string, file: File, progress: (loaded: number, total: number) => void, verifying: () => void) {
  return new Promise<void>((resolve, reject) => {
    const request = new XMLHttpRequest();
    request.open("POST", `/api/inquiry-submissions/${draftId}/documents`);
    request.setRequestHeader("Authorization", `Bearer ${token}`);
    request.upload.onprogress = (event) => progress(event.loaded, event.lengthComputable ? event.total : file.size);
    request.upload.onload = verifying;
    request.onerror = () => reject(new Error(`${file.name}: network upload failed.`));
    request.onload = () => {
      let body: { ok?: boolean; error?: { message?: string; reference?: string } } | null = null;
      try { body = JSON.parse(request.responseText); } catch { /* handled below */ }
      if (request.status >= 200 && request.status < 300 && body?.ok) resolve();
      else reject(new Error(`${file.name}: ${body?.error?.message || `upload failed (HTTP ${request.status})`}${body?.error?.reference ? ` Reference: ${body.error.reference}.` : ""}`));
    };
    const form = new FormData(); form.append("documents", file); request.send(form);
  });
}

export function CustomerInquiryPortal() {
  const { checking, authToken, isAuthRequired, userEmail, signOut, requireAuth, unavailable: sessionUnavailable, error: sessionError, refreshSession } = useOperatorSession();
  const formRef = useRef<HTMLFormElement>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);
  const replaceFileInputRef = useRef<HTMLInputElement>(null);
  const replaceIndexRef = useRef<number | null>(null);
  const draftIdRef = useRef<string | null>(null);
  const draftReferenceRef = useRef<string | null>(null);
  const [selectedFiles, setSelectedFiles] = useState<File[]>([]);
  const [dragging, setDragging] = useState(false);
  const [rows, setRows] = useState<CustomerInquiryListItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState("");
  const [notice, setNotice] = useState("");
  const [uploadStates, setUploadStates] = useState<Record<number, UploadState>>({});
  const [overallProgress, setOverallProgress] = useState(0);
  const [profile, setProfile] = useState<CustomerInquiryProfile | null>(null);
  const [projects, setProjects] = useState<CustomerInquiryProject[]>([]);
  const [setupLoaded, setSetupLoaded] = useState(false);
  const [historyProject, setHistoryProject] = useState("");
  const [historyType, setHistoryType] = useState("");
  const [historyStatus, setHistoryStatus] = useState("");

  useEffect(() => {
    if (!submitting) return;
    const warn = (event: BeforeUnloadEvent) => { event.preventDefault(); event.returnValue = ""; };
    window.addEventListener("beforeunload", warn);
    return () => window.removeEventListener("beforeunload", warn);
  }, [submitting]);

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

  async function loadSetup() {
    if (!authToken) return;
    try {
      const response=await fetch("/api/inquiry-submissions/profile",{headers:{Authorization:`Bearer ${authToken}`},cache:"no-store"});
      const body=await response.json(); if(!response.ok||!body.ok) throw new Error(body?.error?.message||"Unable to load saved customer details.");
      setProfile(body.data.profile); setProjects(body.data.projects||[]);
    } catch(reason) { setError(reason instanceof Error?reason.message:"Unable to load saved customer details."); }
    finally { setSetupLoaded(true); }
  }
  useEffect(() => { if (!checking && authToken) queueMicrotask(() => { void load(); void loadSetup(); }); }, [authToken, checking]); // eslint-disable-line react-hooks/exhaustive-deps

  function validFiles(input: FileList | File[]): File[] | null {
    const files = Array.from(input);
    const oversized = files.find((file) => file.size > MAX_BYTES);
    if (oversized) { setError(`${oversized.name} exceeds the 30 MB per-file limit.`); return null; }
    return files;
  }

  function addFiles(input: FileList | File[]) {
    const candidates = Array.from(input);
    setSelectedFiles((current) => {
      const accepted: File[] = [];
      const rejected: string[] = [];
      for (const file of candidates) {
        if (file.size > MAX_BYTES) {
          rejected.push(`${file.name} exceeds the 30 MB per-file limit.`);
          continue;
        }
        const duplicate = [...current, ...accepted].some((existing) => existing.name.toLocaleLowerCase() === file.name.toLocaleLowerCase());
        if (duplicate) {
          rejected.push(`${file.name} is already attached. Remove or replace the existing document before adding another file with that name.`);
          continue;
        }
        if (current.length + accepted.length >= MAX_FILES) {
          rejected.push(`${file.name} was skipped because the ${MAX_FILES}-document limit is already filled.`);
          continue;
        }
        accepted.push(file);
      }
      setError(rejected.join(" "));
      return accepted.length ? [...current, ...accepted] : current;
    });
  }

  function replaceFile(file: File) {
    const files = validFiles([file]);
    const index = replaceIndexRef.current;
    if (!files || index === null) return;
    setSelectedFiles((current) => {
      const replacement = files[0]!;
      const duplicate = current.some((existing, position) => position !== index && existing.name.toLocaleLowerCase() === replacement.name.toLocaleLowerCase());
      if (duplicate) { setError(`${replacement.name} is already attached. Choose a document with a different name.`); return current; }
      setError("");
      return current.map((existing, position) => position === index ? replacement : existing);
    });
    replaceIndexRef.current = null;
  }

  async function submit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault(); setError(""); setNotice("");
    if (!authToken) { requireAuth(); return; }
    const files = selectedFiles;
    if (files.length > MAX_FILES) { setError(`Select at most ${MAX_FILES} documents.`); return; }
    const oversized = files.find((file) => file.size > MAX_BYTES);
    if (oversized) { setError(`${oversized.name} exceeds the 30 MB per-file limit.`); return; }
    const form = new FormData(event.currentTarget); form.delete("documents"); form.set("expected_document_count", String(files.length));
    if (!draftIdRef.current) {
      setUploadStates(Object.fromEntries(files.map((_, index) => [index, { status: "waiting", percent: 0, detail: "Waiting" }])));
      setOverallProgress(0);
    }
    setSubmitting(true);
    try {
      if (!draftIdRef.current) {
        const profileResponse=await fetch("/api/inquiry-submissions/profile",{method:"PUT",headers:{Authorization:`Bearer ${authToken}`},body:form});
        const profileBody=await profileResponse.json().catch(()=>null); if(!profileResponse.ok||!profileBody?.ok)throw new Error(profileBody?.error?.message||"Customer details could not be saved.");
        if (!String(form.get("project_id")||"")) {
          const projectResponse=await fetch("/api/inquiry-submissions/projects",{method:"POST",headers:{Authorization:`Bearer ${authToken}`},body:form});
          const projectBody=await projectResponse.json().catch(()=>null); if(!projectResponse.ok||!projectBody?.ok)throw new Error(projectBody?.error?.message||"Project could not be saved.");
          const saved=Array.isArray(projectBody.data)?projectBody.data[0]:projectBody.data; form.set("project_id",saved.id);
        }
        const draftResponse = await fetch("/api/inquiry-submissions/drafts", { method: "POST", headers: { Authorization: `Bearer ${authToken}` }, body: form });
        const draftBody = await draftResponse.json().catch(() => null);
        if (draftResponse.status === 401) { requireAuth(); return; }
        if (!draftResponse.ok || !draftBody?.ok) throw new Error(draftBody?.error?.message || "The inquiry draft could not be created.");
        const createdDraft = draftBody.data as { id: string; submission_reference: string };
        draftIdRef.current = createdDraft.id; draftReferenceRef.current = createdDraft.submission_reference;
      }
      const draftId = draftIdRef.current!;
      const totalBytes = files.reduce((sum, file) => sum + file.size, 0) || 1;
      let completedBytes = files.reduce((sum, file, index) => sum + (uploadStates[index]?.status === "complete" ? file.size : 0), 0);
      for (let index = 0; index < files.length; index++) {
        const file = files[index]!;
        if (uploadStates[index]?.status === "complete") continue;
        const started = performance.now();
        setUploadStates((current) => ({ ...current, [index]: { status: "uploading", percent: 0, detail: "Starting upload" } }));
        try {
          await uploadDocument(authToken, draftId, file, (loaded, total) => {
            const seconds = Math.max((performance.now() - started) / 1000, 0.1);
            const rate = loaded / seconds; const remaining = rate > 0 ? Math.ceil((total - loaded) / rate) : 0;
            const percent = Math.min(100, Math.round((loaded / Math.max(total, 1)) * 100));
            setUploadStates((current) => ({ ...current, [index]: { status: "uploading", percent, detail: `${percent}% · ${remaining}s remaining` } }));
            setOverallProgress(Math.min(99, Math.round(((completedBytes + loaded) / totalBytes) * 100)));
          }, () => setUploadStates((current) => ({ ...current, [index]: { status: "verifying", percent: 100, detail: "Uploaded · verifying content" } })));
          completedBytes += file.size;
          setUploadStates((current) => ({ ...current, [index]: { status: "complete", percent: 100, detail: "Verified" } }));
          setOverallProgress(Math.round((completedBytes / totalBytes) * 100));
        } catch (reason) {
          setUploadStates((current) => ({ ...current, [index]: { status: "failed", percent: current[index]?.percent || 0, detail: "Failed" } }));
          throw reason;
        }
      }
      const finalResponse = await fetch(`/api/inquiry-submissions/${draftId}/finalize`, { method: "POST", headers: { Authorization: `Bearer ${authToken}` } });
      const finalBody = await finalResponse.json().catch(() => null);
      if (!finalResponse.ok || !finalBody?.ok) throw new Error(finalBody?.error?.message || "Files uploaded, but the inquiry could not be finalized.");
      const created = finalBody.data as CustomerInquirySubmission;
      setNotice(`Inquiry ${created.submission_reference} was submitted securely with ${filesLabel(files.length)}.`);
      formRef.current?.reset(); setSelectedFiles([]); setUploadStates({}); setOverallProgress(0); draftIdRef.current=null; draftReferenceRef.current=null; await load();
    } catch (reason) { setError(reason instanceof Error ? `${reason.message} Draft ${draftReferenceRef.current || ""} is retained; press Submit inquiry to retry only the failed document.` : "The inquiry could not be submitted."); }
    finally { setSubmitting(false); }
  }

  if (checking) return <div role="status" className="grid min-h-[55vh] place-items-center bg-ink-950 text-xs text-text-muted">Checking your secure session…</div>;
  if (sessionUnavailable && !authToken) return <main className="min-h-[65vh] bg-ink-950 px-4 py-20 text-text-primary"><section className="mx-auto max-w-md rounded-2xl border border-state-warning/40 bg-ink-900 p-6 text-center"><h1 className="text-xl font-bold">Saved sign-in could not be confirmed yet</h1><p className="mt-2 text-xs leading-relaxed text-text-muted">{sessionError || "The authentication service is temporarily unreachable. Your saved sign-in has not been discarded."}</p><button type="button" onClick={() => void refreshSession()} className="mt-5 rounded-xl bg-brand-400 px-5 py-3 text-sm font-bold text-accent-ink">Retry saved sign-in</button></section></main>;
  if (isAuthRequired || !authToken) return <main className="min-h-[65vh] bg-ink-950 px-4 py-20 text-text-primary"><section className="mx-auto max-w-md rounded-2xl border border-line-strong bg-ink-900 p-6 text-center"><h1 className="text-xl font-bold">Sign in to submit an inquiry</h1><p className="mt-2 text-xs text-text-muted">Email verification protects your inquiry history and private documents.</p><LocaleLink href="/account?redirect=%2Finquiries%2Fsubmit" className="mt-5 inline-flex rounded-xl bg-brand-400 px-5 py-3 text-sm font-bold text-accent-ink">Continue with email</LocaleLink></section></main>;
  const filteredRows=rows.filter((row)=>(!historyProject||row.project_id===historyProject)&&(!historyType||row.inquiry_type===historyType)&&(!historyStatus||row.status===historyStatus));

  return <main className="min-h-screen bg-ink-950 pb-20 text-text-primary">
    <header className="border-b border-line-strong/60"><div className="shell flex flex-wrap items-center justify-between gap-3 py-6"><div><div className="eyebrow text-brand-300">Secure customer inquiry</div><h1 className="mt-1 text-2xl font-bold">Submit project documents</h1><p className="mt-1 text-xs text-text-muted">PDF, images, XLSX and CSV · up to 30 MB each · 15 files maximum</p></div><div className="text-right"><div className="text-3xs text-text-faint">{userEmail}</div><button type="button" onClick={() => void signOut()} className="mt-2 text-xs font-semibold text-brand-300">Sign out</button></div></div></header>
    <div className="shell mt-6 grid gap-6 lg:grid-cols-[minmax(0,1.1fr)_minmax(20rem,.9fr)]">
      <section className="rounded-2xl border border-line-strong/60 bg-ink-900/85 p-5"><h2 className="text-lg font-bold">New inquiry</h2><p className="mt-1 text-xs text-text-muted">Provide enough information for the estimating team to review your request.</p>
        {error && <div role="alert" className="mt-4 rounded-xl border border-state-danger/40 bg-state-danger/10 p-3 text-xs text-state-danger">{error}</div>}
        {notice && <div role="status" className="mt-4 rounded-xl border border-state-success/40 bg-state-success/10 p-3 text-xs text-state-success">{notice}</div>}
        {!setupLoaded?<p role="status" className="mt-5 text-xs text-text-muted">Loading your saved details and projects…</p>:<form ref={formRef} onSubmit={submit} className="mt-5 grid gap-4 sm:grid-cols-2">
          <Field name="contact_name" label="Contact name" required maxLength={120} defaultValue={profile?.contact_name} /><Field name="company_name" label="Company" required maxLength={160} defaultValue={profile?.company_name} />
          <Field name="phone" label="Phone" maxLength={60} defaultValue={profile?.phone} /><Field name="customer_reference" label="Your reference" maxLength={100} />
          <Field name="attention_name" label="Attention" maxLength={120} defaultValue={profile?.attention_name} /><Field name="vat_trn_number" label="VAT / TRN number" maxLength={50} defaultValue={profile?.vat_trn_number} />
          <Field name="salesman_reference" label="Salesman reference" maxLength={120} /><Field name="handover_estimator_name" label="Handover estimator name" maxLength={120} />
          <label className="sm:col-span-2 text-sm font-semibold leading-5 text-text-secondary">Saved project<select name="project_id" onChange={(event)=>{const project=projects.find((item)=>item.id===event.target.value);if(!project||!formRef.current)return;const elements=formRef.current.elements;(elements.namedItem("project_name") as HTMLInputElement).value=project.project_name;(elements.namedItem("project_location") as HTMLInputElement).value=project.project_location;(elements.namedItem("inquiry_type") as HTMLSelectElement).value=project.default_inquiry_type;}} className="mt-2 min-h-12 w-full rounded-xl border border-line-strong bg-ink-950 px-3 text-base text-text-primary"><option value="">New project — save from fields below</option>{projects.map((project)=><option key={project.id} value={project.id}>{project.project_name}{project.project_location?` · ${project.project_location}`:""}</option>)}</select></label>
          <Field name="project_name" label="Project name" required maxLength={180} /><Field name="project_location" label="Project location" maxLength={180} />
          <label className="sm:col-span-2 text-sm font-semibold leading-5 text-text-secondary">Inquiry type<select name="inquiry_type" defaultValue="quotation" className="mt-2 min-h-12 w-full rounded-xl border border-line-strong bg-ink-950 px-3 text-base text-text-primary"><option value="quotation">Quotation</option><option value="drawing_review">Drawing review</option><option value="technical">Technical question</option><option value="order_handover">Order handover</option><option value="other">Other</option></select></label>
          <label htmlFor="inquiry-description" className="sm:col-span-2 text-sm font-semibold leading-5 text-text-secondary">Requirements<textarea id="inquiry-description" name="description" required minLength={10} maxLength={5000} rows={6} className="mt-2 min-h-36 w-full rounded-xl border border-line-strong bg-ink-950 p-3 text-base leading-6 text-text-primary caret-brand-300 outline-none focus-visible:border-brand-400 focus-visible:ring-2 focus-visible:ring-brand-400/40 sm:text-sm" /></label>
          <div className="sm:col-span-2"><div className="flex items-center justify-between gap-3"><div id="inquiry-documents-label" className="text-sm font-semibold leading-5 text-text-secondary">Documents</div>{selectedFiles.length>0&&<button type="button" onClick={()=>setSelectedFiles([])} className="rounded-lg border border-line-strong px-3 py-1.5 text-xs font-semibold text-state-danger hover:bg-state-danger/10 focus-visible:ring-2 focus-visible:ring-state-danger/40">Remove all</button>}</div><input ref={fileInputRef} id="inquiry-documents" aria-labelledby="inquiry-documents-label" aria-describedby="inquiry-documents-help" name="documents" type="file" multiple accept={ACCEPT} onChange={(event)=>{if(event.target.files)addFiles(event.target.files);event.target.value="";}} className="sr-only" /><input ref={replaceFileInputRef} aria-label="Choose a replacement document" type="file" accept={ACCEPT} onChange={(event)=>{const file=event.target.files?.[0];if(file)replaceFile(file);event.target.value="";}} className="sr-only" />
            <button type="button" aria-labelledby="inquiry-documents-label inquiry-document-picker-copy" aria-describedby="inquiry-documents-help" onClick={()=>fileInputRef.current?.click()} onDragEnter={(event)=>{event.preventDefault();setDragging(true);}} onDragOver={(event)=>event.preventDefault()} onDragLeave={()=>setDragging(false)} onDrop={(event)=>{event.preventDefault();setDragging(false);addFiles(event.dataTransfer.files);}} className={`mt-2 w-full rounded-2xl border-2 border-dashed p-5 text-center outline-none transition focus-visible:border-brand-300 focus-visible:ring-2 focus-visible:ring-brand-400/50 sm:p-6 ${dragging?"border-brand-300 bg-brand-500/15":"border-line-strong bg-ink-950/70 hover:border-brand-400/70 hover:bg-ink-950"}`}>
              <span aria-hidden="true" className="mx-auto grid h-11 w-11 place-items-center rounded-xl bg-brand-500/15 text-xl text-brand-300">+</span><span id="inquiry-document-picker-copy" className="mt-3 block text-sm font-bold leading-5 text-text-primary">{selectedFiles.length ? "Add more documents" : "Choose documents or drop them here"}</span><span id="inquiry-documents-help" className="mt-1 block text-xs leading-5 text-text-muted">PDF, images, XLSX or CSV · 30 MB each · {selectedFiles.length}/{MAX_FILES} selected</span>
            </button>
            {selectedFiles.length>0&&<div aria-live="polite" aria-label={`${selectedFiles.length} documents selected`} className="mt-3 grid gap-2 sm:grid-cols-2">{selectedFiles.map((file,index)=><div key={`${file.name}-${file.size}-${index}`} className="flex items-center justify-between gap-3 rounded-xl border border-line-strong bg-ink-950 px-3 py-2"><div className="min-w-0"><div className="truncate text-xs font-semibold text-text-primary">{file.name}</div><div className="text-3xs text-text-faint">{Math.ceil(file.size/1024).toLocaleString()} KB · {uploadStates[index]?.detail || "ready to attach"}</div>{uploadStates[index]&&<div className="mt-1 h-1.5 overflow-hidden rounded-full bg-ink-800" role="progressbar" aria-label={`${file.name} upload progress`} aria-valuemin={0} aria-valuemax={100} aria-valuenow={uploadStates[index]?.percent}><div className={`h-full transition-[width] ${uploadStates[index]?.status==="failed"?"bg-state-danger":"bg-brand-400"}`} style={{width:`${uploadStates[index]?.percent||0}%`}} /></div>}</div><div className={`shrink-0 gap-1 ${submitting?"hidden":"flex"}`}><button type="button" aria-label={`Replace ${file.name}`} onClick={()=>{replaceIndexRef.current=index;replaceFileInputRef.current?.click();}} className="rounded-lg px-2 py-1 text-xs font-semibold text-brand-300 hover:bg-brand-500/10 focus-visible:ring-2 focus-visible:ring-brand-400/40">Replace</button><button type="button" aria-label={`Remove ${file.name}`} onClick={()=>setSelectedFiles((current)=>current.filter((_,position)=>position!==index))} className="rounded-lg px-2 py-1 text-xs font-semibold text-state-danger hover:bg-state-danger/10 focus-visible:ring-2 focus-visible:ring-state-danger/40">Remove</button></div></div>)}</div>}
            <span className="mt-2 block text-3xs text-text-faint">Files stay private and are checked by content, not filename alone. CAD files must be exported to PDF or image.</span>
          </div>
          {submitting&&<div className="sm:col-span-2" role="status" aria-live="polite"><div className="flex justify-between text-xs font-semibold"><span>Uploading and verifying {filesLabel(selectedFiles.length)}</span><span>{overallProgress}%</span></div><div className="mt-2 h-2.5 overflow-hidden rounded-full bg-ink-800" role="progressbar" aria-label="Overall inquiry upload progress" aria-valuemin={0} aria-valuemax={100} aria-valuenow={overallProgress}><div className="h-full bg-brand-400 transition-[width]" style={{width:`${overallProgress}%`}} /></div><p className="mt-2 text-3xs text-text-muted">Keep this page open. Each document is uploaded and verified separately.</p></div>}
          <button disabled={submitting} className="sm:col-span-2 rounded-xl bg-brand-400 px-5 py-3 text-sm font-bold text-accent-ink disabled:opacity-50">{submitting ? `Uploading ${overallProgress}%` : "Submit inquiry"}</button>
        </form>}
      </section>
      <section className="rounded-2xl border border-line-strong/60 bg-ink-900/85 p-5"><div className="flex items-center justify-between"><h2 className="text-lg font-bold">Your submissions</h2><button onClick={() => void load()} className="text-xs font-semibold text-brand-300">Refresh</button></div>
        <div className="mt-3 grid gap-2 sm:grid-cols-3"><select aria-label="Filter by project" value={historyProject} onChange={(event)=>setHistoryProject(event.target.value)} className="h-10 rounded-xl border border-line-strong bg-ink-950 px-2 text-xs"><option value="">All projects</option>{projects.map((project)=><option key={project.id} value={project.id}>{project.project_name}</option>)}</select><select aria-label="Filter by inquiry type" value={historyType} onChange={(event)=>setHistoryType(event.target.value)} className="h-10 rounded-xl border border-line-strong bg-ink-950 px-2 text-xs"><option value="">All types</option><option value="quotation">Quotation</option><option value="drawing_review">Drawing review</option><option value="technical">Technical</option><option value="order_handover">Order handover</option><option value="other">Other</option></select><select aria-label="Filter by status" value={historyStatus} onChange={(event)=>setHistoryStatus(event.target.value)} className="h-10 rounded-xl border border-line-strong bg-ink-950 px-2 text-xs"><option value="">All statuses</option><option value="submitted">Submitted</option><option value="under_review">Under review</option><option value="accepted">Accepted</option><option value="rejected">Rejected</option><option value="closed">Closed</option></select></div>
        {loading ? <p role="status" className="mt-5 text-xs text-text-muted">Loading…</p> : rows.length === 0 ? <p className="mt-5 text-xs text-text-muted">No website inquiries submitted by this account.</p> : filteredRows.length===0?<p className="mt-5 text-xs text-text-muted">No inquiries match these filters.</p>: <div className="mt-4 space-y-3">{filteredRows.map((row) => <LocaleLink key={row.id} href={`/inquiries/submissions/${row.id}`} className="block rounded-xl border border-line-strong bg-ink-950/70 p-4"><div className="flex justify-between gap-3"><span className="font-mono text-xs font-bold text-brand-300">{row.submission_reference}</span><span className="rounded-full border border-line-strong px-2 py-0.5 text-3xs uppercase text-text-muted">{row.status.replaceAll("_", " ")}</span></div><div className="mt-2 text-sm font-semibold">{row.project_name}</div><div className="mt-1 text-3xs text-text-faint">{row.document_count} document{row.document_count === 1 ? "" : "s"} · submitted {date(row.submitted_at)}</div><div className="mt-2 text-3xs font-semibold text-text-muted">Response estimate: {row.estimated_response_at ? date(row.estimated_response_at) : "pending operator review"}</div>{row.operator_note && <div className="mt-1 text-xs text-text-muted">Operator note: {row.operator_note}</div>}</LocaleLink>)}</div>}
      </section>
    </div>
  </main>;
}

function Field({ name, label, required, maxLength, defaultValue }: { name: string; label: string; required?: boolean; maxLength: number; defaultValue?: string }) {
  const id = `inquiry-${name.replaceAll("_", "-")}`;
  return <label htmlFor={id} className="text-sm font-semibold leading-5 text-text-secondary">{label}<input id={id} name={name} required={required} maxLength={maxLength} defaultValue={defaultValue} className="mt-2 min-h-12 w-full rounded-xl border border-line-strong bg-ink-950 px-3 py-3 text-base leading-6 text-text-primary caret-brand-300 outline-none placeholder:text-text-faint focus-visible:border-brand-400 focus-visible:ring-2 focus-visible:ring-brand-400/40 sm:text-sm" /></label>;
}

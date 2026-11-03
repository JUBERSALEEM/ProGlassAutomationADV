"use client";

import { useEffect, useRef, useState } from "react";
import { LocaleLink } from "@/components/ui/LocaleLink";
import { InquiryPromptFlow } from "@/components/inquiries/InquiryPromptFlow";
import { ProformaTechnicalSpecFlow } from "@/components/inquiries/ProformaTechnicalSpecFlow";
import { useOperatorSession } from "@/lib/useOperatorSession";
import { CUSTOMER_INQUIRY_TYPES, CUSTOMER_INQUIRY_TYPE_LABELS } from "@/types/inquiry-submissions";
import type { CustomerInquiryListItem, CustomerInquiryProfile, CustomerInquiryProject, CustomerInquirySubmission, CustomerInquiryType } from "@/types/inquiry-submissions";

const ACCEPT = ".pdf,.jpg,.jpeg,.png,.gif,.webp,.bmp,.xlsx,.csv";
const MAX_FILES = 15;
const AUTO_PI_ATTACHMENT_COUNT = 3;
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

export function CustomerInquiryPortal({ intent = "quotation" }: { intent?: CustomerInquiryType } = {}) {
  const { checking, authToken, isAuthRequired, userEmail, signOut, requireAuth, unavailable: sessionUnavailable, error: sessionError, refreshSession } = useOperatorSession();
  const userFileLimit = intent === "proforma_invoice" ? MAX_FILES - AUTO_PI_ATTACHMENT_COUNT : MAX_FILES;
  const formRef = useRef<HTMLFormElement>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);
  const replaceFileInputRef = useRef<HTMLInputElement>(null);
  const replaceIndexRef = useRef<number | null>(null);
  const draftIdRef = useRef<string | null>(null);
  const draftReferenceRef = useRef<string | null>(null);
  const [selectedFiles, setSelectedFiles] = useState<File[]>([]);
  const [generatedPiFiles, setGeneratedPiFiles] = useState<File[]>([]);
  const generatedPiFilesRef = useRef<File[] | null>(null);
  const [generatingPiFiles, setGeneratingPiFiles] = useState(false);
  const [flowInstance, setFlowInstance] = useState(0);
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
        if (current.length + accepted.length >= userFileLimit) {
          rejected.push(`${file.name} was skipped because the ${userFileLimit}-document limit for optional uploads is already filled.`);
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
    const currentForm = event.currentTarget;
    const requiredValues: Array<[string, string, number]> = [
      ["company_name", "Company name", 1],
      ["contact_name", "Contact name", 1],
      ["project_name", "Project name", 1],
      ["description", "Project requirements", 10],
    ];
    for (const [name, label, minimum] of requiredValues) {
      const value = currentForm.elements.namedItem(name);
      const text = value instanceof HTMLInputElement || value instanceof HTMLTextAreaElement ? value.value.trim() : "";
      if (text.length < minimum) {
        setError(`${label} is required${minimum > 1 ? ` (at least ${minimum} characters)` : ""}.`);
        (value as HTMLElement | null)?.focus();
        return;
      }
    }
    let submissionDescription = String(new FormData(currentForm).get("description") || "").trim();
    let technicalSpecification = "";
    let technicalSpecificationJson = "";
    if (intent === "proforma_invoice") {
      const complete = currentForm.querySelector<HTMLInputElement>('input[name="pi_spec_complete"]')?.value === "true";
      const specification = currentForm.querySelector<HTMLInputElement>('input[name="pi_specification"]')?.value.trim() || "";
      technicalSpecificationJson = currentForm.querySelector<HTMLInputElement>('input[name="pi_specification_json"]')?.value || "";
      if (!complete || !specification || !technicalSpecificationJson) {
        setError("Complete the technical specification steps 4–7 before sending the PI request.");
        currentForm.querySelector<HTMLElement>("[aria-label='Proforma technical specification generator']")?.scrollIntoView({ behavior: "smooth", block: "center" });
        return;
      }
      technicalSpecification = specification;
      submissionDescription = `${submissionDescription}\n\n${specification}`;
      if (submissionDescription.length > 5000) {
        setError("The project description and technical specification exceed the 5,000-character request limit. Shorten the notes or reduce the cutting-list lines.");
        return;
      }
    }
    if (!authToken) { requireAuth(); return; }
    const userFiles = selectedFiles;
    if (userFiles.length > userFileLimit) { setError(`Select at most ${userFileLimit} optional project files${intent === "proforma_invoice" ? "; three more slots are reserved for the generated PI files" : ""}.`); return; }
    const oversized = userFiles.find((file) => file.size > MAX_BYTES);
    if (oversized) { setError(`${oversized.name} exceeds the 30 MB per-file limit.`); return; }
    const form = new FormData(currentForm); form.delete("documents");
    form.delete("pi_specification"); form.delete("pi_specification_json"); form.delete("pi_spec_complete");
    setSubmitting(true);
    try {
      let files = userFiles;
      if (intent === "proforma_invoice") {
        if (!generatedPiFilesRef.current) {
          setGeneratingPiFiles(true);
          const { generateCustomerPiAttachments } = await import("@/lib/customer-pi-attachments");
          const generated = await generateCustomerPiAttachments({
            companyName: String(form.get("company_name") || ""),
            contactName: String(form.get("contact_name") || ""),
            customerReference: String(form.get("customer_reference") || ""),
            projectName: String(form.get("project_name") || ""),
            projectLocation: String(form.get("project_location") || ""),
            projectRequirements: String(form.get("description") || ""),
            technicalSpecification,
            technicalSpecificationJson,
          });
          const collidingName = userFiles.find((file) => generated.some((attachment) => file.name.toLocaleLowerCase() === attachment.name.toLocaleLowerCase()));
          if (collidingName) throw new Error(`Rename or remove ${collidingName.name}; it conflicts with an automatically generated PI attachment.`);
          generatedPiFilesRef.current = generated;
          setGeneratedPiFiles(generated);
        }
        setGeneratingPiFiles(false);
        files = [...userFiles, ...(generatedPiFilesRef.current || [])];
      }
      const duplicateFile = files.find((file, index) => files.findIndex((candidate) => candidate.name.normalize("NFKC").trim().toLocaleLowerCase() === file.name.normalize("NFKC").trim().toLocaleLowerCase()) !== index);
      if (duplicateFile) throw new Error(`${duplicateFile.name} is attached more than once. Rename or remove the duplicate before retrying.`);
      if (files.length > MAX_FILES) throw new Error(`The request exceeds the ${MAX_FILES}-document upload limit.`);
      form.set("expected_document_count", String(files.length));
      if (!draftIdRef.current) {
        setUploadStates(Object.fromEntries(files.map((_, index) => [index, { status: "waiting", percent: 0, detail: "Waiting" }])));
        setOverallProgress(0);
      }
      if (!draftIdRef.current) {
        const profileResponse=await fetch("/api/inquiry-submissions/profile",{method:"PUT",headers:{Authorization:`Bearer ${authToken}`},body:form});
        const profileBody=await profileResponse.json().catch(()=>null); if(!profileResponse.ok||!profileBody?.ok)throw new Error(profileBody?.error?.message||"Customer details could not be saved.");
        if (!String(form.get("project_id")||"")) {
          const projectResponse=await fetch("/api/inquiry-submissions/projects",{method:"POST",headers:{Authorization:`Bearer ${authToken}`},body:form});
          const projectBody=await projectResponse.json().catch(()=>null); if(!projectResponse.ok||!projectBody?.ok)throw new Error(projectBody?.error?.message||"Project could not be saved.");
          const saved=Array.isArray(projectBody.data)?projectBody.data[0]:projectBody.data; form.set("project_id",saved.id);
        }
        const draftForm = new FormData();
        form.forEach((value, key) => draftForm.append(key, value));
        draftForm.set("description", submissionDescription);
        const draftResponse = await fetch("/api/inquiry-submissions/drafts", { method: "POST", headers: { Authorization: `Bearer ${authToken}` }, body: draftForm });
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
      setNotice(intent === "proforma_invoice" ? `PI request ${created.submission_reference} was sent to the ProGlass inquiry inbox with ${filesLabel(files.length)}.` : `Inquiry ${created.submission_reference} was submitted securely with ${filesLabel(files.length)}.`);
      formRef.current?.reset(); setFlowInstance((value) => value + 1); setSelectedFiles([]); setGeneratedPiFiles([]); generatedPiFilesRef.current = null; setGeneratingPiFiles(false); setUploadStates({}); setOverallProgress(0); draftIdRef.current=null; draftReferenceRef.current=null; await load();
    } catch (reason) {
      setGeneratingPiFiles(false);
      const message = reason instanceof Error ? reason.message : "The inquiry could not be submitted.";
      setError(draftIdRef.current ? `${message} Draft ${draftReferenceRef.current || ""} is retained; press ${intent === "proforma_invoice" ? "Send PI request to ProGlass" : "Submit inquiry"} to retry the remaining document uploads.` : `${message} No request was sent; correct the issue and try again.`);
    }
    finally { setSubmitting(false); }
  }

  if (checking) return <div role="status" className="grid min-h-[55vh] place-items-center bg-ink-950 text-xs text-text-muted">Checking your secure session…</div>;
  if (sessionUnavailable && !authToken) return <main className="min-h-[65vh] bg-ink-950 px-4 py-20 text-text-primary"><section className="mx-auto max-w-md rounded-2xl border border-state-warning/40 bg-ink-900 p-6 text-center"><h1 className="text-xl font-bold">Saved sign-in could not be confirmed yet</h1><p className="mt-2 text-xs leading-relaxed text-text-muted">{sessionError || "The authentication service is temporarily unreachable. Your saved sign-in has not been discarded."}</p><button type="button" onClick={() => void refreshSession()} className="mt-5 rounded-xl bg-brand-400 px-5 py-3 text-sm font-bold text-accent-ink">Retry saved sign-in</button></section></main>;
  if (isAuthRequired || !authToken) return <main className="min-h-[65vh] bg-ink-950 px-4 py-20 text-text-primary"><section className="mx-auto max-w-md rounded-2xl border border-line-strong bg-ink-900 p-6 text-center"><h1 className="text-xl font-bold">Sign in to submit an inquiry</h1><p className="mt-2 text-xs text-text-muted">Email verification protects your inquiry history and private documents.</p><LocaleLink href="/account?redirect=%2Finquiries%2Fsubmit" className="mt-5 inline-flex rounded-xl bg-brand-400 px-5 py-3 text-sm font-bold text-accent-ink">Continue with email</LocaleLink></section></main>;
  const filteredRows=rows.filter((row)=>(!historyProject||row.project_id===historyProject)&&(!historyType||row.inquiry_type===historyType)&&(!historyStatus||row.status===historyStatus));

  return <main className="min-h-screen bg-ink-950 pb-20 text-text-primary">
    <header className="border-b border-line-strong/60"><div className="shell flex flex-wrap items-center justify-between gap-3 py-6"><div><div className="eyebrow text-brand-300">Secure customer portal</div><h1 className="mt-1 text-2xl font-bold">{intent === "proforma_invoice" ? "Request a proforma invoice" : "Create an inquiry"}</h1><p className="mt-1 text-xs text-text-muted">{intent === "proforma_invoice" ? "Enter customer and project details, then build the technical glass specification through WPF Step 7. Your Excel, CSV, and PDF files are generated and attached automatically; no upload is required." : "Share your company and project details in one guided flow. Supporting files are optional."}</p></div><div className="text-right"><div className="text-3xs text-text-faint">{userEmail}</div><button type="button" onClick={() => void signOut()} className="mt-2 text-xs font-semibold text-brand-300">Sign out</button></div></div></header>
    <div className="shell mt-6 grid gap-6 lg:grid-cols-[minmax(0,1.1fr)_minmax(20rem,.9fr)]">
      <section className="rounded-2xl border border-line-strong/60 bg-ink-900/85 p-5"><h2 className="text-lg font-bold">{intent === "proforma_invoice" ? "PI request details" : "Inquiry details"}</h2><p className="mt-1 text-xs text-text-muted">{intent === "proforma_invoice" ? "Customer and project details come first; the glass specification generator follows before the request is sent to the ProGlass team." : "Company, client, and project details are gathered together for the ProGlass team."}</p>
        {error && <div role="alert" className="mt-4 rounded-xl border border-state-danger/40 bg-state-danger/10 p-3 text-xs text-state-danger">{error}</div>}
        {notice && <div role="status" className="mt-4 rounded-xl border border-state-success/40 bg-state-success/10 p-3 text-xs text-state-success">{notice}</div>}
        {!setupLoaded ? <p role="status" className="mt-5 text-xs text-text-muted">Loading your saved details and projects…</p> : (
          <form ref={formRef} onSubmit={submit} noValidate className="mt-5">
            {intent === "proforma_invoice" && <input type="hidden" name="inquiry_type" value="proforma_invoice" />}
            <InquiryPromptFlow
              key={flowInstance}
              hints={[
                { group: "Customer + project", title: "Company name", body: "Enter the company name the estimating team should use for this request.", example: "Al Noor Glass LLC" },
                { group: "Customer + project", title: "Your name", body: "Who should we contact about this project?", example: "Aisha Khan" },
                { group: "Customer + project", title: "Phone number", body: "Add a direct number if you would like the team to call. This field is optional.", example: "+971 50 123 4567" },
                { group: "Customer + project", title: "VAT / TRN number", body: "If available, enter the company tax registration number. You can leave it blank.", example: "100123456700003" },
                { group: "Customer + project", title: "Your reference", body: "Add your own quotation, tender, or purchase reference so the team can match correspondence.", example: "RFQ-2026-018" },
                { group: "Customer + project", title: "Choose a saved project", body: "Reuse an existing project, or leave this blank and enter a new project in the next prompts." },
                { group: "Customer + project", title: "Project name", body: "Use the project or site name that should appear in the inquiry inbox.", example: "Marina Residence — Tower B" },
                { group: "Customer + project", title: "Project location", body: "Enter the site location, city, or address. This helps the ProGlass team route the request.", example: "Al Reem Island, Abu Dhabi" },
                { group: "Customer + project", title: "Attention", body: "Optional: name the person or department that should receive the response.", example: "Attn: Procurement Department" },
                { group: "Customer + project", title: "Sales reference", body: "Optional: identify the salesperson or internal reference connected to this request.", example: "Sales team — Khalid" },
                { group: "Customer + project", title: "Handover estimator", body: "Optional: if an estimator is already assigned, enter their name. Otherwise leave it blank." },
                ...(intent === "proforma_invoice" ? [
                  { group: "Customer + project", title: "Project requirements", body: "Add delivery timing, scope, and any project context before you generate the technical glass specifications.", example: "Supply 24 panels for the Marina Residence fit-out; requested delivery in March." },
                  { group: "PI specification", title: "Build the technical specification", body: "Next configure one or more glass groups: thickness, colour, FT or annealed treatment, stock sheet dimensions, cutting-list lines, overlap, U-insert, polish, and other fabrication details. The final WPF Step 7 preview is technical-only—no customer pricing." },
                ] : [
                  { group: "Request details", title: "What do you need?", body: "Choose the kind of help you need. You can ask for a quotation, drawing review, technical support, or another inquiry." },
                  { group: "Request details", title: "Describe the project", body: "Tell us what you need estimated or reviewed. Include glass type, quantities, dimensions, deadline, or other useful context.", example: "Please quote 24 DGU panels, 1200 × 1800 mm, clear glass, delivery to Abu Dhabi." },
                ]),
              ]}
            >
              <Field name="company_name" label="Company name" placeholder="e.g. Al Noor Glass LLC" required maxLength={160} defaultValue={profile?.company_name} />
              <Field name="contact_name" label="Contact name" placeholder="e.g. Aisha Khan" required maxLength={120} defaultValue={profile?.contact_name} />
              <Field name="phone" label="Phone" placeholder="e.g. +971 50 123 4567" maxLength={60} defaultValue={profile?.phone} />
              <Field name="vat_trn_number" label="VAT / TRN number" placeholder="Optional" maxLength={50} defaultValue={profile?.vat_trn_number} />
              <Field name="customer_reference" label="Your reference" placeholder="e.g. RFQ-2026-018" maxLength={100} />
              <label className="block text-sm font-semibold leading-5 text-text-secondary">Saved project
                <select name="project_id" defaultValue="" onChange={(event) => {
                  const project = projects.find((item) => item.id === event.target.value);
                  if (!project || !formRef.current) return;
                  const elements = formRef.current.elements;
                  (elements.namedItem("project_name") as HTMLInputElement).value = project.project_name;
                  (elements.namedItem("project_location") as HTMLInputElement).value = project.project_location;
                  if (intent !== "proforma_invoice") (elements.namedItem("inquiry_type") as HTMLSelectElement).value = project.default_inquiry_type;
                }} className="mt-2 min-h-12 w-full rounded-xl border border-line-strong bg-ink-950 px-3 text-base text-text-primary">
                  <option value="">New project — save from fields below</option>
                  {projects.map((project) => <option key={project.id} value={project.id}>{project.project_name}{project.project_location ? ` · ${project.project_location}` : ""}</option>)}
                </select>
              </label>
              <Field name="project_name" label="Project name" placeholder="e.g. Marina Residence — Tower B" required maxLength={180} />
              <Field name="project_location" label="Project location" placeholder="e.g. Al Reem Island, Abu Dhabi" maxLength={180} />
              <Field name="attention_name" label="Attention" placeholder="Optional contact or department" maxLength={120} defaultValue={profile?.attention_name} />
              <Field name="salesman_reference" label="Sales reference" placeholder="Optional" maxLength={120} />
              <Field name="handover_estimator_name" label="Handover estimator" placeholder="Optional" maxLength={120} />
              {intent !== "proforma_invoice" && <label className="block text-sm font-semibold leading-5 text-text-secondary">Inquiry type
                <select name="inquiry_type" defaultValue={intent} className="mt-2 min-h-12 w-full rounded-xl border border-line-strong bg-ink-950 px-3 text-base text-text-primary">
                  {CUSTOMER_INQUIRY_TYPES.map((value) => <option key={value} value={value}>{CUSTOMER_INQUIRY_TYPE_LABELS[value]}</option>)}
                </select>
              </label>}
              <label htmlFor="inquiry-description" className="block text-sm font-semibold leading-5 text-text-secondary">Project requirements
                <textarea id="inquiry-description" name="description" required minLength={10} maxLength={5000} rows={5} placeholder={intent === "proforma_invoice" ? "Add the project scope, requested delivery timing, and other context before generating glass specifications." : "What should the ProGlass team estimate or review? Add glass type, dimensions, quantities, delivery date, and any important notes."} className="mt-2 min-h-36 w-full rounded-xl border border-line-strong bg-ink-950 p-3 text-base leading-6 text-text-primary caret-brand-300 outline-none placeholder:text-text-faint focus-visible:border-brand-400 focus-visible:ring-2 focus-visible:ring-brand-400/40 sm:text-sm" />
              </label>
              {intent === "proforma_invoice" && <ProformaTechnicalSpecFlow />}
            </InquiryPromptFlow>

            <section className="mt-4 rounded-2xl border border-line-strong/60 bg-ink-950/45 p-4" aria-labelledby="inquiry-documents-label">
              <div className="flex items-center justify-between gap-3">
                <div><h3 id="inquiry-documents-label" className="text-sm font-bold text-text-primary">Supporting project files <span className="font-normal text-text-faint">(optional)</span></h3><p className="mt-1 text-xs text-text-muted">{intent === "proforma_invoice" ? "You can submit without uploading anything. Your completed PI specification is automatically attached as an Excel workbook, CSV, and PDF; upload drawings or BOQ files only if you want to." : "Attach inquiry drawings or BOQ files for the WPF inbox, or submit without files."}</p></div>
                {selectedFiles.length > 0 && <button type="button" onClick={() => setSelectedFiles([])} className="rounded-lg border border-line-strong px-3 py-1.5 text-xs font-semibold text-state-danger hover:bg-state-danger/10">Remove all</button>}
              </div>
              <input ref={fileInputRef} id="inquiry-documents" aria-labelledby="inquiry-documents-label" aria-describedby="inquiry-documents-help" name="documents" type="file" multiple accept={ACCEPT} onChange={(event) => { if (event.target.files) addFiles(event.target.files); event.target.value = ""; }} className="sr-only" />
              <input ref={replaceFileInputRef} aria-label="Choose a replacement document" type="file" accept={ACCEPT} onChange={(event) => { const file = event.target.files?.[0]; if (file) replaceFile(file); event.target.value = ""; }} className="sr-only" />
              <button type="button" aria-labelledby="inquiry-documents-label inquiry-document-picker-copy" aria-describedby="inquiry-documents-help" onClick={() => fileInputRef.current?.click()} onDragEnter={(event) => { event.preventDefault(); setDragging(true); }} onDragOver={(event) => event.preventDefault()} onDragLeave={() => setDragging(false)} onDrop={(event) => { event.preventDefault(); setDragging(false); addFiles(event.dataTransfer.files); }} className={`mt-3 w-full rounded-2xl border-2 border-dashed p-4 text-center outline-none transition focus-visible:border-brand-300 focus-visible:ring-2 focus-visible:ring-brand-400/50 sm:p-5 ${dragging ? "border-brand-300 bg-brand-500/15" : "border-line-strong bg-ink-950/70 hover:border-brand-400/70"}`}>
                <span aria-hidden="true" className="mx-auto grid h-10 w-10 place-items-center rounded-xl bg-brand-500/15 text-xl text-brand-300">+</span><span id="inquiry-document-picker-copy" className="mt-2 block text-sm font-bold leading-5 text-text-primary">{selectedFiles.length ? "Add more documents" : "Choose drawings or drop them here"}</span><span id="inquiry-documents-help" className="mt-1 block text-xs leading-5 text-text-muted">PDF, images, XLSX or CSV · 30 MB each · {selectedFiles.length}/{userFileLimit} optional files{intent === "proforma_invoice" ? ` · ${AUTO_PI_ATTACHMENT_COUNT} PI files auto-generated` : ""}</span>
              </button>
              {intent === "proforma_invoice" && <div className="mt-3 rounded-xl border border-brand-300/15 bg-brand-400/[.05] p-3 text-xs"><p className="font-bold text-brand-100">Auto-attached PI files · Excel (.xlsx), CSV, and PDF</p><p className="mt-1 text-text-muted">{generatingPiFiles ? "Generating the three files from your Step 4–7 specification…" : generatedPiFiles.length ? generatedPiFiles.map((file, index) => `${file.name} · ${uploadStates[selectedFiles.length + index]?.detail || "ready to attach"}`).join(" · ") : "Generated and attached automatically when you send the PI request. No upload is needed."}</p></div>}
              {selectedFiles.length > 0 && <div aria-live="polite" aria-label={`${selectedFiles.length} documents selected`} className="mt-3 grid gap-2 sm:grid-cols-2">{selectedFiles.map((file, index) => <div key={`${file.name}-${file.size}-${index}`} className="flex items-center justify-between gap-3 rounded-xl border border-line-strong bg-ink-950 px-3 py-2"><div className="min-w-0"><div className="truncate text-xs font-semibold text-text-primary">{file.name}</div><div className="text-3xs text-text-faint">{Math.ceil(file.size / 1024).toLocaleString()} KB · {uploadStates[index]?.detail || "ready to attach"}</div>{uploadStates[index] && <div className="mt-1 h-1.5 overflow-hidden rounded-full bg-ink-800" role="progressbar" aria-label={`${file.name} upload progress`} aria-valuemin={0} aria-valuemax={100} aria-valuenow={uploadStates[index]?.percent}><div className={`h-full transition-[width] ${uploadStates[index]?.status === "failed" ? "bg-state-danger" : "bg-brand-400"}`} style={{ width: `${uploadStates[index]?.percent || 0}%` }} /></div>}</div><div className={`shrink-0 gap-1 ${submitting ? "hidden" : "flex"}`}><button type="button" aria-label={`Replace ${file.name}`} onClick={() => { replaceIndexRef.current = index; replaceFileInputRef.current?.click(); }} className="rounded-lg px-2 py-1 text-xs font-semibold text-brand-300 hover:bg-brand-500/10">Replace</button><button type="button" aria-label={`Remove ${file.name}`} onClick={() => setSelectedFiles((current) => current.filter((_, position) => position !== index))} className="rounded-lg px-2 py-1 text-xs font-semibold text-state-danger hover:bg-state-danger/10">Remove</button></div></div>)}</div>}
              <span className="mt-2 block text-3xs text-text-faint">Files stay private and are checked by content, not filename alone. CAD files must be exported to PDF or image.</span>
            </section>

            {submitting && <div className="mt-4" role="status" aria-live="polite"><div className="flex justify-between text-xs font-semibold"><span>{generatingPiFiles ? "Generating Excel, CSV, and PDF PI files" : `Uploading and verifying ${filesLabel(selectedFiles.length + generatedPiFiles.length)}`}</span><span>{overallProgress}%</span></div><div className="mt-2 h-2.5 overflow-hidden rounded-full bg-ink-800" role="progressbar" aria-label="Overall inquiry upload progress" aria-valuemin={0} aria-valuemax={100} aria-valuenow={overallProgress}><div className="h-full bg-brand-400 transition-[width]" style={{ width: `${overallProgress}%` }} /></div><p className="mt-2 text-3xs text-text-muted">Keep this page open. Each selected file is uploaded and verified separately.</p></div>}
            <button disabled={submitting} className="mt-4 w-full rounded-xl bg-brand-400 px-5 py-3.5 text-sm font-bold text-ink-950 transition hover:bg-brand-300 disabled:opacity-50">{submitting ? `Sending request ${overallProgress}%` : intent === "proforma_invoice" ? "Send PI request to ProGlass" : "Submit inquiry"}</button>
          </form>
        )}
      </section>
      <section className="rounded-2xl border border-line-strong/60 bg-ink-900/85 p-5"><div className="flex items-center justify-between"><h2 className="text-lg font-bold">Your submissions</h2><button onClick={() => void load()} className="text-xs font-semibold text-brand-300">Refresh</button></div>
        <div className="mt-3 grid gap-2 sm:grid-cols-3"><select aria-label="Filter by project" value={historyProject} onChange={(event)=>setHistoryProject(event.target.value)} className="h-10 rounded-xl border border-line-strong bg-ink-950 px-2 text-xs"><option value="">All projects</option>{projects.map((project)=><option key={project.id} value={project.id}>{project.project_name}</option>)}</select><select aria-label="Filter by inquiry type" value={historyType} onChange={(event)=>setHistoryType(event.target.value)} className="h-10 rounded-xl border border-line-strong bg-ink-950 px-2 text-xs"><option value="">All types</option>{CUSTOMER_INQUIRY_TYPES.map((value)=><option key={value} value={value}>{CUSTOMER_INQUIRY_TYPE_LABELS[value]}</option>)}</select><select aria-label="Filter by status" value={historyStatus} onChange={(event)=>setHistoryStatus(event.target.value)} className="h-10 rounded-xl border border-line-strong bg-ink-950 px-2 text-xs"><option value="">All statuses</option><option value="submitted">Submitted</option><option value="under_review">Under review</option><option value="accepted">Accepted</option><option value="rejected">Rejected</option><option value="closed">Closed</option></select></div>
        {loading ? <p role="status" className="mt-5 text-xs text-text-muted">Loading…</p> : rows.length === 0 ? <p className="mt-5 text-xs text-text-muted">No website inquiries submitted by this account.</p> : filteredRows.length===0?<p className="mt-5 text-xs text-text-muted">No inquiries match these filters.</p>: <div className="mt-4 space-y-3">{filteredRows.map((row) => <LocaleLink key={row.id} href={`/inquiries/submissions/${row.id}`} className="block rounded-xl border border-line-strong bg-ink-950/70 p-4"><div className="flex justify-between gap-3"><span className="font-mono text-xs font-bold text-brand-300">{row.submission_reference}</span><span className="rounded-full border border-line-strong px-2 py-0.5 text-3xs uppercase text-text-muted">{row.status.replaceAll("_", " ")}</span></div><div className="mt-2 text-sm font-semibold">{row.project_name}</div><div className="mt-1 text-3xs text-text-faint">{row.document_count} document{row.document_count === 1 ? "" : "s"} · submitted {date(row.submitted_at)}</div><div className="mt-2 text-3xs font-semibold text-text-muted">Response estimate: {row.estimated_response_at ? date(row.estimated_response_at) : "pending operator review"}</div>{row.operator_note && <div className="mt-1 text-xs text-text-muted">Operator note: {row.operator_note}</div>}</LocaleLink>)}</div>}
      </section>
    </div>
  </main>;
}

function Field({ name, label, required, maxLength, defaultValue, placeholder }: { name: string; label: string; required?: boolean; maxLength: number; defaultValue?: string; placeholder?: string }) {
  const id = `inquiry-${name.replaceAll("_", "-")}`;
  return <label htmlFor={id} className="block text-sm font-semibold leading-5 text-text-secondary">{label}<input id={id} name={name} required={required} maxLength={maxLength} defaultValue={defaultValue} placeholder={placeholder} className="mt-2 min-h-12 w-full rounded-xl border border-line-strong bg-ink-950 px-3 py-3 text-base leading-6 text-text-primary caret-brand-300 outline-none placeholder:text-text-faint focus-visible:border-brand-400 focus-visible:ring-2 focus-visible:ring-brand-400/40 sm:text-sm" /></label>;
}

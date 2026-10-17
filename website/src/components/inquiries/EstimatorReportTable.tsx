"use client";

import { useMemo, useState } from "react";
import { LocaleLink } from "@/components/ui/LocaleLink";
import type { InquiryEstimator, InquiryEstimatorQueueItem } from "@/types/inquiry-estimators";

type SortField = "received"|"age"|"reference"|"customer"|"company"|"project"|"location"|"type"|"primary"|"documents"|"due"|"activity"|"status";
type Direction = "asc"|"desc";
const day=86_400_000;

export function EstimatorReportTable({rows,estimators,nowMs}:{rows:InquiryEstimatorQueueItem[];estimators:InquiryEstimator[];nowMs:number}){
  const [sort,setSort]=useState<SortField>("received");
  const [direction,setDirection]=useState<Direction>("desc");
  const name=(id:string)=>estimators.find(e=>e.user_id===id)?.display_name||"Unassigned";
  const value=(row:InquiryEstimatorQueueItem,field:SortField):string|number=>{
    const primary=row.assignments.find(a=>a.assignment_role==="primary");
    switch(field){
      case "received":return Date.parse(row.submitted_at)||0;
      case "age":return nowMs-(Date.parse(row.submitted_at)||nowMs);
      case "reference":return row.submission_reference;
      case "customer":return row.customer_reference;
      case "company":return row.company_name;
      case "project":return row.project_name;
      case "location":return row.project_location;
      case "type":return row.inquiry_type;
      case "primary":return name(primary?.estimator_user_id||"");
      case "documents":return row.document_count;
      case "due":return row.estimated_response_at?Date.parse(row.estimated_response_at):Number.MAX_SAFE_INTEGER;
      case "activity":return Date.parse(row.status_updated_at)||0;
      case "status":return row.status;
    }
  };
  const ordered=useMemo(()=>[...rows].sort((a,b)=>{const av=value(a,sort),bv=value(b,sort);const compared=typeof av==="number"&&typeof bv==="number"?av-bv:String(av).localeCompare(String(bv));return direction==="asc"?compared:-compared}),[rows,sort,direction,nowMs,estimators]); // eslint-disable-line react-hooks/exhaustive-deps
  const change=(field:SortField)=>{if(field===sort)setDirection(v=>v==="asc"?"desc":"asc");else{setSort(field);setDirection("asc")}};
  const header=(label:string,field:SortField)=><button type="button" onClick={()=>change(field)} className="inline-flex whitespace-nowrap font-semibold uppercase tracking-wider hover:text-text-primary">{label}<span aria-hidden="true" className="ml-1">{sort===field?(direction==="asc"?"▲":"▼"):"↕"}</span></button>;
  const format=(iso:string|null)=>iso?new Intl.DateTimeFormat("en-GB",{day:"2-digit",month:"2-digit",year:"numeric"}).format(new Date(iso)):"—";
  return <section aria-label="Estimator inquiry report" className="mt-5 overflow-hidden rounded-2xl border border-line-strong bg-ink-900">
    <div className="flex items-center justify-between border-b border-line px-4 py-3"><h2 className="text-sm font-bold">Operational inquiry report</h2><span className="font-mono text-3xs text-text-faint">{rows.length} visible</span></div>
    <div className="overflow-x-auto"><table className="w-full border-collapse text-left text-xs"><thead><tr className="border-b border-line-strong bg-ink-950 text-3xs text-text-faint">
      <th className="px-3 py-3">{header("Received","received")}</th><th className="px-3 py-3">{header("Age","age")}</th><th className="px-3 py-3">{header("Inquiry Ref","reference")}</th><th className="px-3 py-3">{header("Customer Ref","customer")}</th><th className="px-3 py-3">{header("Company","company")}</th><th className="px-3 py-3">{header("Project","project")}</th><th className="px-3 py-3">{header("Location","location")}</th><th className="px-3 py-3">{header("Inquiry Type","type")}</th><th className="px-3 py-3">{header("Primary","primary")}</th><th className="px-3 py-3">Collaborators</th><th className="px-3 py-3">{header("Documents","documents")}</th><th className="px-3 py-3">{header("Response Due","due")}</th><th className="px-3 py-3">{header("Last Activity","activity")}</th><th className="px-3 py-3">{header("Status","status")}</th><th className="px-3 py-3">Action</th>
    </tr></thead><tbody className="divide-y divide-line/50">{ordered.map(row=>{const primary=row.assignments.find(a=>a.assignment_role==="primary"),collaborators=row.assignments.filter(a=>a.assignment_role==="collaborator");return <tr key={row.id} className="hover:bg-ink-850"><td className="whitespace-nowrap px-3 py-2 font-mono text-3xs">{format(row.submitted_at)}</td><td className="whitespace-nowrap px-3 py-2 font-mono">{Math.max(0,Math.floor((nowMs-Date.parse(row.submitted_at))/day))}d</td><td className="whitespace-nowrap px-3 py-2 font-mono font-semibold text-brand-300">{row.submission_reference}</td><td className="max-w-32 truncate px-3 py-2">{row.customer_reference||"—"}</td><td className="max-w-40 truncate px-3 py-2">{row.company_name}</td><td className="max-w-40 truncate px-3 py-2">{row.project_name}</td><td className="max-w-36 truncate px-3 py-2">{row.project_location||"—"}</td><td className="whitespace-nowrap px-3 py-2">{row.inquiry_type.replaceAll("_"," ")}</td><td className="whitespace-nowrap px-3 py-2">{name(primary?.estimator_user_id||"")}</td><td className="max-w-48 truncate px-3 py-2">{collaborators.map(a=>name(a.estimator_user_id)).join(", ")||"—"}</td><td className="px-3 py-2 text-right font-mono">{row.document_count}</td><td className="whitespace-nowrap px-3 py-2 font-mono text-3xs">{format(row.estimated_response_at)}</td><td className="whitespace-nowrap px-3 py-2 font-mono text-3xs">{format(row.status_updated_at)}</td><td className="whitespace-nowrap px-3 py-2 uppercase">{row.status.replaceAll("_"," ")}</td><td className="px-3 py-2"><LocaleLink href={`/inquiries/estimator/${row.id}?from=report`} className="whitespace-nowrap rounded-md border border-line-strong px-2 py-1 text-3xs font-semibold text-brand-200 hover:border-brand-400">Inspect</LocaleLink></td></tr>})}</tbody></table></div>
    {!ordered.length&&<p className="p-8 text-center text-sm text-text-muted">No inquiries match the active report filters.</p>}
  </section>;
}

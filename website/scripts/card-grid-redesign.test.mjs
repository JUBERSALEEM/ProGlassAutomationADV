import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
const read=(file)=>readFileSync(new URL(`../${file}`,import.meta.url),"utf8");
const routes={
  "home":["src/app/[locale]/page.tsx"],
  about:["src/app/[locale]/about/page.tsx"],
  blog:["src/app/[locale]/blog/page.tsx"],
  collaborate:["src/app/[locale]/collaborate/page.tsx"],
  contact:["src/app/[locale]/contact/page.tsx"],
  deliveries:["src/app/[locale]/deliveries/page.tsx","src/components/deliveries/DeliveriesSummaryCards.tsx"],
  developer:["src/app/[locale]/developer/page.tsx"],
  features:["src/app/[locale]/features/page.tsx","src/components/product/ModuleBrowser.tsx"],
  "how-it-works":["src/app/[locale]/how-it-works/page.tsx"],
  inquiries:["src/app/[locale]/inquiries/page.tsx","src/components/inquiries/InquiriesDashboard.tsx","src/components/inquiries/InboxSummary.tsx"],
  "job-orders":["src/app/[locale]/job-orders/page.tsx","src/components/job-orders/JobOrdersSummaryCards.tsx"],
  optimization:["src/app/[locale]/optimization/page.tsx"],
  pricing:["src/app/[locale]/pricing/page.tsx"],
  records:["src/app/[locale]/records/page.tsx","src/components/records/RecordsSummaryCards.tsx"],
  reports:["src/app/[locale]/reports/page.tsx","src/components/records/RecordsSummaryCards.tsx"],
  resources:["src/app/[locale]/resources/page.tsx"],
  screenshots:["src/app/[locale]/screenshots/page.tsx","src/components/gallery/ScreenshotGallery.tsx"],
  sponsors:["src/app/[locale]/sponsors/page.tsx"],
  "worked-example":["src/app/[locale]/worked-example/page.tsx"],
};
test("all 19 top-level pages have an audited compact card collection",()=>{assert.equal(Object.keys(routes).length,19);for(const[name,files]of Object.entries(routes)){const source=files.map(read).join("\n");assert.match(source,/dense-card-grid/,`${name} has no audited compact collection`)}});
test("normal collections show four cards from the 640px tier",()=>{for(const files of Object.values(routes)){const source=files.map(read).join("\n");assert.match(source,/grid-cols-2[^"\n]*sm:grid-cols-4|grid-cols-3[^"\n]*sm:grid-cols-[56]/)}});
test("dense collections fill each row according to their actual card count",()=>{const css=read("src/app/responsive-card-grid.css");assert.match(css,/\.dense-card-grid:not\(\.dense-fixed-grid\)/);assert.match(css,/repeat\(auto-fit, minmax\(min\(100%, 9\.5rem\), 1fr\)\)/);assert.match(css,/gap:\s*0\.75rem/)});
test("catalogue proof is compact only at the four-card tablet tier",()=>{const css=read("src/app/responsive-card-grid.css"),browser=read("src/components/product/ModuleBrowser.tsx");assert.match(css,/min-width:\s*40rem[\s\S]*max-width:\s*63\.999rem/);assert.match(browser,/module-card-supporting/);assert.match(browser,/module-card-status/)});

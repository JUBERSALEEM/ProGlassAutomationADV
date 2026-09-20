# ERP XAML migration ledger

Branch: `arena/01a0bb84-proglassautomationadv`  
Inventory: **337** XAML (guard). Settings / About / Ios26 / GlassBlush / print invoice: **not** restyled.

| Phase | Batch | SHA | Push | Static | Build | Test | Runtime |
|---|---|---|---|---|---|---|---|
| Shared chrome | GDS last-win + ErpPageRoot ~89 | `e45fac6` | yes | OK | NOT EXECUTED | NOT EXECUTED | NOT EXECUTED |
| PI 1–3 fields | Field.* + JO/DN/TI tab band | `b72643d` | yes | OK | NOT EXECUTED | NOT EXECUTED | NOT EXECUTED |
| Document forms | JO/DN/TI/Company/User/Sheet Field.* | `3853699` | yes | OK | NOT EXECUTED | NOT EXECUTED | NOT EXECUTED |
| Remaining captions | P&L / CSV / audit / crash Field.Label | `7c1dc0b` | yes | OK | NOT EXECUTED | NOT EXECUTED | NOT EXECUTED |
| List/report chrome | Filter Input.* last-win; JO titles/KPI; DN shell | `13b112e` | yes | OK | NOT EXECUTED | NOT EXECUTED | NOT EXECUTED |
| Production density | Drop local Height=34 on PCC/process/specs/opt tabs | `f263c5e` | yes | OK | NOT EXECUTED | NOT EXECUTED | NOT EXECUTED |
| List/inquiry chrome | Order report header/KPI; inquiry Field.*; executive dates | `fb97e84` | yes | OK | NOT EXECUTED | NOT EXECUTED | NOT EXECUTED |
| Report filter density | Drop Height=40 on report/sheet filters; inquiry table header; reports hub band | `177f9cb` | yes | OK | NOT EXECUTED | NOT EXECUTED | NOT EXECUTED |
| Phase 1 list overflow | Star-ratio columns; CanContentScroll=False; PI empty overlay; JO token dialect | `9781485` | yes | OK | NOT EXECUTED | NOT EXECUTED | NOT EXECUTED |
| Phase 1 list follow-through | Inquiry header cols; KPI N0; generator White; pixel-scroll; inquiry editor tokens | `39e5e35` | yes | OK | NOT EXECUTED | NOT EXECUTED | NOT EXECUTED |
| Phase 1 archetype rewrite | PI list = Archetype 1; JO list = Archetype 2 | `ec3b9e8` / `71108ac` | yes | OK | NOT EXECUTED | NOT EXECUTED | NOT EXECUTED |
| Phase 1 tabbed/rail lists | TI + DN full Archetype 2; Inquiry list+rail chrome | `04afcd5` / `9277363` | yes | OK | NOT EXECUTED | NOT EXECUTED | NOT EXECUTED |
| Phase 1 report registers | Statement + Order Report lists = Archetype 1 | `95c3d6e` | yes | OK | NOT EXECUTED | NOT EXECUTED | NOT EXECUTED |
| Phase 2 document shells | JO/DN/TI detail workspace; JO glass header=row | `c76195f` | yes | OK | NOT EXECUTED | NOT EXECUTED | NOT EXECUTED |
| Phase 2 detail sections | DN attachments/history; TI attachments/family | `18819d8` | yes | OK | NOT EXECUTED | NOT EXECUTED | NOT EXECUTED |
| Phase 2 type scale | Static Text* FontSize → FontSize.* (12 views) | `d4eabc1` | yes | OK | NOT EXECUTED | NOT EXECUTED | NOT EXECUTED |
| Phase 2 list density | ListPageStyles GDS type; report KPI 38→KpiIconStyle | `7f1ebdc` | yes | OK | NOT EXECUTED | NOT EXECUTED | NOT EXECUTED |
| Phase 2 dialect pass | SecondaryForeground→TextSecondary; Segoe→Icon; production filter/detail | `d6ce242` | yes | OK | NOT EXECUTED | NOT EXECUTED | NOT EXECUTED |
| Phase 2 surface strip 10 | Competing SurfacePrimaryStyle on 10 report/sheet/P&L views | working-tree | n/a | OK | NOT EXECUTED | NOT EXECUTED | NOT EXECUTED |
| Phase 2 surface strip 10b | VAT/login/cutting/preview/audit/use/2D competing SurfacePrimaryStyle | `84bfff9` | yes | OK | NOT EXECUTED | NOT EXECUTED | NOT EXECUTED |
| Phase 2 surface strip 10c | FirstRun/PCC/CompanyReport/WizardStyles/History/Jobs/templates/process/charges/print | `c2323ff` | yes | OK | NOT EXECUTED | NOT EXECUTED | NOT EXECUTED |
| Phase 2 surface strip 10d | Customer/Profitability/Project reports; activity/storage; AiChat; CompanyCenter; rename; inquiry preview; portfolio service | `2b8eb4b` | yes | OK | NOT EXECUTED | NOT EXECUTED | NOT EXECUTED |
| Phase 2 surface strip 10e | AuthSurface/LoginPanel; DataGrid/Wastages; Executive/JO/PI/Salesman/TI reports; ChangesDialog | `b5b408e` | yes | OK | NOT EXECUTED | NOT EXECUTED | NOT EXECUTED |
| Phase 2 surface strip 10f | StorageSidebar/UserSwitch/BuyDialog/Approval; Crash styles; wastage; pipeline; cutting/lam/edge | `d58fb45` | yes | OK | NOT EXECUTED | NOT EXECUTED | NOT EXECUTED |
| Phase 2 surface strip 10g | Holes/mitre/pair/polish/temper; CompanySetup; GlassSpec; Order preview; Statement shell/report | `0c4aa96` | yes | OK | NOT EXECUTED | NOT EXECUTED | NOT EXECUTED |
| Phase 2 surface strip 10h | Activity/QuickSettings/ScreenRecord/Screenshot/Share; Crash; DN/JO GodView; JO picker; opt report | `067cfe8` | yes | OK | NOT EXECUTED | NOT EXECUTED | NOT EXECUTED |
| Phase 2 surface strip 10i | Summary/ReleaseBar; PI dialogs/pricing/summary; order generator/execute; ComingSoon; VoidReason | `dd1d9bf` | yes | OK | NOT EXECUTED | NOT EXECUTED | NOT EXECUTED |
| Phase 2 surface strip tail | AddEditSheet; TI GodView; IssueDrafts; UserCenter; Terms MinHeight 40 | *(this commit)* | pending | pending | NOT EXECUTED | NOT EXECUTED | NOT EXECUTED |

**Done:** Competing SurfacePrimaryStyle strip closed on in-scope Views (sole card chrome kept). Terms clause TextBox MinHeight → DensityInputHeight.

**Next:** leftover Height 38/form density if any; Settings/Ios26/print deferred. No generator KPI Typography.Label BasedOn.

**Do not:** ErpPageRoot on nested Controls/Sections; Settings Ios26*; PrintableInvoiceView; force-push.

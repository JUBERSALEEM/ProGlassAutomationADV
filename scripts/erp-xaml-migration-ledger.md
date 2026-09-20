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
| Phase 1 archetype rewrite | PI list = Archetype 1 full file; JO list = Archetype 2 full file; TI list bug pass | *(this commit)* | pending | pending | NOT EXECUTED | NOT EXECUTED | NOT EXECUTED |

**Done:** PI/JO register lists rewritten to master archetypes (star columns, Z-layers, filter scroller, BrushKeyConverter stripe, 4px gutters). Bindings/x:Name/handlers preserved.

**Next:** DN/Inquiry/Statement/TI full archetype files; then Phase 2 details. No generator KPI Typography.Label BasedOn; Settings/Ios26/print after tests.

**Do not:** ErpPageRoot on nested Controls/Sections; Settings Ios26*; PrintableInvoiceView; force-push.

using System.Collections.Generic;
using System.Linq;

namespace ProGlassAutomation.Agent.Demo
{
    /// <summary>
    /// Workflows discovered from MainViewModel.Navigate — not invented modules.
    /// Each step is a navigation key the sidebar already accepts.
    /// </summary>
    public sealed class DemoWorkflow
    {
        public string Id { get; init; } = "";
        public string Title { get; init; } = "";
        public string Purpose { get; init; } = "";
        public string NavigateTarget { get; init; } = "";
        public IReadOnlyList<string> NarrationBeats { get; init; } = [];
    }

    public static class DemoWorkflowCatalog
    {
        public static readonly IReadOnlyList<DemoWorkflow> All = new[]
        {
            W("Dashboard", "Dashboard", "Factory pulse and module entry.",
                "Dashboard",
                "Open the dashboard to see the factory pulse.",
                "The tiles are the same figures the Overview module computes."),
            W("SheetStore", "Sheet Store", "Inventory of glass sheets.",
                "SheetStore",
                "Open Sheet Store.",
                "Stock, used and balance are the live sheet fields.",
                "Available quantity is Stock minus Used."),
            W("ProformaInvoice", "Proforma Invoice", "Customer quotation through save and PDF.",
                "ProformaInvoice",
                "Open Proforma Invoice.",
                "A quotation starts with the customer and project.",
                "Glass lines carry width, height, quantity, rate and value.",
                "Saving persists the snapshot the rest of the chain reads."),
            W("JobOrder", "Job Order", "Production sheet from an approved PI.",
                "JobOrder",
                "Open Job Orders.",
                "A job is converted from a proforma — it has no selling price on the floor sheet."),
            W("DeliveryNote", "Delivery Note", "Dispatch, delivered quantity and remaining balance.",
                "DeliveryNote",
                "Open Delivery Notes.",
                "Create starts as Dispatching. Make DN marks Dispatched.",
                "A signed attachment moves the note to Received."),
            W("TaxInvoice", "Tax Invoice", "FTA tax invoice from dispatched quantity.",
                "TaxInvoice",
                "Open Tax Invoices.",
                "A master invoice uses dispatched pieces only.",
                "Rate and value stay as separate columns on the FTA layout."),
            W("ReportsHub", "Reports Hub", "Unified reporting overview and entry to every report.",
                "SalesReport",
                "This is the Reports Hub — one overview of every operational and financial report the factory already produces, without opening each module by hand.",
                "From here a clerk opens Proforma, Job Order, Delivery, Tax Invoice or Statement analysis. The figures are the live documents, not a spreadsheet copy."),
            W("SalesReport", "Statement Generator", "Account statement of invoiced and outstanding.",
                "StatementGenerator",
                "Open the Statement Generator (Dispatch).",
                "The generator filters live tax invoices, previews matches and selects them."),
            W("JobProfitability", "Job Profitability", "Margin from Step 9 snapshots.",
                "JobProfitability",
                "Open Job Profitability.",
                "Figures are the profit Step 9 already persisted — they are not recalculated here."),
            W("CompanyCenter", "Company Center", "Supplier identity used on documents.",
                "CompanyCenter",
                "Open Company Center.",
                "Company name, TRN, bank and account print on invoices."),
            W("UserCenter", "User Center", "People who sign documents and audit entries.",
                "UserCenter", "Open User Center.", "Manage operators and document signers."),
            W("Optimization", "Optimization", "Plan efficient sheet yield and cutting layouts.", "Optimization",
                "This is the cutting-optimisation desk. The factory uses it to nest panes onto real sheet sizes so wastage stays visible before anyone scores a sheet.",
                "Yield, offcuts and remaining usable area sit on the same screen as the job, so a planner can change a layout without leaving production."),
            W("Cutting", "Cutting", "Track production cutting work.", "Cutting",
                "The Cutting floor lists every pane that has been scored and broken from the sheet. Operators mark progress here so the bay never loses a piece between the store and the furnace.",
                "Status, quantity and glass reference stay in one place, which is how the factory proves a job actually left the cutting table."),
            W("HolesCutouts", "Holes and Cutouts", "Manage drilling and cutout operations.", "HolesCutouts",
                "Holes and cutouts are a separate production stage because a drilled pane cannot go back on the cutter. This board tracks each opening so CNC and hand drilling stay on the same job.",
                "When a cutout is complete the pane moves forward; nothing is guessed from a handwritten ticket."),
            W("Polish", "Polish", "Track edge-polishing production.", "Polish",
                "Edge polish is billed and scheduled on its own because a polished edge changes both appearance and handling. This board shows which panes still need the machine.",
                "The same glass reference from the quotation follows the pane, so polish never runs on the wrong thickness."),
            W("EdgeDeletion", "Edge Deletion", "Track coating edge deletion.", "EdgeDeletion",
                "Coated glass needs the coating stripped at the edge before it can be sealed into a unit. This stage exists so that work is never skipped on a rush job.",
                "Operators tick off each pane here; the insulated-glass line will not pair a coated lite that still has coating on the seal."),
            W("Mitring", "Mitring", "Manage mitred-edge fabrication.", "Mitring",
                "Mitred edges are structural, not cosmetic — a wrong angle will not close on site. This board is where those panes are tracked through the saw.",
                "Quantity and angle stay with the glass reference so the installer receives the same cut the quotation promised."),
            W("Tempering", "Tempering", "Track furnace and tempering operations.", "Tempering",
                "The furnace is a bottleneck. This board shows which panes are waiting, in the oven, or already toughened, so the rest of the factory can plan around cycle time.",
                "A tempered pane cannot be recut. Completing the stage here is the factory's record that the glass is now safety glass."),
            W("Pairing", "Pairing", "Pair production panes for assembly.", "Pairing",
                "Pairing matches the inner and outer lites of an insulated unit before they are sealed. Wrong pairing is expensive because both panes are already cut.",
                "The board uses the same glass references as the job order so a six-millimetre clear never sits against a six-millimetre tinted by mistake."),
            W("DoublingLamination", "Doubling and Lamination", "Assemble DGU and laminated units.", "DoublingLamination",
                "Doubling and lamination is where single panes become a sealed unit or a safety laminate. This is the last production stage before packing.",
                "Spacer, gas fill and interlayer are confirmed here against the specification that was priced on the quotation — the rate only holds if the build matches."),
            W("PiReport", "Proforma Report", "Analyze quotations and values.", "PiReport",
                "The Proforma report is the sales desk's picture of every quotation — draft, sent, confirmed and expired — with values in dirhams.",
                "Use it to see which quotes are still open and which already converted to a job, without opening each invoice one by one."),
            W("JoReport", "Job Order Report", "Analyze production orders.", "JoReport",
                "The Job Order report shows what is on the factory floor: not started, in production, overdue and delivered.",
                "Production leads use it at the morning meeting so the cutting bay and the furnace share one list of work."),
            W("DeliveryReport", "Delivery Report", "Analyze dispatch and balances.", "DeliveryReport",
                "The Delivery report is dispatch: how many pieces left the factory, how many remain on the job, and which notes still need a signature.",
                "Balance quantities here must match the delivery note itself — this is the clerk's check before a tax invoice is raised."),
            W("TaxInvoiceReport", "Tax Invoice Report", "Analyze invoicing and VAT.", "TaxInvoiceReport",
                "The Tax Invoice report is the FTA view: issued, paid, overdue and VAT in dirhams, taken from live invoices not a spreadsheet.",
                "Accounts use it to see what has been billed against dispatched glass, so a job is never invoiced twice or not at all."),
            W("StatementReport", "Statement Report", "Analyze generated customer statements.", "StatementReport",
                "The Statement report lists customer statements that have already been generated — period, outstanding and what was included.",
                "It is the after-the-fact ledger for the statement generator, so a clerk can reopen a statement without rebuilding the period."),
            W("SystemCenter", "System Center", "Administer access and system users.", "SystemCenter",
                "System Center is where operators, roles and sign-in live. A shop-floor terminal is only as safe as the people listed here.",
                "Managers add and disable users without leaving the app, and every change is audit-logged against the person who made it."),
            W("Diagnostics", "Diagnostics", "Inspect application health and logs.", "Diagnostics",
                "Diagnostics is the health board: logs, connectivity and the last operations that succeeded or failed.",
                "Support reads this screen instead of asking the factory to describe an error from memory."),
            W("Settings", "Settings", "Configure application services and appearance.", "Settings",
                "This is Appearance. Theme, density, accent and wallpaper live here so the factory floor and the office can share one look without a restart.",
                "AI providers and keys are on this page. The assistant stays off until a clerk turns it on, so quotations never leave the machine by accident.",
                "Voice is the microphone and speech engine used by Demo Center and the assistant. Internal audio and the mic are captured from the same settings the recording HUD uses.",
                "Data and Backup is the snapshot of every database file. Restore is blocked while Demo Mode is on, so a recording cannot overwrite production.",
                "Updates checks GitHub for a new ProGlass build and can install it in place. The factory does not need to copy an installer by hand."),
            W("Features", "Settings — Features", "Toggle optional fields in Proforma Invoice and Inquiry, and manage the session.", "Settings",
                "This is Features for Proforma Invoice. Hide bank details, LPO and the attention line if the desk never fills them. Company name and Proforma ID stay on because every saved quotation needs them.",
                "Inquiry uses the same idea. Customer and project always show; every other field is a switch, so a small contractor is not staring at columns they never use.",
                "Job Order switches hide production-only fields the office does not need on the floor sheet. Quantity and glass reference stay visible because cutting cannot guess them.",
                "Delivery Note switches hide optional dispatch columns. Delivered quantity and remaining balance stay on — those two numbers are how the tax invoice is raised.",
                "Tax Invoice switches hide FTA columns the clerk does not type. Rate, value and VAT stay on the layout because the Authority reads them from the printed form."),
            W("License", "Settings — License", "Start a trial or activate a licence key.", "Settings",
                "This is License — the only place activation lives in the app. A clerk starts a trial or pastes a key here without calling support.",
                "A fresh machine gets thirty days with every module unlocked. When the trial ends the app becomes read-only: existing jobs stay visible, but new writes wait for a real key.",
                "The trial is one-time per machine. Config, program data and a protected blob all have to agree before another trial can be issued, so a factory cannot reset the clock by deleting a folder."),
            W("About", "About", "Review platform capabilities and release information.", "About",
                "This is About — the release, the modules this build includes, and the machine fingerprint support asks for when a licence will not activate.",
                "The same fingerprint is what a licence file is bound to. If the factory replaced a PC, this page is where you copy the new id.",
                "Support reads this page together with Diagnostics when a build will not start or a licence file is refused, so the factory does not have to describe the machine from memory."),
        };

        public static DemoWorkflow? Find(string? id)
        {
            // Exact catalog IDs must win over navigation aliases. Reports Hub
            // navigates to "SalesReport", while the statement workflow keeps
            // the historical SalesReport ID; one OR predicate made the first
            // entry shadow the statement generator.
            var exact = All.FirstOrDefault(w =>
                string.Equals(w.Id, id, System.StringComparison.OrdinalIgnoreCase));
            return exact ?? All.FirstOrDefault(w =>
                string.Equals(w.NavigateTarget, id, System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(w.Title, id, System.StringComparison.OrdinalIgnoreCase));
        }

        public static IEnumerable<DemoWorkflow> Resolve(string? argument)
        {
            if (string.IsNullOrWhiteSpace(argument)
                || string.Equals(argument, "All", System.StringComparison.OrdinalIgnoreCase))
                return All;

            var one = Find(argument);
            return one == null ? Enumerable.Empty<DemoWorkflow>() : new[] { one };
        }

        private static DemoWorkflow W(
            string id, string title, string purpose, string nav, params string[] beats) =>
            new()
            {
                Id = id,
                Title = title,
                Purpose = purpose,
                NavigateTarget = nav,
                NarrationBeats = beats
            };
    }
}

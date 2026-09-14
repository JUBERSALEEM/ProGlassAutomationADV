// Station identity, column packs, scan, cut-plan, print ticket, load/pairing
// display — no clamp / DN / distribution maths.

using ProGlassAutomation.Commands;
using ProGlassAutomation.Models.Production;
using ProGlassAutomation.Services.Audit;
using ProGlassAutomation.Utilities;
using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels.Production
{
    public partial class ProductionStageViewModelBase
    {
        public bool IsCuttingStation => Stage == ProductionStage.Cutting;
        public bool IsHolesStation => Stage == ProductionStage.HolesCutouts;
        public bool IsPolishStation => Stage == ProductionStage.Polish;
        public bool IsEdgeDeletionStation => Stage == ProductionStage.EdgeDeletion;
        public bool IsMitringStation => Stage == ProductionStage.Mitring;
        public bool IsTemperingStation => Stage == ProductionStage.Tempering;
        public bool IsPairingStation => Stage == ProductionStage.Pairing;
        public bool IsAssemblyStation => Stage == ProductionStage.DoublingLamination;

        /// <summary>
        /// Header of the IN column. The cutting sidebar is the slim
        /// totals-only list (2026-09-14), so there the column reads
        /// TOTAL — the pieces to cut at this stage.
        /// </summary>
        public string InColumnHeader => IsCuttingStation ? "TOTAL" : "IN";

        public bool ShowSizeColumn => IsCuttingStation || IsHolesStation || IsTemperingStation;
        public bool ShowGlassColumn => IsCuttingStation || IsEdgeDeletionStation || IsPairingStation || IsAssemblyStation;
        public bool ShowLmColumn => IsPolishStation || IsMitringStation;
        public bool ShowTreatmentColumn => IsHolesStation || IsEdgeDeletionStation;
        public bool ShowLoadColumn => IsTemperingStation;
        public bool ShowMatchColumn => IsPairingStation || IsAssemblyStation;

        /// <summary>Dispatch KPI belongs at the end of the chain, not at the saw.</summary>
        public bool ShowDispatchKpi =>
            IsPairingStation || IsAssemblyStation || IsTemperingStation;

        public string StationWorkOrderHint => Stage switch
        {
            ProductionStage.Cutting => "Click a job to record cut pieces (W×H and glass on the list).",
            ProductionStage.HolesCutouts => "Click a job to record holes and cutouts.",
            ProductionStage.Polish => "Click a job to record polish — LM is on the list.",
            ProductionStage.EdgeDeletion => "Click a job to record edge deletion.",
            ProductionStage.Mitring => "Click a job to record mitring — LM is on the list.",
            ProductionStage.Tempering => "Click a job to record tempered pieces.",
            ProductionStage.Pairing => "Click a job to record paired units.",
            ProductionStage.DoublingLamination => "Click a job to record assembled units.",
            _ => "Click a job to open the detail panel."
        };

        public string EmptyQueueTitle => "No work at " + ModuleName.ToLowerInvariant() + " yet";

        public string InflowColumnHeader =>
            IsPolishStation || IsMitringStation ? "IN LM/pcs" : IsAssemblyStation || IsPairingStation ? "IN" : "TOTAL";

        public string HeroMetricCaption => Stage switch
        {
            ProductionStage.Polish or ProductionStage.Mitring => "linear metres + pieces",
            ProductionStage.Tempering => "furnace load",
            ProductionStage.Pairing or ProductionStage.DoublingLamination => "units",
            _ => "pieces in this job"
        };

        public bool HasAndon => HasWaitingUpstream || HasActionWarning;

        public string AndonLabel
        {
            get
            {
                if (HasActionWarning && HasWaitingUpstream)
                    return ActionWarning + "  ·  " + WaitingUpstreamLabel;
                if (HasActionWarning) return ActionWarning;
                return WaitingUpstreamLabel;
            }
        }

        public string FloorOperatorLabel =>
            string.IsNullOrWhiteSpace(AuditService.CurrentUsername)
                ? "operator" : AuditService.CurrentUsername;

        public string FloorClockLabel =>
            DateTime.Now.ToString("dd MMM yyyy HH:mm", CultureInfo.InvariantCulture);

        private string _scanText = "";
        public string ScanText
        {
            get => _scanText;
            set { if (_scanText == value) return; _scanText = value; OnPropertyChanged(); }
        }

        public ICommand CommitScanCommand { get; private set; } = null!;
        public ICommand OpenCutPlanCommand { get; private set; } = null!;
        public ICommand PrintTicketCommand { get; private set; } = null!;
        public ICommand CompleteLoadCommand { get; private set; } = null!;

        private void InitStationCommands()
        {
            CommitScanCommand = new RelayCommand(_ => CommitScan());
            OpenCutPlanCommand = new RelayCommand(
                _ => OnOpenStage?.Invoke("Optimization"),
                _ => IsCuttingStation);
            PrintTicketCommand = new RelayCommand(
                _ => PrintStationTicket(),
                _ => HasSelection);
            CompleteLoadCommand = new RelayCommand(
                _ => AsyncUtility.SafeFireAndForget(CompleteUnitAsync, "Production complete load"),
                _ => HasSelection && SelectedStageItems.Any(i => i.CanWork && i.Done < i.Inflow));
        }

        private void CommitScan()
        {
            string raw = (ScanText ?? "").Trim();
            if (raw.Length == 0) return;
            SearchText = raw;
            AsyncUtility.SafeFireAndForget(async () =>
            {
                await LoadListAsync().ConfigureAwait(true);
                var match = ListRows.FirstOrDefault(r =>
                    r.JobRef.IndexOf(raw, StringComparison.OrdinalIgnoreCase) >= 0
                    || r.Customer.IndexOf(raw, StringComparison.OrdinalIgnoreCase) >= 0
                    || r.GlassLabel.IndexOf(raw, StringComparison.OrdinalIgnoreCase) >= 0);
                if (match != null) OpenUnit(match);
            }, "Production scan");
        }

        private void PrintStationTicket()
        {
            if (!HasSelection) return;
            var doc = new FlowDocument
            {
                FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
                FontSize = 14,
                PagePadding = new Thickness(48)
            };
            doc.Blocks.Add(new Paragraph(new Bold(new Run(ModuleName.ToUpperInvariant() + "  ·  STATION TICKET")))
            { Margin = new Thickness(0, 0, 0, 12) });
            doc.Blocks.Add(new Paragraph(new Run(
                "Job " + SelectedJobNo + "\n" +
                SelectedCustomer + "\n" +
                SelectedSpecLabel + "\n" +
                "Size  " + (SelectedRow?.SizeLabel ?? "—") + "\n" +
                "Glass " + (SelectedRow?.GlassLabel ?? "—") + "\n" +
                "IN " + SelectedUnitTotalLabel + "   DONE " + SelectedUnitDoneLabel +
                "   BALANCE " + SelectedUnitBalanceLabel + "\n" +
                "Operator " + FloorOperatorLabel + "   " + FloorClockLabel)));
            doc.Blocks.Add(new Paragraph(new Run("Barcode: *" + SelectedJobNo + "*"))
            { Margin = new Thickness(0, 16, 0, 0) });

            var dlg = new System.Windows.Controls.PrintDialog();
            if (dlg.ShowDialog() == true)
            {
                var paginator = ((IDocumentPaginatorSource)doc).DocumentPaginator;
                dlg.PrintDocument(paginator, ModuleName + " ticket " + SelectedJobNo);
            }
        }

        public string PairingBoardSummary
        {
            get
            {
                int outerBal = OuterStageItems.Sum(r => r.Balance);
                int innerBal = InnerStageItems.Sum(r => r.Balance);
                return "Outer left " + outerBal.ToString(CultureInfo.InvariantCulture) +
                       "  ·  Inner left " + innerBal.ToString(CultureInfo.InvariantCulture);
            }
        }

        public string TemperingLoadSummary =>
            (SelectedSqmSummary ?? "—") + "  ·  " + (SelectedPcsSummary ?? "—");
    }
}

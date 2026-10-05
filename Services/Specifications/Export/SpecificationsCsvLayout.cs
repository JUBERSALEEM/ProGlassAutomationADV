// ═══════════════════════════════════════════════════════════════════════
//  FILE: Services/Specifications/Export/SpecificationsCsvLayout.cs
//  PROJECT: ProGlassAutomation
//  CREATED: 2026-09-02 — Specifications Center import/export
//
//  CSV layout for the specification catalogue, following the same
//  ICsvLayout contract every other module uses (DeliveryNote, JobOrder,
//  ProformaInvoice) so it plugs straight into CsvExportEngine.
//
//  Column set is deliberately ROUND-TRIPPABLE: the importer reads exactly
//  these headers back, so an exported file can be edited in Excel and
//  re-imported without losing anything the catalogue stores.
// ═══════════════════════════════════════════════════════════════════════

using CsvHelper;
using ProGlassAutomation.Models.ProformaInvoice.Specifications;
using ProGlassAutomation.Services.Export.Core;
using ProGlassAutomation.Services.Export.Csv;
using System;
using System.Collections.Generic;

namespace ProGlassAutomation.Services.Specifications.Export
{
    public class SpecificationsCsvLayout : ICsvLayout
    {
        private readonly IReadOnlyList<SavedSpecification> _rows;

        public SpecificationsCsvLayout(IReadOnlyList<SavedSpecification> rows)
        {
            _rows = rows ?? Array.Empty<SavedSpecification>();
        }

        // ── IDocumentLayout ──────────────────────────────────────────────
        public string DocumentPrefix => "SPEC";
        public string DocumentId => "Specifications";
        public string DocumentTitle => "Specifications Catalogue";
        public string CompanyName => string.Empty;
        public string Reference => string.Empty;
        public DateTime DocumentDate => DateTime.Now;

        /// <summary>
        /// Header names the importer matches on. Kept as public constants so
        /// the two sides cannot drift apart silently.
        /// </summary>
        public static readonly string[] Headers =
        {
            "GlassType",
            "SpecName",
            "PricePerSqm",
            "Color",
            "ThicknessMm",
            "SheetWidthMm",
            "SheetHeightMm",
            "Category",
            "ProformaId",
            "UsageCount",
            "CreatedAt",
            "LastUsedAt"
        };

        public void WriteRows(CsvWriter writer)
        {
            foreach (var h in Headers) writer.WriteField(h);
            writer.NextRecord();

            foreach (var s in _rows)
            {
                writer.WriteField(s.GlassType ?? "");
                writer.WriteField(s.SpecName ?? "");
                writer.WriteField(s.PricePerSqm);
                writer.WriteField(s.Color ?? "");
                writer.WriteField(s.ThicknessMm);
                writer.WriteField(s.SheetWidthMm);
                writer.WriteField(s.SheetHeightMm);
                writer.WriteField(s.Category ?? "");
                writer.WriteField(s.ProformaId ?? "");
                writer.WriteField(s.UsageCount);
                writer.WriteField(s.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"));
                writer.WriteField(s.LastUsedAt.ToString("yyyy-MM-dd HH:mm:ss"));
                writer.NextRecord();
            }
        }
    }
}

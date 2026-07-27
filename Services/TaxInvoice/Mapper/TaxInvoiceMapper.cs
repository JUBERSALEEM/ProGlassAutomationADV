using ProGlassAutomation.Models.TaxInvoice;
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace ProGlassAutomation.Services.TaxInvoice.Mapper
{
    public class TaxInvoiceSnapshot
    {
        public int SchemaVersion { get; set; } = 1;
        public string InvoiceNumber { get; set; } = "";
        public DateTime InvoiceDate { get; set; }
        public string DeliveryNoteId { get; set; } = "";
        public string JobOrderId { get; set; } = "";
        public string ProformaId { get; set; } = "";
        public string CustomerName { get; set; } = "";
        public string ProjectName { get; set; } = "";
        public string Salesman { get; set; } = "";
        public string CustomerTrn { get; set; } = "";
        public string CompanyTrn { get; set; } = "";
        public decimal Subtotal { get; set; }
        public decimal Discount { get; set; }
        public decimal TaxableAmount { get; set; }
        public decimal VatAmount { get; set; }
        public decimal GrandTotal { get; set; }
        public string Currency { get; set; } = "AED";
        public decimal ExchangeRate { get; set; } = 1.0m;
        public string PaymentStatus { get; set; } = "Unpaid";
        public List<TaxInvoiceItemSnapshot> Items { get; set; } = new();
    }

    public class TaxInvoiceItemSnapshot
    {
        public int SpecGroupNumber { get; set; }
        public string SpecGroupName { get; set; } = "";
        public int SerialNumber { get; set; }
        public string GlassRef { get; set; } = "";
        public int W1 { get; set; }
        public int H1 { get; set; }
        public int W2 { get; set; }
        public int H2 { get; set; }
        public int DeliveredQty { get; set; }
        public int InvoicedQty { get; set; }
        public decimal Rate { get; set; }
        public decimal Amount { get; set; }
        public string Notes { get; set; } = "";
    }

    public static class TaxInvoiceMapper
    {
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = false,
            PropertyNameCaseInsensitive = true
        };

        public static Models.TaxInvoice.TaxInvoice FromSnapshot(TaxInvoiceSnapshot snapshot)
        {
            var entity = new Models.TaxInvoice.TaxInvoice
            {
                InvoiceNumber = snapshot.InvoiceNumber,
                InvoiceDate = snapshot.InvoiceDate,
                DeliveryNoteId = snapshot.DeliveryNoteId,
                JobOrderId = snapshot.JobOrderId,
                ProformaId = snapshot.ProformaId,
                CustomerName = snapshot.CustomerName,
                ProjectName = snapshot.ProjectName,
                Salesman = snapshot.Salesman,
                CustomerTrn = snapshot.CustomerTrn,
                CompanyTrn = snapshot.CompanyTrn,
                Subtotal = snapshot.Subtotal,
                Discount = snapshot.Discount,
                TaxableAmount = snapshot.TaxableAmount,
                VatAmount = snapshot.VatAmount,
                GrandTotal = snapshot.GrandTotal,
                Currency = snapshot.Currency,
                ExchangeRate = snapshot.ExchangeRate,
                PaymentStatus = snapshot.PaymentStatus
            };

            foreach (var item in snapshot.Items)
            {
                entity.Items.Add(new TaxInvoiceItem
                {
                    TaxInvoiceId = snapshot.InvoiceNumber,
                    SpecGroupNumber = item.SpecGroupNumber,
                    SpecGroupName = item.SpecGroupName,
                    SerialNumber = item.SerialNumber,
                    GlassRef = item.GlassRef,
                    W1 = item.W1,
                    H1 = item.H1,
                    W2 = item.W2,
                    H2 = item.H2,
                    DeliveredQty = item.DeliveredQty,
                    InvoicedQty = item.InvoicedQty,
                    Rate = item.Rate,
                    Amount = item.Amount,
                    Notes = item.Notes
                });
            }

            entity.FullSnapshotJson = JsonSerializer.Serialize(snapshot, _jsonOptions);
            return entity;
        }

        public static TaxInvoiceSnapshot ToSnapshot(Models.TaxInvoice.TaxInvoice entity)
        {
            var snapshot = new TaxInvoiceSnapshot
            {
                InvoiceNumber = entity.InvoiceNumber,
                InvoiceDate = entity.InvoiceDate,
                DeliveryNoteId = entity.DeliveryNoteId,
                JobOrderId = entity.JobOrderId,
                ProformaId = entity.ProformaId,
                CustomerName = entity.CustomerName,
                ProjectName = entity.ProjectName,
                Salesman = entity.Salesman,
                CustomerTrn = entity.CustomerTrn,
                CompanyTrn = entity.CompanyTrn,
                Subtotal = entity.Subtotal,
                Discount = entity.Discount,
                TaxableAmount = entity.TaxableAmount,
                VatAmount = entity.VatAmount,
                GrandTotal = entity.GrandTotal,
                Currency = entity.Currency,
                ExchangeRate = entity.ExchangeRate,
                PaymentStatus = entity.PaymentStatus
            };

            foreach (var item in entity.Items)
            {
                snapshot.Items.Add(new TaxInvoiceItemSnapshot
                {
                    SpecGroupNumber = item.SpecGroupNumber,
                    SpecGroupName = item.SpecGroupName,
                    SerialNumber = item.SerialNumber,
                    GlassRef = item.GlassRef,
                    W1 = item.W1,
                    H1 = item.H1,
                    W2 = item.W2,
                    H2 = item.H2,
                    DeliveredQty = item.DeliveredQty,
                    InvoicedQty = item.InvoicedQty,
                    Rate = item.Rate,
                    Amount = item.Amount,
                    Notes = item.Notes
                });
            }

            return snapshot;
        }

        public static TaxInvoiceSnapshot DeserializeSnapshot(Models.TaxInvoice.TaxInvoice entity)
        {
            if (string.IsNullOrWhiteSpace(entity.FullSnapshotJson) || entity.FullSnapshotJson == "{}")
                return ToSnapshot(entity);

            try
            {
                return JsonSerializer.Deserialize<TaxInvoiceSnapshot>(entity.FullSnapshotJson, _jsonOptions) ?? ToSnapshot(entity);
            }
            catch
            {
                return ToSnapshot(entity);
            }
        }

        public static void RefreshSnapshot(Models.TaxInvoice.TaxInvoice entity)
        {
            var snap = ToSnapshot(entity);
            entity.FullSnapshotJson = JsonSerializer.Serialize(snap, _jsonOptions);
        }
    }
}
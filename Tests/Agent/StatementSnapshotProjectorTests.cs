// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Agent/StatementSnapshotProjectorTests.cs
// PROJECT: ProGlassAutomation.Tests
//
// Pins StatementSnapshotProjector.FilterByInvoices (the projection logic
// extracted from the StatementGeneratorViewModel): it must restrict rows to
// the selected invoice numbers (case-insensitive), recompute the financial
// summary and aging buckets over only the selected rows, and preserve the
// header/customer info.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using ProGlassAutomation.Models.Statement;
using ProGlassAutomation.Services.Statement.Workflow;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    public class StatementSnapshotProjectorTests
    {
        private static StatementSnapshot Sample()
        {
            var today = DateTime.Today;

            var rows = new List<StatementInvoiceRow>
            {
                new() // overdue 10 days
                {
                    InvoiceNumber = "INV-001",
                    Subtotal = 100m, VatAmount = 5m, GrandTotal = 105m,
                    PaidAmount = 0m, DueDate = today.AddDays(-10),
                    DaysOverdue = 10, TotalQty = 2, TotalSqm = 3.5m
                },
                new() // current (not due yet)
                {
                    InvoiceNumber = "inv-002",  // deliberately lowercase
                    Subtotal = 200m, VatAmount = 10m, GrandTotal = 210m,
                    PaidAmount = 60m, DueDate = today.AddDays(5),
                    DaysOverdue = 0, TotalQty = 4, TotalSqm = 7.0m
                },
                new() // >90 days overdue, should be excluded when unselected
                {
                    InvoiceNumber = "INV-003",
                    Subtotal = 50m, VatAmount = 2.5m, GrandTotal = 52.5m,
                    PaidAmount = 0m, DueDate = today.AddDays(-120),
                    DaysOverdue = 120, TotalQty = 1, TotalSqm = 1.0m
                }
            };

            return new StatementSnapshot
            {
                Header = new StatementHeader { StatementNumber = "STMT-1" },
                CustomerInfo = new StatementCustomerInfo { CustomerName = "Acme" },
                InvoiceRows = rows
            };
        }

        [Fact]
        public void FilterByInvoices_restricts_rows_to_selected_numbers()
        {
            var original = Sample();
            var projected = StatementSnapshotProjector.FilterByInvoices(
                original, new List<string> { "INV-001", "inv-002" });

            // Two selected; matching is case-insensitive.
            Assert.Equal(2, projected.InvoiceRows.Count);
            Assert.Equal(2, projected.Financial.TotalInvoiceCount);

            // Totals only over selected rows.
            Assert.Equal(300m, projected.Financial.TotalSubtotal);
            Assert.Equal(15m, projected.Financial.TotalVat);
            Assert.Equal(315m, projected.Financial.TotalInvoiced);
            Assert.Equal(60m, projected.Financial.TotalPaid);
            Assert.Equal(255m, projected.Financial.TotalOutstanding); // 105 + 150
            Assert.Equal(6, projected.Financial.TotalQty);
            Assert.Equal(10.5m, projected.Financial.TotalSqm);
        }

        [Fact]
        public void FilterByInvoices_recomputes_aging_buckets_over_selected()
        {
            var original = Sample();
            var projected = StatementSnapshotProjector.FilterByInvoices(
                original, new List<string> { "INV-001", "INV-003" });

            // INV-001 (10 days overdue) => Days0To30; INV-003 (120) => Days90Plus.
            Assert.Equal(105m, projected.Aging.Days0To30);
            Assert.Equal(52.5m, projected.Aging.Days90Plus);
            Assert.Equal(0m, projected.Aging.Current);
            Assert.Equal(157.5m, projected.Financial.TotalOutstanding);
        }

        [Fact]
        public void FilterByInvoices_preserves_header_and_customer_info()
        {
            var projected = StatementSnapshotProjector.FilterByInvoices(
                Sample(), new List<string> { "INV-001" });

            Assert.Equal("STMT-1", projected.Header.StatementNumber);
            Assert.Equal("Acme", projected.CustomerInfo.CustomerName);
        }
    }
}

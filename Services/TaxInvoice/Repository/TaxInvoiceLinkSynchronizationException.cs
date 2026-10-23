using System;
using ProGlassAutomation.Models.TaxInvoice.SavedTaxInvoice;

namespace ProGlassAutomation.Services.TaxInvoice.Repository
{
    /// <summary>
    /// The Tax Invoice commit succeeded, but the companion Delivery Note link
    /// transaction did not. Carries the persisted row so the UI can switch to
    /// edit mode and avoid creating a duplicate on retry.
    /// </summary>
    public sealed class TaxInvoiceLinkSynchronizationException : InvalidOperationException
    {
        public SavedTaxInvoice PersistedInvoice { get; }

        public TaxInvoiceLinkSynchronizationException(
            SavedTaxInvoice persistedInvoice, Exception innerException)
            : base(
                $"Tax Invoice {persistedInvoice.InvoiceNumber} was saved, but its Delivery Note links could not be synchronized. The invoice remains saved; retry the link synchronization. {innerException.Message}",
                innerException)
        {
            PersistedInvoice = persistedInvoice
                ?? throw new ArgumentNullException(nameof(persistedInvoice));
        }
    }
}

using POS_MT.Models;

namespace POS_MT.Interfaces;

public interface ICustomerAdvanceService
{
    Task EnsureSchemaAsync(CancellationToken cancellationToken = default);

    Task<decimal> GetAdvanceBalanceAsync(int customerUid, CancellationToken cancellationToken = default);

    /// <summary>
    /// Posts a new customer payment: unused amount increases advance;
    /// optional invoice link applies up to the due amount and updates invoice paid/balance.
    /// Sets <see cref="CustomerPayment.AppliedToInvoiceAmount"/>.
    /// </summary>
    Task ApplyNewPaymentAsync(CustomerPayment payment, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reverses the ledger effect of an existing payment, then applies the updated payment values.
    /// </summary>
    Task ReplacePaymentAsync(CustomerPayment payment, int previousCustomerUid, int? previousInvoiceUid, decimal previousAmount, decimal previousAppliedToInvoice, CancellationToken cancellationToken = default);

    /// <summary>
    /// Consumes customer advance toward an unpaid invoice. Returns amount applied.
    /// Creates a CustomerPayment row when amount &gt; 0.
    /// </summary>
    Task<decimal> AutoApplyAdvanceToInvoiceAsync(
        SalesInvoice invoice,
        int? userUid = null,
        int? terminalUid = null,
        CancellationToken cancellationToken = default);
}

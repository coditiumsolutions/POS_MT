using Microsoft.EntityFrameworkCore;
using POS_MT.Data;
using POS_MT.Interfaces;
using POS_MT.Models;

namespace POS_MT.Services;

public sealed class CustomerAdvanceService : ICustomerAdvanceService
{
    private readonly POSDbContext _db;
    private bool _schemaChecked;

    public CustomerAdvanceService(POSDbContext db)
    {
        _db = db;
    }

    public async Task EnsureSchemaAsync(CancellationToken cancellationToken = default)
    {
        if (_schemaChecked)
            return;

        try
        {
            if (_db.Database.IsSqlite())
            {
                await EnsureSqliteColumnAsync("Customers", "AdvanceBalance",
                    "ALTER TABLE Customers ADD COLUMN AdvanceBalance REAL NOT NULL DEFAULT 0", cancellationToken);
                await EnsureSqliteColumnAsync("CustomerPayments", "AppliedToInvoiceAmount",
                    "ALTER TABLE CustomerPayments ADD COLUMN AppliedToInvoiceAmount REAL NOT NULL DEFAULT 0", cancellationToken);
            }
            else
            {
                await _db.Database.ExecuteSqlRawAsync("""
                    IF COL_LENGTH(N'dbo.Customers', N'AdvanceBalance') IS NULL
                        ALTER TABLE dbo.Customers ADD [AdvanceBalance] DECIMAL(18, 2) NOT NULL CONSTRAINT DF_Customers_AdvanceBalance DEFAULT (0);
                    """, cancellationToken);
                await _db.Database.ExecuteSqlRawAsync("""
                    IF COL_LENGTH(N'dbo.CustomerPayments', N'AppliedToInvoiceAmount') IS NULL
                        ALTER TABLE dbo.CustomerPayments ADD [AppliedToInvoiceAmount] DECIMAL(18, 2) NOT NULL CONSTRAINT DF_CustomerPayments_AppliedToInvoiceAmount DEFAULT (0);
                    """, cancellationToken);
            }
        }
        catch
        {
            // Column may already exist under a concurrent call; continue.
        }

        _schemaChecked = true;
    }

    public async Task<decimal> GetAdvanceBalanceAsync(int customerUid, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        return await _db.Customers.AsNoTracking()
            .Where(x => x.Uid == customerUid)
            .Select(x => x.AdvanceBalance)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task ApplyNewPaymentAsync(CustomerPayment payment, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        if (payment.Amount <= 0)
            throw new InvalidOperationException("Payment amount must be greater than zero.");

        var customer = await _db.Customers.FirstOrDefaultAsync(x => x.Uid == payment.CustomerUid, cancellationToken)
            ?? throw new InvalidOperationException("Customer was not found.");

        var applied = 0m;
        if (payment.SalesInvoiceUid is int invoiceUid and > 0)
        {
            var invoice = await _db.SalesInvoices.FirstOrDefaultAsync(x => x.Uid == invoiceUid, cancellationToken)
                ?? throw new InvalidOperationException("Sales invoice was not found.");

            if (invoice.CustomerUid is int invCustomer && invCustomer != payment.CustomerUid)
                throw new InvalidOperationException("Selected invoice does not belong to this customer.");

            applied = ApplyCashToInvoice(invoice, payment.Amount);
        }

        var advancePart = payment.Amount - applied;
        if (advancePart > 0)
            customer.AdvanceBalance += advancePart;

        payment.AppliedToInvoiceAmount = applied;
        customer.UpdatedDate = DateTime.Now;
    }

    public async Task ReplacePaymentAsync(
        CustomerPayment payment,
        int previousCustomerUid,
        int? previousInvoiceUid,
        decimal previousAmount,
        decimal previousAppliedToInvoice,
        CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        await ReversePaymentEffectAsync(previousCustomerUid, previousInvoiceUid, previousAmount, previousAppliedToInvoice, cancellationToken);
        await ApplyNewPaymentAsync(payment, cancellationToken);
    }

    public async Task<decimal> AutoApplyAdvanceToInvoiceAsync(
        SalesInvoice invoice,
        int? userUid = null,
        int? terminalUid = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        if (invoice.CustomerUid is not int customerUid || customerUid <= 0)
            return 0m;

        // Monthly Supply is settled at month end — do not auto-clear mid-month.
        if (CustomerClassification.IsMonthlySupplyInvoice(invoice.Remarks))
            return 0m;

        var due = Math.Max(0, invoice.NetAmount - invoice.PaidAmount);
        if (due <= 0)
            return 0m;

        var customer = await _db.Customers.FirstOrDefaultAsync(x => x.Uid == customerUid, cancellationToken);
        if (customer is null || customer.AdvanceBalance <= 0)
            return 0m;

        var apply = Math.Min(due, customer.AdvanceBalance);
        if (apply <= 0)
            return 0m;

        ApplyCashToInvoice(invoice, apply);
        customer.AdvanceBalance -= apply;
        customer.UpdatedDate = DateTime.Now;

        var paymentNo = $"ADV-{DateTime.Now:yyyyMMddHHmmssfff}";
        while (await _db.CustomerPayments.AnyAsync(x => x.PaymentNo == paymentNo, cancellationToken))
            paymentNo = $"ADV-{DateTime.Now:yyyyMMddHHmmssfff}-{Random.Shared.Next(10, 99)}";

        _db.CustomerPayments.Add(new CustomerPayment
        {
            PaymentNo = paymentNo,
            PaymentDate = DateTime.Now,
            CustomerUid = customerUid,
            SalesInvoiceUid = invoice.Uid,
            Amount = apply,
            AppliedToInvoiceAmount = apply,
            UserUid = userUid,
            TerminalUid = terminalUid,
            Remarks = $"Auto-applied advance to {invoice.InvoiceNo}",
            CreatedDate = DateTime.Now
        });

        return apply;
    }

    public async Task<decimal> AutoApplyAdvanceFifoAsync(
        int customerUid,
        int? userUid = null,
        int? terminalUid = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        if (customerUid <= 0)
            return 0m;

        var unpaid = await _db.SalesInvoices
            .Where(x => x.CustomerUid == customerUid
                        && x.InvoiceStatus != "Cancelled"
                        && x.BalanceAmount > 0)
            .OrderBy(x => x.InvoiceDate)
            .ThenBy(x => x.Uid)
            .ToListAsync(cancellationToken);

        var totalApplied = 0m;
        foreach (var invoice in unpaid)
        {
            if (CustomerClassification.IsMonthlySupplyInvoice(invoice.Remarks))
                continue;

            var applied = await AutoApplyAdvanceToInvoiceAsync(invoice, userUid, terminalUid, cancellationToken);
            if (applied <= 0)
                break;
            totalApplied += applied;
        }

        return totalApplied;
    }

    public async Task<decimal> ReverseAutoAppliedAdvancesOnMonthlySupplyAsync(
        int customerUid,
        CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        if (customerUid <= 0)
            return 0m;

        var monthlyInvoices = await _db.SalesInvoices
            .Where(x => x.CustomerUid == customerUid
                        && x.InvoiceStatus != "Cancelled"
                        && x.Remarks != null
                        && x.Remarks.StartsWith("Monthly Supply"))
            .ToListAsync(cancellationToken);

        if (monthlyInvoices.Count == 0)
            return 0m;

        var invoiceIds = monthlyInvoices.Select(x => x.Uid).ToList();
        var autoPayments = await _db.CustomerPayments
            .Where(x => x.SalesInvoiceUid != null
                        && invoiceIds.Contains(x.SalesInvoiceUid.Value)
                        && x.Remarks != null
                        && x.Remarks.StartsWith("Auto-applied advance"))
            .ToListAsync(cancellationToken);

        foreach (var invoice in monthlyInvoices)
        {
            var paidManual = await _db.CustomerPayments.AsNoTracking()
                .Where(x => x.SalesInvoiceUid == invoice.Uid
                            && (x.Remarks == null
                                || !x.Remarks.StartsWith("Auto-applied advance")))
                .SumAsync(x => (decimal?)x.AppliedToInvoiceAmount, cancellationToken) ?? 0m;

            invoice.PaidAmount = paidManual;
            invoice.BalanceAmount = Math.Max(0, invoice.NetAmount - paidManual);
            invoice.PaymentStatus = paidManual <= 0
                ? "Unpaid"
                : paidManual >= invoice.NetAmount ? "Paid" : "Partial";
            invoice.UpdatedDate = DateTime.Now;
        }

        var restored = 0m;
        if (autoPayments.Count > 0)
        {
            var customer = await _db.Customers.FirstOrDefaultAsync(x => x.Uid == customerUid, cancellationToken);
            foreach (var payment in autoPayments)
            {
                var applied = payment.AppliedToInvoiceAmount > 0
                    ? payment.AppliedToInvoiceAmount
                    : payment.Amount;
                if (applied > 0)
                {
                    restored += applied;
                    if (customer is not null)
                        customer.AdvanceBalance += applied;
                }
            }

            if (customer is not null)
                customer.UpdatedDate = DateTime.Now;

            _db.CustomerPayments.RemoveRange(autoPayments);
        }

        return restored;
    }

    private async Task ReversePaymentEffectAsync(
        int customerUid,
        int? invoiceUid,
        decimal amount,
        decimal appliedToInvoice,
        CancellationToken cancellationToken)
    {
        var customer = await _db.Customers.FirstOrDefaultAsync(x => x.Uid == customerUid, cancellationToken);
        if (customer is null)
            return;

        var advancePart = Math.Max(0, amount - Math.Max(0, appliedToInvoice));
        if (advancePart > 0)
            customer.AdvanceBalance = Math.Max(0, customer.AdvanceBalance - advancePart);

        if (invoiceUid is int invId and > 0 && appliedToInvoice > 0)
        {
            var invoice = await _db.SalesInvoices.FirstOrDefaultAsync(x => x.Uid == invId, cancellationToken);
            if (invoice is not null)
            {
                invoice.PaidAmount = Math.Max(0, invoice.PaidAmount - appliedToInvoice);
                RecalcInvoicePayment(invoice);
                invoice.UpdatedDate = DateTime.Now;
            }
        }

        customer.UpdatedDate = DateTime.Now;
    }

    private static decimal ApplyCashToInvoice(SalesInvoice invoice, decimal amount)
    {
        var due = Math.Max(0, invoice.NetAmount - invoice.PaidAmount);
        var applied = Math.Min(amount, due);
        if (applied <= 0)
            return 0m;

        invoice.PaidAmount += applied;
        RecalcInvoicePayment(invoice);
        invoice.UpdatedDate = DateTime.Now;
        return applied;
    }

    private static void RecalcInvoicePayment(SalesInvoice invoice)
    {
        invoice.BalanceAmount = Math.Max(0, invoice.NetAmount - invoice.PaidAmount);
        if (invoice.NetAmount <= 0)
        {
            invoice.PaymentStatus = "Unpaid";
            invoice.PaidAmount = 0;
            invoice.BalanceAmount = 0;
        }
        else if (invoice.PaidAmount <= 0)
        {
            invoice.PaymentStatus = "Unpaid";
        }
        else if (invoice.PaidAmount >= invoice.NetAmount)
        {
            invoice.PaymentStatus = "Paid";
            invoice.PaidAmount = invoice.NetAmount;
            invoice.BalanceAmount = 0;
        }
        else
        {
            invoice.PaymentStatus = "Partial";
        }
    }

    private async Task EnsureSqliteColumnAsync(string table, string column, string alterSql, CancellationToken ct)
    {
        var conn = _db.Database.GetDbConnection();
        await _db.Database.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info({table})";
        var found = false;
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                if (string.Equals(reader["name"]?.ToString(), column, StringComparison.OrdinalIgnoreCase))
                {
                    found = true;
                    break;
                }
            }
        }

        if (!found)
            await _db.Database.ExecuteSqlRawAsync(alterSql, ct);
    }
}

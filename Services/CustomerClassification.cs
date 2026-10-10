namespace POS_MT.Services;

/// <summary>
/// Walk-in is the Cash POS customer (C001 / Walk-in name).
/// Sales customer type uses invoice remarks: Monthly Supply → Monthly; Credit POS → Credit.
/// </summary>
public static class CustomerClassification
{
    private const string MonthlySupplyRemarkPrefix = "Monthly Supply";
    private const string CreditPosRemarkToken = "Monthly POS";

    public static bool IsWalkIn(string? customerCode, string? customerName)
    {
        if (string.Equals(customerCode, "C001", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(customerName))
        {
            return false;
        }

        return customerName.Contains("Walk-in", StringComparison.OrdinalIgnoreCase)
               || customerName.Contains("Walk In", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsMonthlySupplyInvoice(string? invoiceRemarks)
        => !string.IsNullOrWhiteSpace(invoiceRemarks)
           && invoiceRemarks.StartsWith(MonthlySupplyRemarkPrefix, StringComparison.OrdinalIgnoreCase);

    public static string GetSalesTypeLabel(string? customerCode, string? customerName, string? invoiceRemarks)
    {
        if (customerCode is null && customerName is null)
        {
            return "—";
        }

        if (IsWalkIn(customerCode, customerName))
        {
            return "Walk-in";
        }

        if (IsMonthlySupplyInvoice(invoiceRemarks))
        {
            return "Monthly";
        }

        return "Credit";
    }

    /// <summary>Customer master list (no invoice context) — non walk-in are credit customers.</summary>
    public static string GetTypeLabel(string? customerCode, string? customerName)
        => GetSalesTypeLabel(customerCode, customerName, invoiceRemarks: null);
}

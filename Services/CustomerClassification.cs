namespace POS_MT.Services;

/// <summary>
/// Walk-in is only the Cash POS customer (C001 / Walk-in name).
/// All other customers are Monthly.
/// </summary>
public static class CustomerClassification
{
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

    public static string GetTypeLabel(string? customerCode, string? customerName)
    {
        if (customerCode is null && customerName is null)
        {
            return "—";
        }

        return IsWalkIn(customerCode, customerName) ? "Walk-in" : "Monthly";
    }
}

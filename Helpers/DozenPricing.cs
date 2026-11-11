namespace POS_MT.Helpers;

/// <summary>
/// Products sold by the dozen (e.g. Eggs): sale price is per dozen, quantity is tracked in pieces.
/// </summary>
public static class DozenPricing
{
    public const decimal PiecesPerDozen = 12m;

    public static bool IsDozenUnit(string? unitName, string? unitCode) =>
        (!string.IsNullOrWhiteSpace(unitName) && unitName.Contains("dozen", StringComparison.OrdinalIgnoreCase))
        || (!string.IsNullOrWhiteSpace(unitCode) && unitCode.Contains("doz", StringComparison.OrdinalIgnoreCase));

    /// <summary>Billable dozen-count from piece quantity (12 pieces = 1 dozen).</summary>
    public static decimal BillQuantity(decimal pieceQuantity, bool pricedByDozen) =>
        pricedByDozen ? pieceQuantity / PiecesPerDozen : pieceQuantity;

    public static decimal LineAmount(decimal pieceQuantity, decimal unitPrice, bool pricedByDozen) =>
        Math.Round(unitPrice * BillQuantity(pieceQuantity, pricedByDozen), 2, MidpointRounding.AwayFromZero);

    public static decimal ItemTotalWithDelivery(
        decimal pieceQuantity,
        decimal unitPrice,
        decimal deliveryCharges,
        bool pricedByDozen) =>
        Math.Round(
            LineAmount(pieceQuantity, unitPrice, pricedByDozen) + deliveryCharges,
            0,
            MidpointRounding.AwayFromZero);
}

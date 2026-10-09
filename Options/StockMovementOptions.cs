namespace POS_MT.Options;

/// <summary>
/// Configurable thresholds for Fast / Slow / Dead stock classification.
/// Bound from configuration section "StockMovementAnalysis".
/// </summary>
public sealed class StockMovementOptions
{
    public const string SectionName = "StockMovementAnalysis";

    /// <summary>Sold within this many days (and not declining) → Fast-Moving.</summary>
    public int FastMovingMaxDaysSinceSale { get; set; } = 30;

    /// <summary>Last sale older than this (but newer than dead) → Slow-Moving.</summary>
    public int SlowMovingMinDaysSinceSale { get; set; } = 30;

    /// <summary>No sale for this many days (or never sold with stock) → Dead Stock.</summary>
    public int DeadStockMinDaysSinceSale { get; set; } = 90;

    /// <summary>Days in each half of the sales-trend comparison window.</summary>
    public int TrendWindowDays { get; set; } = 30;

    /// <summary>
    /// Recent-period qty below this ratio of the prior period counts as declining
    /// (e.g. 0.7 = recent sales under 70% of prior window).
    /// </summary>
    public decimal DecliningTrendRatio { get; set; } = 0.7m;

    /// <summary>Only include products with stock greater than this.</summary>
    public decimal MinimumStockToInclude { get; set; } = 0m;
}

namespace POS_MT.Interfaces;

/// <summary>
/// Recipe: 40 kg milk → 32 kg Fresh Yogurt. Deducts milk (may go negative) and posts a Shop sales invoice.
/// </summary>
public interface IYogurtRecipeService
{
    decimal RecipeMilkKg { get; }
    decimal RecipeYogurtKg { get; }

    decimal MilkForYogurt(decimal yogurtKg);

    /// <summary>
    /// After Fresh Yogurt is received (e.g. purchase), deduct milk at the same warehouse and create a Shop sales invoice.
    /// Yogurt stock itself is not changed here — caller already added it.
    /// </summary>
    Task<YogurtRecipeApplyResult> ConsumeMilkForReceivedYogurtAsync(
        decimal yogurtQty,
        int warehouseUid,
        int? branchUid,
        string? branchName,
        int? userUid,
        int? terminalUid,
        string? terminalName,
        string sourceReferenceNo,
        CancellationToken ct = default);
}

public sealed class YogurtRecipeApplyResult
{
    public bool Applied { get; init; }
    public decimal YogurtQuantity { get; init; }
    public decimal MilkQuantity { get; init; }
    public string? MilkProductName { get; init; }
    public decimal MilkBalanceAfter { get; init; }
    public int? SalesInvoiceUid { get; init; }
    public string? SalesInvoiceNo { get; init; }
    public string? Message { get; init; }
}

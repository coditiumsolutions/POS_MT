namespace POS_MT.Interfaces;

public sealed class InventoryLineMovement
{
    public int ProductUid { get; init; }
    public string ProductName { get; init; } = string.Empty;
    public decimal Quantity { get; init; }
    public decimal UnitCost { get; init; }
}

public sealed class InventoryApplyResult
{
    public List<string> ShortageNotes { get; } = [];

    public bool HasShortages => ShortageNotes.Count > 0;

    public string? RemarksSuffix =>
        ShortageNotes.Count == 0
            ? null
            : "Stock short: " + string.Join("; ", ShortageNotes);
}

public interface IInventoryLedgerService
{
    Task<int> ResolveWarehouseUidAsync(int? preferredWarehouseUid, int? branchUid, CancellationToken cancellationToken = default);

    Task<InventoryApplyResult> ApplySaleAsync(
        int salesInvoiceUid,
        string invoiceNo,
        IReadOnlyList<InventoryLineMovement> lines,
        int? warehouseUid,
        int? branchUid,
        int? userUid,
        int? terminalUid,
        CancellationToken cancellationToken = default);

    /// <summary>Adjust stock for a quantity change on an existing sales line (positive = more sold).</summary>
    Task<InventoryApplyResult> ApplySaleQuantityDeltaAsync(
        int salesInvoiceUid,
        string invoiceNo,
        InventoryLineMovement lineDelta,
        int? warehouseUid,
        int? branchUid,
        int? userUid,
        int? terminalUid,
        CancellationToken cancellationToken = default);

    Task ReverseSaleAsync(
        int salesInvoiceUid,
        string invoiceNo,
        int? userUid,
        int? terminalUid,
        CancellationToken cancellationToken = default);

    Task ApplyPurchaseAsync(
        int purchaseInvoiceUid,
        string purchaseInvoiceNo,
        int warehouseUid,
        IReadOnlyList<InventoryLineMovement> lines,
        int? userUid,
        int? terminalUid,
        CancellationToken cancellationToken = default,
        bool allowAdditional = false);

    Task ReversePurchaseAsync(
        int purchaseInvoiceUid,
        string purchaseInvoiceNo,
        int? userUid,
        int? terminalUid,
        CancellationToken cancellationToken = default);

    string MergeShortageRemarks(string? existingRemarks, InventoryApplyResult result);
}

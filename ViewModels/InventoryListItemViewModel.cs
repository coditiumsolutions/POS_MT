namespace POS_MT.ViewModels;
public sealed class InventoryListItemViewModel {
  public int Uid { get; set; }
  public string ProductName { get; set; } = string.Empty;
  public string WarehouseName { get; set; } = string.Empty;
  public decimal CurrentQuantity { get; set; }
  public decimal AverageCost { get; set; }
  public decimal LastPurchasePrice { get; set; }
  public decimal LastSalePrice { get; set; }
}

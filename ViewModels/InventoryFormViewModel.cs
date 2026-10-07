using System.ComponentModel.DataAnnotations;
namespace POS_MT.ViewModels;
public sealed class InventoryFormViewModel {
  public int Uid { get; set; }
  [Required, Display(Name="Product")] public int ProductUid { get; set; }
  [Required, Display(Name="Warehouse")] public int WarehouseUid { get; set; }
  [Display(Name="Quantity In")] public decimal QuantityIn { get; set; }
  [Display(Name="Quantity Out")] public decimal QuantityOut { get; set; }
  [Display(Name="Current Quantity")] public decimal CurrentQuantity { get; set; }
  [Display(Name="Average Cost")] public decimal AverageCost { get; set; }
  [Display(Name="Last Purchase Price")] public decimal LastPurchasePrice { get; set; }
  [Display(Name="Last Sale Price")] public decimal LastSalePrice { get; set; }
  [Display(Name="Min Stock")] public decimal MinimumStock { get; set; }
  [Display(Name="Max Stock")] public decimal MaximumStock { get; set; }
}

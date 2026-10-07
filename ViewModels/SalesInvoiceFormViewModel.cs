using System.ComponentModel.DataAnnotations;
namespace POS_MT.ViewModels;
public sealed class SalesInvoiceFormViewModel {
  public int Uid { get; set; }
  [Required, StringLength(50), Display(Name="Invoice No")] public string InvoiceNo { get; set; } = string.Empty;
  [Display(Name="Invoice Date")] public DateTime InvoiceDate { get; set; } = DateTime.Now;
  [Display(Name="Branch")] public int? BranchUid { get; set; }
  [Display(Name="Customer")] public int? CustomerUid { get; set; }
  [Display(Name="Payment Method")] public int? PaymentMethodUid { get; set; }
  [Display(Name="Sub Total")] public decimal SubTotal { get; set; }
  [Display(Name="Discount")] public decimal DiscountAmount { get; set; }
  [Display(Name="Tax")] public decimal TaxAmount { get; set; }
  [Display(Name="Other Charges")] public decimal OtherCharges { get; set; }
  [Display(Name="Net Amount")] public decimal NetAmount { get; set; }
  [Display(Name="Paid Amount")] public decimal PaidAmount { get; set; }
  [Display(Name="Balance")] public decimal BalanceAmount { get; set; }
  [StringLength(30), Display(Name="Payment Status")] public string PaymentStatus { get; set; } = "Unpaid";
  [StringLength(30), Display(Name="Invoice Status")] public string InvoiceStatus { get; set; } = "Completed";
  [StringLength(500)] public string? Remarks { get; set; }
}

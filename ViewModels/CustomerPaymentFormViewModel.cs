using System.ComponentModel.DataAnnotations;
namespace POS_MT.ViewModels;
public sealed class CustomerPaymentFormViewModel {
  public int Uid { get; set; }
  [Required, StringLength(50), Display(Name="Payment No")] public string PaymentNo { get; set; } = string.Empty;
  [Display(Name="Payment Date")] public DateTime PaymentDate { get; set; } = DateTime.Now;
  [Required, Display(Name="Customer")] public int CustomerUid { get; set; }
  [Display(Name="Sales Invoice")] public int? SalesInvoiceUid { get; set; }
  [Display(Name="Payment Method")] public int? PaymentMethodUid { get; set; }
  public decimal Amount { get; set; }
  [StringLength(100), Display(Name="Reference No")] public string? ReferenceNo { get; set; }
  [StringLength(500)] public string? Remarks { get; set; }
}

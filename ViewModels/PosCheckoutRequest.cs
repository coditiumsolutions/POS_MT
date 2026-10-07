using System.ComponentModel.DataAnnotations;

namespace POS_MT.ViewModels;

public sealed class PosCheckoutItemRequest
{
    [Required]
    public int ProductUid { get; set; }

    [Range(0.001, 999999)]
    public decimal Quantity { get; set; }

    [Range(0, 999999999)]
    public decimal UnitPrice { get; set; }
}

public sealed class PosCheckoutRequest
{
    [Required]
    public string Mode { get; set; } = "Cash";

    [MinLength(1)]
    public List<PosCheckoutItemRequest> Items { get; set; } = [];
}

public sealed class PosCheckoutResultViewModel
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int InvoiceUid { get; set; }
    public string InvoiceNo { get; set; } = string.Empty;
    public decimal NetAmount { get; set; }
    public DateTime InvoiceDate { get; set; }
}

using System.ComponentModel.DataAnnotations;

namespace POS_MT.ViewModels;

public sealed class SalesReturnListItemViewModel
{
    public int Uid { get; set; }
    public string ReturnNo { get; set; } = string.Empty;
    public DateTime ReturnDate { get; set; }
    public decimal NetAmount { get; set; }
    public string? Remarks { get; set; }
}

public sealed class SalesReturnCreateViewModel
{
    public bool UseCardLayout { get; set; }
    public List<PosProductItemViewModel> Products { get; set; } = [];
    public List<PosCategoryItemViewModel> Categories { get; set; } = [];
}

public sealed class SalesReturnCheckoutItemRequest
{
    [Required]
    public int ProductUid { get; set; }

    [Range(0.001, 999999)]
    public decimal Quantity { get; set; }

    [Range(0, 999999999)]
    public decimal UnitPrice { get; set; }
}

public sealed class SalesReturnCheckoutRequest
{
    [StringLength(50)]
    public string? OriginalInvoiceNo { get; set; }

    [StringLength(400)]
    public string? Reason { get; set; }

    [MinLength(1)]
    public List<SalesReturnCheckoutItemRequest> Items { get; set; } = [];
}

public sealed class SalesReturnCheckoutResultViewModel
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int InvoiceUid { get; set; }
    public string ReturnNo { get; set; } = string.Empty;
    public decimal NetAmount { get; set; }
}

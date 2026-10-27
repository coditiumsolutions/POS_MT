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

    /// <summary>Optional client-generated GUID for idempotent offline/local-hub checkout.</summary>
    public Guid? ClientTransactionId { get; set; }

    [Range(0, 999999999)]
    public decimal DiscountAmount { get; set; }

    /// <summary>Cash note/amount the customer handed over. Optional; when set must be ≥ net.</summary>
    [Range(0, 999999999)]
    public decimal? CustomerGivenAmount { get; set; }

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

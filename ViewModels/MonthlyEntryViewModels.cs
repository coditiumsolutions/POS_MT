using System.ComponentModel.DataAnnotations;

namespace POS_MT.ViewModels;

public sealed class MonthlyCustomerListItemViewModel
{
    public int Uid { get; set; }
    public string CustomerCode { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
}

public sealed class MonthlyProductOptionViewModel
{
    public int Uid { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public decimal SalePrice { get; set; }
}

public sealed class MonthlyEntryPageViewModel
{
    public List<MonthlyCustomerListItemViewModel> Customers { get; set; } = [];
    public List<MonthlyProductOptionViewModel> Products { get; set; } = [];
}

public sealed class MonthlyEntryRowViewModel
{
    public int InvoiceUid { get; set; }
    public int DetailUid { get; set; }
    public DateTime EntryDate { get; set; }
    public int ProductUid { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
}

public sealed class MonthlyEntrySaveRequest
{
    public int? DetailUid { get; set; }

    [Required]
    public int CustomerUid { get; set; }

    [Required]
    public DateTime EntryDate { get; set; }

    [Required]
    public int ProductUid { get; set; }

    [Range(0.001, 999999)]
    public decimal Quantity { get; set; }

    [Range(0, 999999999)]
    public decimal UnitPrice { get; set; }
}

public sealed class MonthlyEntryApiResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public MonthlyEntryRowViewModel? Entry { get; set; }
    public int? SalesInvoiceUid { get; set; }
    public string? InvoiceNo { get; set; }
}

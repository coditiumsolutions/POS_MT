namespace POS_MT.ViewModels;

public class ExpenseReportViewModel
{
    public DateTime DateFrom { get; set; }
    public DateTime DateTo { get; set; }

    public int ExpenseCount { get; set; }
    public decimal ExpenseTotal { get; set; }

    public int EmployeePaymentCount { get; set; }
    public decimal EmployeePaymentTotal { get; set; }

    public int VendorPaymentCount { get; set; }
    public decimal VendorPaymentTotal { get; set; }

    public int CustomerInvoiceCount { get; set; }
    public decimal CustomerInvoiceTotal { get; set; }

    public decimal TotalOutflow => ExpenseTotal + EmployeePaymentTotal + VendorPaymentTotal;
    public decimal NetPosition => CustomerInvoiceTotal - TotalOutflow;
}

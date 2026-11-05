namespace POS_MT.ViewModels;

public sealed class TestBarcodeItemViewModel
{
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;

    /// <summary>Value encoded in the barcode — product code when barcode field is empty.</summary>
    public string BarcodeValue { get; set; } = string.Empty;
}

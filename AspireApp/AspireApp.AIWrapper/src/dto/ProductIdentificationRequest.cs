/// <summary>
/// DTO from API layer to LLM Wrapper to extract product information
/// from given html content which was requested for Identification
/// </summary>
public class ProductIdentificationRequest : ProductClassificationBase
{
    public string? ProductName { get; set; }
    public string? ProductDescription { get; set; }
    public string? ProductCategory { get; set; }
    public string? EAN { get; set; }
}

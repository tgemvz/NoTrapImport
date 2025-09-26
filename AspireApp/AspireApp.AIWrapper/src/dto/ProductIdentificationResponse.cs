
/// <summary>
/// DTO from LLM Wrapper to relay information about product identification
/// </summary>
public class ProductIdentificationResponse : ProductClassificationBase
{
    public string? ProductName { get; set; }
    public string? ProductDescription { get; set; }
    public string? ProductCategory { get; set; }
    public string? EAN { get; set; }
}
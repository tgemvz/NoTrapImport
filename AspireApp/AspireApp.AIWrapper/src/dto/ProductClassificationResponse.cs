
/// <summary>
/// DTO from LLM Wrapper to relay information about product identification
/// and legal status to display for user
/// </summary>
public class ProductClassificationResponse : ProductClassificationBase
{
    public string? ProductName { get; set; }
    public string? ProductDescription { get; set; }
    public string? ProductCategory { get; set; }
    /// <summary>
    /// Scale from 1 to 100
    /// 1 would be "definitly not legal brotha!"
    /// 100 would "no worries mate :)"
    /// </summary>
    public double? ProductLegality { get; set; }
}


/// <summary>
/// DTO from LLM Wrapper to relay information about product legality classification
/// </summary>
public class ProductClassificationResponse : ProductClassificationBase
{
    /// <summary>
    /// Scale from 1 to 100
    /// 1 would be "definitly not legal brotha!"
    /// 100 would "no worries mate :)"
    /// </summary>
    public double? ProductLegality { get; set; }
    public bool IsLegal { get; set; }
    public string? LegalExplanation { get; set; }
    public string[]? LinkToLegalDocuments { get; set; }
}


/// <summary>
/// DTO from API layer to LLM Wrapper to extract product information
/// from given html content which was requested for classification
/// </summary>
public class ProductClassificationRequest : ProductClassificationBase
{
    /// <summary>
    /// Product site content, for classification
    /// </summary>
    public required string HtmlContent { get; init; }
}

public class ProductClassificationBase
{
    /// <summary>
    /// ID, for tracking purposes
    /// </summary>
    public Guid Id { get; set; }
    /// <summary>
    /// Timestamp, for caching purposes
    /// </summary>
    public DateTime RequestDate { get; set; }
    /// <summary>
    /// Request URL, for caching purposes
    /// </summary>
    public required string ProductUrl { get; set; }

}
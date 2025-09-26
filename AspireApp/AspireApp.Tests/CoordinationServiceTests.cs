using AspireApp.ApiService.Services;
using Moq;

namespace AspireApp.Tests;

[TestClass]
public class CoordinationServiceTests
{
    [TestMethod]
    public async Task TestMyFluff()
    {
        var source = new CancellationTokenSource();
        var cancellationToken = source.Token;
        var fileContent = await File.ReadAllTextAsync("HtmlFiles/kugelschreiber.txt", cancellationToken);
        var contentFetcherMock = new Mock<IWebContentFetcher>();
        contentFetcherMock.Setup(x => x.GetHtmlContentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(fileContent);
        var coordinationService = new CoordinationService(contentFetcherMock.Object);

        var classification = coordinationService.ClassifyProductByHtml(fileContent, cancellationToken);
        
    }
}

/// <summary>
/// DTO from API layer to LLM Wrapper to extract product information
/// from given html content which was requested for classification
/// </summary>
public class ProductClassificationRequest : ProductClassificationBase
{
    /// <summary>
    /// Request URL, for caching purposes
    /// </summary>
    public required string Url {get; set;}
    /// <summary>
    /// Product site content, for classification
    /// </summary>
    public required string HtmlContent { get; init; }
}

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
}
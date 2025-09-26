using AspireApp.ApiService.Services;
using FluentAssertions;
using Moq;

namespace AspireApp.Tests;

[TestClass]
public class CoordinationServiceTests
{
    [TestMethod]
    public async Task TestMyFluff()
    {
        Environment.SetEnvironmentVariable("SWISS_AI_PLATFORM_API_KEY", "b2ckhqYMYMBKxi9KPi7NRw5XlBAO");
        var source = new CancellationTokenSource();
        var cancellationToken = source.Token;
        var fileContent = await File.ReadAllTextAsync("HtmlFiles/kugelschreiber.txt", cancellationToken);
        var contentFetcherMock = new Mock<IWebContentFetcher>();
        var wrapper = new AspireAppAIWrapper();
        contentFetcherMock.Setup(x => x.GetHtmlContentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(fileContent);
        var coordinationService = new CoordinationService(contentFetcherMock.Object, wrapper);

        var classification = await coordinationService.ClassifyProductByHtmlAsync(fileContent, cancellationToken);
        classification.Should().NotBeNull();
        classification.ProductName.Should().Be("testName");
        classification.ProductDescription.Should().Be("testDescription");
        classification.ProductCategory.Should().Be("testCategory");
        classification.ProductLegality.Should().Be(100);

    }
}


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


using AspireApp.ApiService.Services;
using FluentAssertions;
using Moq;

namespace AspireApp.Tests;

[TestClass]
public class CoordinationServiceTests
{
    private static string TestKnife =
        "https://jars-messer.de/products/bombfrog-jars-messer-42a-cpm3v-n690-stahl-taucher-survival-jagd-outdoor-messer-green-black-micarta?variant=41232888922206";

    private static string TestPistol =
        "https://www.action-shop24.de/MAUSER-1911-OD-Green-.22lr-HV-Selbstladepistole/411.02.13";
    
    [TestMethod]
    public async Task TestMyFluff()
    {
        Environment.SetEnvironmentVariable("SWISS_AI_PLATFORM_API_KEY", "b2ckhqYMYMBKxi9KPi7NRw5XlBAO");
        var source = new CancellationTokenSource();
        var cancellationToken = source.Token;
        var fileContent = await File.ReadAllTextAsync("HtmlFiles/kugelschreiber.txt", cancellationToken);
        var contentFetcherMock = new Mock<WebContentFetcher>();
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
    
    [TestMethod]
    public async Task TestMyFluff2_Knife()
    {
        Environment.SetEnvironmentVariable("SWISS_AI_PLATFORM_API_KEY", "b2ckhqYMYMBKxi9KPi7NRw5XlBAO");
        var source = new CancellationTokenSource();
        var cancellationToken = source.Token;
        var contentFetcher = new WebContentFetcher();
        var wrapper = new AspireAppAIWrapper();
        var coordinationService = new CoordinationService(contentFetcher, wrapper);

        var classification = await coordinationService.ClassifyProductByUrl(TestKnife, cancellationToken);
        classification.Should().NotBeNull();
        classification.ProductName.Should().Be("testName");
        classification.ProductDescription.Should().Be("testDescription");
        classification.ProductCategory.Should().Be("testCategory");
        classification.ProductLegality.Should().Be(100);

    }
    
    [TestMethod]
    public async Task TestMyFluff2_Pistol()
    {
        Environment.SetEnvironmentVariable("SWISS_AI_PLATFORM_API_KEY", "b2ckhqYMYMBKxi9KPi7NRw5XlBAO");
        var source = new CancellationTokenSource();
        var cancellationToken = source.Token;
        var contentFetcher = new WebContentFetcher();
        var wrapper = new AspireAppAIWrapper();
        var coordinationService = new CoordinationService(contentFetcher, wrapper);

        var classification = await coordinationService.ClassifyProductByUrl(TestPistol, cancellationToken);
        classification.Should().NotBeNull();
        classification.ProductName.Should().Be("testName");
        classification.ProductDescription.Should().Be("testDescription");
        classification.ProductCategory.Should().Be("testCategory");
        classification.ProductLegality.Should().Be(100);

    }
}


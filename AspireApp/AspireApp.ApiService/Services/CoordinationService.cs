namespace AspireApp.ApiService.Services;

public interface ICoordinationService
{
    Task<ProductClassificationResponse> ClassifyProductByUrl(string url, CancellationToken cancellationToken);
    Task<ProductClassificationResponse> ClassifyProductByHtmlAsync(string htmlContent, CancellationToken cancellationToken);
}

public class CoordinationService(IWebContentFetcher webContentFetcher, AspireAppAIWrapper aiWrapper) : ICoordinationService
{
    public async Task<ProductClassificationResponse> ClassifyProductByUrl(string url, CancellationToken cancellationToken)
    {
        var htmlContent = await webContentFetcher.GetHtmlContentAsync(url, cancellationToken);
        if (string.IsNullOrWhiteSpace(htmlContent))
        {
            throw new ArgumentException($"Could not load html content from given url: {url}");
        }

        var request = MapRequest(htmlContent);
        
        // TODO: call classification API
        var classification = await aiWrapper.GetProductIdentificationAsync(request, cancellationToken);
        
        // TODO: call rating API
        var rating = await aiWrapper.GetProductClassificationAsync("who do you think you are", cancellationToken);
        
        // TODO: return actual rating to API / consumer
        return rating;
    }

    private static ProductClassificationRequest MapRequest(string htmlContent, string url = "www.testing.com")
    {
        var request = new ProductClassificationRequest()
        {
            Id = Guid.NewGuid(),
            RequestDate = DateTime.Now,
            Url = url,
            HtmlContent = htmlContent
        };

        return request;
    }

    public async Task<ProductClassificationResponse> ClassifyProductByHtmlAsync(string htmlContent, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(htmlContent))
        {
            throw new ArgumentException($"Content is empty.");
        }

        var request = MapRequest(htmlContent);
        
        // TODO: call classification API
        var classification = await aiWrapper.GetProductIdentificationAsync(request, cancellationToken);
        
        // TODO: call rating API
        var rating = await aiWrapper.GetProductClassificationAsync("who do you think you are", cancellationToken);
        
        // TODO: return actual rating to API / consumer
        return rating;
    }
}
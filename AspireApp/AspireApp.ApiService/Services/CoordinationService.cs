namespace AspireApp.ApiService.Services;

public interface ICoordinationService
{
    Task<string> ClassifyProductByUrl(string url, CancellationToken cancellationToken);
    string ClassifyProductByHtml(string htmlContent, CancellationToken cancellationToken);
}

public class CoordinationService(IWebContentFetcher webContentFetcher) : ICoordinationService
{
    public async Task<string> ClassifyProductByUrl(string url, CancellationToken cancellationToken)
    {
        var htmlContent = await webContentFetcher.GetHtmlContentAsync(url, cancellationToken);
        if (string.IsNullOrWhiteSpace(htmlContent))
        {
            throw new ArgumentException($"Could not load html content from given url: {url}");
        }
        
        // TODO: call classification API
        
        // TODO: call rating API
        
        // TODO: return actual rating to API / consumer
        return string.Empty;
    }

    public string ClassifyProductByHtml(string htmlContent, CancellationToken cancellationToken)
    {
        // TODO: call classification API
        
        // TODO: call rating API
        
        // TODO: return actual rating to API / consumer
        return string.Empty;
    }
}
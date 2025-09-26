using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace AspireApp.ApiService.Services;

public class CoordinationService(WebContentFetcher webContentFetcher, AspireAppAIWrapper aiWrapper)
{
    public async Task<ProductClassificationResponse> ClassifyProductByUrl(string url, CancellationToken cancellationToken)
    {
        var htmlContent = await webContentFetcher.GetHtmlContentAsync(url, cancellationToken);
        var cleanedHtml = ExtractBodyContent(htmlContent);
        if (string.IsNullOrWhiteSpace(cleanedHtml))
        {
            throw new ArgumentException($"Could not load html content from given url: {url}");
        }

        var request = MapRequest(url, htmlContent);
        
        // TODO: call classification API
        var classification = await aiWrapper.GetProductIdentificationAsync(request, cancellationToken);
        
        // TODO: call rating API
        var rating = await aiWrapper.GetProductClassificationAsync("who do you think you are", cancellationToken);
        
        // TODO: return actual rating to API / consumer
        return rating;
    }
    
    

    private static ProductClassificationRequest MapRequest(string url, string htmlContent)
    {
        return new ProductClassificationRequest()
        {
            Id = Guid.NewGuid(),
            RequestDate = DateTime.Now,
            Url = url,
            HtmlContent = htmlContent
        };
    }

    public async Task<ProductClassificationResponse> ClassifyProductByHtmlAsync(string htmlContent, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(htmlContent))
        {
            throw new ArgumentException($"Content is empty.");
        }

        var request = MapRequest("no url", htmlContent);
        
        // TODO: call classification API
        var classification = await aiWrapper.GetProductIdentificationAsync(request, cancellationToken);
        
        // TODO: call rating API
        var rating = await aiWrapper.GetProductClassificationAsync("who do you think you are", cancellationToken);
        
        // TODO: return actual rating to API / consumer
        return rating;
    }
    
    private string ExtractBodyContent(string fullHtmlContent)
    {
        if (string.IsNullOrWhiteSpace(fullHtmlContent))
        {
            return string.Empty;
        }

        var htmlDocument = new HtmlDocument();
        htmlDocument.OptionFixNestedTags = true;
        htmlDocument.LoadHtml(fullHtmlContent);
        var bodyNode = htmlDocument.DocumentNode.SelectSingleNode("//body");

        if (bodyNode != null)
        {
            string textContent = bodyNode.InnerText;
            string cleaned = textContent.Replace("\r", " ").Replace("\n", " ");
            cleaned = cleaned.Replace("\t", " ");

            // Use Regex to replace two or more spaces with a single space (to "flatten" the whitespace)
            cleaned = Regex.Replace(cleaned, @"\s{2,}", " ");
            return cleaned.Trim();
        }

        return string.Empty;
    }
}
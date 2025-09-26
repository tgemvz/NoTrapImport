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

        var request = MapRequest(url, cleanedHtml);

        // TODO: call classification API
        var ident = await aiWrapper.GetProductIdentificationAsync(request, cancellationToken);
        ProductIdentificationRequest pcr = new ProductIdentificationRequest
        {
            Id = request.Id,
            RequestDate = request.RequestDate,
            ProductUrl = request.ProductUrl,
            ProductDescription = ident.ProductDescription,
            ProductName = ident.ProductName,
            ProductCategory = ident.ProductCategory,
            EAN = ident.EAN
        };

        // TODO: call rating API

        var rating = await aiWrapper.GetProductClassificationAsync(pcr, cancellationToken);

        // TODO: return actual rating to API / consumer
        return rating;
    }



    private static ProductClassificationRequest MapRequest(string url, string htmlContent)
    {
        return new ProductClassificationRequest()
        {
            Id = Guid.NewGuid(),
            RequestDate = DateTime.Now,
            ProductUrl = url,
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

        var ident = await aiWrapper.GetProductIdentificationAsync(request, cancellationToken);
        ProductIdentificationRequest pcr = new ProductIdentificationRequest
        {
            Id = request.Id,
            RequestDate = request.RequestDate,
            ProductUrl = request.ProductUrl,
            ProductDescription = ident.ProductDescription
        };
        var rating = await aiWrapper.GetProductClassificationAsync(pcr, cancellationToken);

        // TODO: return actual rating to API / consumer
        return rating;
    }

    private string ExtractBodyContent(string fullHtmlContent)
    {
        if (string.IsNullOrWhiteSpace(fullHtmlContent))
        {
            return string.Empty;
        }

        // Use HtmlAgilityPack to load the HTML string
        var htmlDocument = new HtmlDocument();

        // This setting helps the parser handle malformed/non-standard HTML more gracefully
        htmlDocument.OptionFixNestedTags = true;

        // Load the content into the document object
        // Use LoadHtml for string input
        htmlDocument.LoadHtml(fullHtmlContent);

        // Find the <body> node using an XPath expression
        var bodyNode = htmlDocument.DocumentNode.SelectSingleNode("//body");

        if (bodyNode != null)
        {
            // 1. Get the *entire text content* of the body node, recursively
            // This automatically strips out all HTML tags (<...>) and their contents
            string textContent = bodyNode.InnerText;

            // 2. Remove all newline and carriage return characters and replace with a space
            string cleaned = textContent.Replace("\r", " ").Replace("\n", " ");

            // 3. Remove all tab characters
            cleaned = cleaned.Replace("\t", " ");

            // 4. Use Regex to replace two or more spaces with a single space (to "flatten" the whitespace)
            cleaned = Regex.Replace(cleaned, @"\s{2,}", " ");

            // 5. Trim leading/trailing whitespace
            return cleaned.Trim();
        }

        // Return empty string if the body tag couldn't be found
        return string.Empty;
    }
}
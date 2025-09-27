using AspireApp.ApiService.Services;
using Microsoft.AspNetCore.Mvc;

namespace AspireApp.ApiService;

[ApiController]
[Route("api/[controller]")]
public class ProductController : ControllerBase
{
    private readonly ILogger<ProductController> _logger;

    public ProductController(ILogger<ProductController> logger)
    {
        _logger = logger;
    }

    [HttpGet("classification")]
    public async Task<ActionResult<string>> GetClassification([FromQuery] string url, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return BadRequest("The URL path parameter is missing.");
        }

        var service = new CoordinationService(new WebContentFetcher(), new AspireAppAIWrapper(_logger));
        var classification = await service.ClassifyProductByUrl(url, cancellationToken);

        return Ok(classification);
    }

    [HttpPost("classification/html")]
    public async Task<ActionResult<string>> GetClassificationFromHtml([FromBody] GetClassificationFromHtmlParam param, CancellationToken cancellationToken)
    {
        if (param == null || string.IsNullOrWhiteSpace(param.html))
        {
            return BadRequest("The HTML path parameter is missing.");
        }

        var service = new CoordinationService(new WebContentFetcher(), new AspireAppAIWrapper(_logger));
        _logger.LogDebug(param.html);
        var classification = await service.ClassifyProductByHtmlAsync(param.html, param.url, cancellationToken);

        return Ok(classification);
    }

    public class GetClassificationFromHtmlParam
    {
        public string html { get; set; }
        public string url { get; set; }
    }
}
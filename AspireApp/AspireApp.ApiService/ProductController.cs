using AspireApp.ApiService.Services;
using Microsoft.AspNetCore.Mvc;

namespace AspireApp.ApiService;

[ApiController]
[Route("api/[controller]")]
public class ProductController(ICoordinationService coordinationService) : ControllerBase
{
    [HttpGet("classification/{*url}")] 
    public async Task<ActionResult<string>> GetClassification(string url, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return BadRequest("The URL path parameter is missing.");
        }

        var classification = await coordinationService.ClassifyProductByUrl(url, cancellationToken);

        return Ok(classification);
    }
}
using AspireApp.RagService.Models;
using Microsoft.AspNetCore.Mvc;

namespace AspireApp.RagService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RagController : ControllerBase
{
    private readonly Services.RagService _ragService;

    public RagController(Services.RagService ragService)
    {
        _ragService = ragService;
    }
    [HttpGet("hello")]
    public string Get()
    {
        return "hello";
    }

    [HttpPost("add")]
    public async Task<IActionResult> AddDocument([FromBody] string text, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return BadRequest("body must have a text");
        }
        var guid = Guid.NewGuid();
        await _ragService.AddDocument(text, guid, cancellationToken);
        return Ok(new { status = "added", guid });
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string query, [FromQuery] ulong limit = 3, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return BadRequest("Query cannot be empty.");
        }

        var results = await _ragService.Search(query, limit, cancellationToken);
        return Ok(results);
    }
}

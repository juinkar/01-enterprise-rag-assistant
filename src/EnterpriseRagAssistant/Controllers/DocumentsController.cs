using EnterpriseRagAssistant.Services;
using Microsoft.AspNetCore.Mvc;

namespace EnterpriseRagAssistant.Controllers;

[ApiController]
[Route("api/documents")]
public class DocumentsController : ControllerBase
{
    private readonly RagService _rag;

    public DocumentsController(RagService rag) => _rag = rag;

    [HttpPost("ingest")]
    [RequestSizeLimit(25 * 1024 * 1024)]
    public async Task<IActionResult> Ingest(IFormFile file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { message = "Upload a non-empty PDF file." });

        if (!Path.GetExtension(file.FileName).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "Only PDF files are supported." });

        await using var stream = file.OpenReadStream();
        var result = await _rag.IngestAsync(file.FileName, stream, cancellationToken);
        return Ok(result);
    }
}

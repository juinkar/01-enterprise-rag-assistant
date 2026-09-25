using EnterpriseRagAssistant.Models;
using EnterpriseRagAssistant.Services;
using Microsoft.AspNetCore.Mvc;

namespace EnterpriseRagAssistant.Controllers;

[ApiController]
[Route("api/chat")]
public class ChatController : ControllerBase
{
    private readonly RagService _rag;

    public ChatController(RagService rag) => _rag = rag;

    [HttpPost("ask")]
    public async Task<ActionResult<AskResponse>> Ask(AskRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
            return BadRequest(new { message = "Question is required." });

        return Ok(await _rag.AskAsync(request.Question, request.TopK, cancellationToken));
    }
}

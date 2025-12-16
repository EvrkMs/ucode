using Microsoft.AspNetCore.Mvc;

namespace Ucode.Backend.Controllers;

[ApiController]
[Route("diag")]
[Route("api/diag")]
public class DiagnosticsController(ILogger<DiagnosticsController> logger) : ControllerBase
{
    private readonly ILogger<DiagnosticsController> _logger = logger;

    public record ClientDiagRequest(string Type, string? Detail, string? Ua, double? T, object? Extra);

    [HttpPost("client")]
    public IActionResult Client([FromBody] ClientDiagRequest payload)
    {
        _logger.LogInformation("Client diag: {Type} {Detail} ua={Ua} t={T} extra={Extra}", payload.Type, payload.Detail, payload.Ua, payload.T, payload.Extra);
        return Ok();
    }
}

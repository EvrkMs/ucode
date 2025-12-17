using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ucode.Backend.Services;

namespace Ucode.Backend.Controllers;

[ApiController]
[Route("root")]
[Route("api/root")]
[Authorize(Roles = "root")]
public class RootController(IUserService userService, ILogger<RootController> logger) : ControllerBase
{
    private readonly IUserService _userService = userService;
    private readonly ILogger<RootController> _logger = logger;

    public record SearchResponse(long TelegramId, string? Username, string? FirstName, string? LastName, bool IsAdmin, bool IsRoot);
    public record SetAdminRequest(bool IsAdmin);

    [HttpGet("users")]
    public async Task<IActionResult> Search([FromQuery] string query, [FromQuery] int limit = 20, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return BadRequest(new { message = "Query is required" });
        }

        var results = await _userService.SearchAsync(query, limit, ct);
        var dto = results.Select(u => new SearchResponse(u.TelegramId, u.Username, u.FirstName, u.LastName, u.IsAdmin, u.IsRoot));
        return Ok(dto);
    }

    [HttpPost("users/{telegramId:long}/admin")]
    public async Task<IActionResult> SetAdmin(long telegramId, [FromBody] SetAdminRequest request, CancellationToken ct = default)
    {
        var actorId = HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        _logger.LogInformation("Root {Actor} sets admin={IsAdmin} for {Target}", actorId, request.IsAdmin, telegramId);

        var updated = await _userService.SetAdminAsync(telegramId, request.IsAdmin, ct);
        if (!updated)
        {
            return NotFound(new { message = "User not found" });
        }

        return Ok(new { message = "Updated" });
    }
}

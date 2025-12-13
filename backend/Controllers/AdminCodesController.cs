using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ucode.Backend.Models.Responses;
using Ucode.Backend.Models;
using Ucode.Backend.Services;

namespace Ucode.Backend.Controllers;

[ApiController]
[Route("codes/admin")]
[Route("api/codes/admin")]
[Authorize(Roles = "admin")]
public class AdminCodesController(ICodeService codeService, IUserService userService, ILogger<AdminCodesController> logger) : ControllerBase
{
    private readonly ICodeService _codeService = codeService;
    private readonly IUserService _userService = userService;
    private readonly ILogger<AdminCodesController> _logger = logger;

    [HttpPost("generate")]
    public async Task<IActionResult> Generate([FromBody] GenerateCodeRequest request)
    {
        var principal = HttpContext.User;
        var sub = principal.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!long.TryParse(sub, out var adminId))
        {
            return Unauthorized();
        }

        if (request.Points <= 0)
        {
            return BadRequest(new { message = "Points must be > 0" });
        }

        var admin = await _userService.GetByTelegramIdAsync(adminId);
        if (admin is null)
        {
            return Unauthorized();
        }

        _logger.LogInformation("Generate code requested by admin {AdminId} for {Points} points", admin.TelegramId, request.Points);

        try
        {
            var code = await _codeService.GenerateAsync(request.Points, adminId, TimeSpan.FromMinutes(40));
            return Ok(new GenerateCodeResponse
            {
                Code = code.Value,
                Points = code.Points,
                ExpiresAt = code.ExpiresAt
            });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to generate code: {Message}", ex.Message);
            return BadRequest(new { message = ex.Message });
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "DB error during code generation");
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Не удалось сохранить код, попробуйте позже" });
        }
    }

    [HttpGet("history")]
    public async Task<IActionResult> History()
    {
        var principal = HttpContext.User;
        var sub = principal.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!long.TryParse(sub, out var adminId))
        {
            return Unauthorized();
        }

        var admin = await _userService.GetByTelegramIdAsync(adminId);
        if (admin is null)
        {
            return Unauthorized();
        }

        var list = await _codeService.GetHistoryAsync();
        return Ok(list);
    }
}

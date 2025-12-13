using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ucode.Backend.Models;
using Ucode.Backend.Services;

namespace Ucode.Backend.Controllers;

[ApiController]
[Route("codes")]
[Route("api/codes")]
public class ClientCodesController(ICodeService codeService, ILeaderboardNotifier notifier) : ControllerBase
{
    private readonly ICodeService _codeService = codeService;
    private readonly ILeaderboardNotifier _notifier = notifier;

    [Authorize]
    [HttpPost("redeem")]
    public async Task<IActionResult> Redeem([FromBody] RedeemCodeRequest request)
    {
        var principal = HttpContext.User;
        var sub = principal.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!long.TryParse(sub, out var userId))
        {
            return Unauthorized();
        }

        var (success, message, newBalance) = await _codeService.RedeemAsync(request.Code, userId);
        if (!success)
        {
            return BadRequest(new { message });
        }

        await _notifier.BroadcastAsync();
        return Ok(new { balance = newBalance, message });
    }

    [AllowAnonymous]
    [HttpGet("leaderboard")]
    public async Task<IActionResult> Leaderboard()
    {
        var list = await _codeService.GetLeaderboardAsync();
        return Ok(list);
    }
}

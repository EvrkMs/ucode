using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Ucode.Backend.Models;
using Ucode.Backend.Options;
using Ucode.Backend.Models.Responses;
using Ucode.Backend.Services;

namespace Ucode.Backend.Controllers;

[ApiController]
[Route("auth")]
[Route("api/auth")]
public class AuthController(
    ITelegramAuthValidator validator,
    ITokenService tokenService,
    IUserService userService,
    IOptions<JwtOptions> jwtOptions,
    ILogger<AuthController> logger) : ControllerBase
{
    private readonly ITelegramAuthValidator _validator = validator;
    private readonly ITokenService _tokenService = tokenService;
    private readonly IUserService _userService = userService;
    private readonly JwtOptions _jwtOptions = jwtOptions.Value;
    private readonly ILogger<AuthController> _logger = logger;

    [HttpPost("telegram")]
    public async Task<IActionResult> TelegramAuth([FromBody] TelegramAuthRequest request)
    {
        try
        {
            if (!_validator.TryValidate(request.InitData, out var authData, out var error))
            {
                return BadRequest(new { message = error });
            }

            var userEntity = await _userService.UpsertTelegramUserAsync(authData.User, authData.AuthDate);
            var token = _tokenService.IssueToken(userEntity);

            var cookieOptions = new CookieOptions
            {
                HttpOnly = false,
                Secure = true,
                SameSite = SameSiteMode.None,
                Path = "/",
                Expires = token.ExpiresAt
            };
            Response.Cookies.Append("csrf", token.CsrfToken, cookieOptions);

            return Ok(new
            {
                token = token.Token,
                expiresAt = token.ExpiresAt,
                user = token.User,
                csrfToken = token.CsrfToken
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process Telegram auth");
            return Problem(ex.Message);
        }
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var authHeader = HttpContext.Request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(authHeader))
        {
            _logger.LogDebug("Authorization header is missing on /auth/me");
        }
        else
        {
            _logger.LogDebug("Authorization header on /auth/me: {Header}", authHeader);
        }

        var principal = HttpContext.User;
        var sub = principal.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!long.TryParse(sub, out var userId))
        {
            _logger.LogWarning("No sub in principal");
            return Unauthorized();
        }

        var user = await _userService.GetByTelegramIdAsync(userId);
        if (user is null)
        {
            _logger.LogWarning("Principal without tg user, claims: {Claims}", string.Join(",", principal.Claims.Select(c => $"{c.Type}={c.Value}")));
            return Unauthorized();
        }

        return Ok(new AuthMeResponse
        {
            User = new AuthUserDto
            {
                Id = user.TelegramId,
                Username = user.Username,
                FirstName = user.FirstName,
                LastName = user.LastName,
                PhotoUrl = user.PhotoUrl,
                Role = user.IsRoot ? "root" : user.IsAdmin ? "admin" : "client",
                Balance = user.Balance
            }
        });
    }

    [AllowAnonymous]
    [HttpGet("config")]
    public IActionResult Config()
    {
        return Ok(new AuthConfigResponse
        {
            Issuer = _jwtOptions.Issuer,
            Audience = _jwtOptions.Audience,
            TokenTtlSeconds = _jwtOptions.LifetimeMinutes * 60
        });
    }
}

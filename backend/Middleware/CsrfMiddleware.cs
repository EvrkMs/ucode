using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Ucode.Backend.Middleware;

public sealed class CsrfMiddleware(RequestDelegate next, ILogger<CsrfMiddleware> logger)
{
    private static readonly HashSet<string> SafeMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        HttpMethods.Get, HttpMethods.Head, HttpMethods.Options, HttpMethods.Trace
    };

    private static readonly HashSet<string> ExcludedPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/auth/telegram",
        "/api/auth/telegram",
        "/diag/client",
        "/api/diag/client"
    };

    public async Task InvokeAsync(HttpContext context)
    {
        if (SafeMethods.Contains(context.Request.Method) || ExcludedPaths.Contains(context.Request.Path.Value ?? string.Empty))
        {
            await next(context);
            return;
        }

        var headerToken = context.Request.Headers["X-CSRF-Token"].ToString();
        var cookieToken = context.Request.Cookies["csrf"];
        if (string.IsNullOrWhiteSpace(headerToken) || string.IsNullOrWhiteSpace(cookieToken))
        {
            logger.LogWarning("CSRF validation failed: missing token (header or cookie)");
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new { message = "CSRF token is missing" });
            return;
        }

        if (!string.Equals(headerToken, cookieToken, StringComparison.Ordinal))
        {
            logger.LogWarning("CSRF validation failed: token mismatch");
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new { message = "CSRF token is invalid" });
            return;
        }

        var claimToken = context.User.FindFirst("csrf")?.Value;
        if (!string.IsNullOrWhiteSpace(claimToken) && !string.Equals(claimToken, headerToken, StringComparison.Ordinal))
        {
            logger.LogWarning("CSRF validation failed: claim mismatch");
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new { message = "CSRF token is invalid" });
            return;
        }

        await next(context);
    }
}

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
        await next(context);
    }
}

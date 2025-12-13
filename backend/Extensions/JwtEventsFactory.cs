using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using System.Security.Claims;

namespace Ucode.Backend.Extensions;

public static class JwtEventsFactory
{
    public static JwtBearerEvents Create()
    {
        return new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                logger.LogWarning(context.Exception, "JWT authentication failed");
                return Task.CompletedTask;
            },
            OnTokenValidated = context =>
            {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                var sub = context.Principal?.FindFirstValue(JwtRegisteredClaimNames.Sub);
                logger.LogDebug("JWT validated for sub={Sub}", sub);
                return Task.CompletedTask;
            },
            OnChallenge = context =>
            {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                var path = context.HttpContext.Request.Path.Value;
                logger.LogWarning("JWT challenge on {Path}: {Error} - {Description}", path, context.Error, context.ErrorDescription);
                return Task.CompletedTask;
            }
        };
    }
}

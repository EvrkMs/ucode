using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Ucode.Backend.Data;
using Ucode.Backend.Middleware;
using Ucode.Backend.Services;

namespace Ucode.Backend.Extensions;

public static class WebApplicationExtensions
{
    public static WebApplication UseApplication(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
        }

        app.UseResponseCompression();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseWebSockets();
        app.Use(async (context, next) =>
        {
            context.Response.Headers.Remove("X-Frame-Options");
            context.Response.Headers["Content-Security-Policy"] =
                "default-src 'self'; " +
                "base-uri 'self'; " +
                "form-action 'self'; " +
                "frame-ancestors 'self' https://web.telegram.org https://t.me https://*.telegram.org; " +
                "script-src 'self' https://telegram.org 'unsafe-inline'; " +
                "style-src 'self' 'unsafe-inline'; " +
                "img-src 'self' data: https:; " +
                "connect-src 'self' https: wss: ws:; " +
                "font-src 'self' data:; " +
                "object-src 'none'";
            await next();
        });
        // CSRF middleware отключён, так как фронт работает внутри Telegram WebApp и токен не требуется.
        app.Use(async (context, next) =>
        {
            var path = context.Request.Path.Value?.ToLowerInvariant() ?? string.Empty;
            if (path.Contains("/auth/config"))
            {
                context.Response.Headers.CacheControl = "public, max-age=3600";
            }
            else if (path.Contains("/codes/leaderboard"))
            {
                context.Response.Headers.CacheControl = "public, max-age=5";
            }
            else if (path.Contains("/auth/me"))
            {
                context.Response.Headers.CacheControl = "private, max-age=60";
            }
            else if (path.StartsWith("/api/") || path.StartsWith("/codes/"))
            {
                context.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
                context.Response.Headers.Pragma = "no-cache";
            }

            await next();
        });

        var staticRoot = Path.Combine(app.Environment.ContentRootPath, "wwwroot");
        var hasStaticFiles = Directory.Exists(staticRoot);
        if (hasStaticFiles)
        {
            app.UseStaticFiles(new StaticFileOptions
            {
                OnPrepareResponse = ctx =>
                {
                    var headers = ctx.Context.Response.Headers;
                    if (ctx.File.Name.Equals("index.html", StringComparison.OrdinalIgnoreCase))
                    {
                        headers.CacheControl = "no-cache, no-store, must-revalidate";
                        headers.Pragma = "no-cache";
                        headers.Expires = "0";
                    }
                    else
                    {
                        headers.CacheControl = "public, max-age=31536000, immutable";
                    }
                }
            });
        }

        app.MapGet("/health", () => Results.Ok("ok"));
        app.MapControllers();

        if (hasStaticFiles)
        {
            app.MapFallbackToFile("index.html", new StaticFileOptions
            {
                OnPrepareResponse = ctx =>
                {
                    var headers = ctx.Context.Response.Headers;
                    headers.CacheControl = "no-cache, no-store, must-revalidate";
                    headers.Pragma = "no-cache";
                    headers.Expires = "0";
                }
            });
        }

        MapWebSockets(app);
        ApplyMigrations(app);
        return app;
    }

    private static void MapWebSockets(WebApplication app)
    {
        app.Map("/ws/leaderboard", async context =>
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsync("Expected WebSocket request");
                return;
            }

            var notifier = context.RequestServices.GetRequiredService<ILeaderboardNotifier>();
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
            var socket = await context.WebSockets.AcceptWebSocketAsync();
            await notifier.RegisterAsync(socket, cts.Token);
        });

        app.Map("/api/ws/leaderboard", async context =>
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsync("Expected WebSocket request");
                return;
            }

            var notifier = context.RequestServices.GetRequiredService<ILeaderboardNotifier>();
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
            var socket = await context.WebSockets.AcceptWebSocketAsync();
            await notifier.RegisterAsync(socket, cts.Token);
        });
    }

    private static void ApplyMigrations(WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<UcodeDbContext>();
        db.Database.Migrate();
    }
}

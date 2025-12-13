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

        app.UseAuthentication();
        app.UseAuthorization();
        app.UseWebSockets();
        app.UseMiddleware<CsrfMiddleware>();

        var staticRoot = Path.Combine(app.Environment.ContentRootPath, "wwwroot");
        var hasStaticFiles = Directory.Exists(staticRoot);
        if (hasStaticFiles)
        {
            app.UseStaticFiles(new StaticFileOptions
            {
                OnPrepareResponse = ctx =>
                {
                    var headers = ctx.Context.Response.Headers;
                    headers.CacheControl = "public, max-age=31536000, immutable";
                }
            });
        }

        app.MapGet("/health", () => Results.Ok("ok"));
        app.MapControllers();

        if (hasStaticFiles)
        {
            app.MapFallbackToFile("index.html");
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

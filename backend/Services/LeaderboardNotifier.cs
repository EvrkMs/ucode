using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Ucode.Backend.Entities;

namespace Ucode.Backend.Services;

public interface ILeaderboardNotifier
{
    Task RegisterAsync(WebSocket socket, CancellationToken ct = default);
    Task BroadcastAsync(CancellationToken ct = default);
}

public sealed class LeaderboardNotifier(IServiceScopeFactory scopeFactory, ILogger<LeaderboardNotifier> logger, IOptions<JsonOptions> jsonOptions) : ILeaderboardNotifier
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly ILogger<LeaderboardNotifier> _logger = logger;
    private readonly JsonSerializerOptions _serializerOptions = jsonOptions.Value.JsonSerializerOptions;
    private readonly ConcurrentDictionary<Guid, WebSocket> _sockets = new();

    public async Task RegisterAsync(WebSocket socket, CancellationToken ct = default)
    {
        var id = Guid.NewGuid();
        _sockets[id] = socket;
        _logger.LogDebug("Leaderboard WS connected: {Id}", id);

        try
        {
            // отправляем начальные данные
            await SendLeaderboardAsync(socket, ct);
            await ListenAsync(id, socket, ct);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "WS session ended with error for {Id}", id);
            _sockets.TryRemove(id, out _);
            await SafeCloseAsync(socket, ct);
        }
    }

    public async Task BroadcastAsync(CancellationToken ct = default)
    {
        var data = await GetLeaderboardPayloadAsync(ct);
        var bytes = Encoding.UTF8.GetBytes(data);
        var toRemove = new List<Guid>();

        foreach (var kv in _sockets)
        {
            var socket = kv.Value;
            if (socket.State != WebSocketState.Open)
            {
                toRemove.Add(kv.Key);
                continue;
            }

            try
            {
                await socket.SendAsync(bytes, WebSocketMessageType.Text, true, ct);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to broadcast to socket {Id}", kv.Key);
                toRemove.Add(kv.Key);
            }
        }

        foreach (var id in toRemove)
        {
            if (_sockets.TryRemove(id, out var socket))
            {
                await SafeCloseAsync(socket, ct);
            }
        }
    }

    private async Task ListenAsync(Guid id, WebSocket socket, CancellationToken ct)
    {
        var buffer = new byte[4];
        try
        {
            while (socket.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                var result = await socket.ReceiveAsync(buffer, ct);
                if (result.CloseStatus.HasValue)
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // ignore
        }
        catch (WebSocketException ex)
        {
            _logger.LogDebug(ex, "WS receive failed for {Id}", id);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "WS listen error for {Id}", id);
        }
        finally
        {
            _sockets.TryRemove(id, out _);
            await SafeCloseAsync(socket, ct);
            _logger.LogDebug("Leaderboard WS disconnected: {Id}", id);
        }
    }

    private async Task SendLeaderboardAsync(WebSocket socket, CancellationToken ct)
    {
        var data = await GetLeaderboardPayloadAsync(ct);
        var bytes = Encoding.UTF8.GetBytes(data);
        await socket.SendAsync(bytes, WebSocketMessageType.Text, true, ct);
    }

    private async Task<string> GetLeaderboardPayloadAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var codeService = scope.ServiceProvider.GetRequiredService<ICodeService>();
        var list = await codeService.GetLeaderboardAsync(100, ct);
        return JsonSerializer.Serialize(list, _serializerOptions);
    }

    private static async Task SafeCloseAsync(WebSocket socket, CancellationToken ct)
    {
        try
        {
            if (socket.State == WebSocketState.Open)
            {
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "closing", ct);
            }
            socket.Dispose();
        }
        catch
        {
            // ignore
        }
    }
}

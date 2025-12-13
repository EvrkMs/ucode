using Microsoft.EntityFrameworkCore;
using Ucode.Backend.Data;
using Ucode.Backend.Entities;

namespace Ucode.Backend.Services;

public interface ICodeService
{
    Task<Code> GenerateAsync(int points, long adminId, TimeSpan ttl, CancellationToken ct = default);
    Task<(bool success, string message, long newBalance)> RedeemAsync(string codeValue, long userId, CancellationToken ct = default);
    Task<List<Code>> GetHistoryAsync(int take = 100, CancellationToken ct = default);
    Task<List<LeaderboardItem>> GetLeaderboardAsync(int take = 100, CancellationToken ct = default);
}

public sealed class CodeService(UcodeDbContext dbContext) : ICodeService
{
    private readonly UcodeDbContext _dbContext = dbContext;
    private static readonly char[] Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789".ToCharArray();
    private static readonly Random Random = new();

    public async Task<Code> GenerateAsync(int points, long adminId, TimeSpan ttl, CancellationToken ct = default)
    {
        if (points <= 0) throw new ArgumentException("points must be > 0", nameof(points));

        const int maxAttempts = 5;
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            var code = new Code
            {
                Value = GenerateCode(),
                Points = points,
                CreatedBy = adminId,
                CreatedAt = DateTimeOffset.UtcNow,
                ExpiresAt = DateTimeOffset.UtcNow.Add(ttl),
                Used = false
            };

            _dbContext.Codes.Add(code);
            try
            {
                await _dbContext.SaveChangesAsync(ct);
                return code;
            }
            catch (DbUpdateException) when (attempt < maxAttempts - 1)
            {
                // Вероятная коллизия уникального индекса по Value — пробуем сгенерировать новый код.
                _dbContext.Entry(code).State = EntityState.Detached;
                continue;
            }
        }

        throw new InvalidOperationException("Не удалось сгенерировать уникальный код.");
    }

    public async Task<(bool success, string message, long newBalance)> RedeemAsync(string codeValue, long userId, CancellationToken ct = default)
    {
        await using var tx = await _dbContext.Database.BeginTransactionAsync(ct);

        var now = DateTimeOffset.UtcNow;
        var code = await _dbContext.Codes.FirstOrDefaultAsync(c => c.Value == codeValue, ct);
        if (code is null)
        {
            return (false, "Код не найден", 0);
        }

        if (code.Used)
        {
            return (false, "Код уже использован", 0);
        }

        if (code.ExpiresAt < now)
        {
            return (false, "Срок действия кода истёк", 0);
        }

        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.TelegramId == userId, ct);
        if (user is null)
        {
            return (false, "Пользователь не найден", 0);
        }

        code.Used = true;
        code.UsedBy = userId;
        code.UsedAt = now;
        user.Balance += code.Points;
        user.UpdatedAt = now;

        try
        {
            await _dbContext.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return (true, "Баллы начислены", user.Balance);
        }
        catch (DbUpdateConcurrencyException)
        {
            await tx.RollbackAsync(ct);
            return (false, "Код уже использован или изменён", user.Balance);
        }
        catch (DbUpdateException)
        {
            await tx.RollbackAsync(ct);
            return (false, "Не удалось применить код, попробуйте позже", user.Balance);
        }
    }

    public Task<List<Code>> GetHistoryAsync(int take = 100, CancellationToken ct = default)
    {
        return _dbContext.Codes
            .AsNoTracking()
            .OrderByDescending(c => c.CreatedAt)
            .Take(take)
            .ToListAsync(ct);
    }

    public Task<List<LeaderboardItem>> GetLeaderboardAsync(int take = 100, CancellationToken ct = default)
    {
        return _dbContext.Users
            .AsNoTracking()
            .Where(u => u.Balance > 0)
            .OrderByDescending(u => u.Balance)
            .ThenBy(u => u.TelegramId)
            .Take(take)
            .Select(u => new LeaderboardItem(
                u.TelegramId,
                u.Username,
                u.FirstName,
                u.LastName,
                u.PhotoUrl,
                u.Balance))
            .ToListAsync(ct);
    }

    private static string GenerateCode(int length = 5)
    {
        var chars = new char[length];
        for (var i = 0; i < length; i++)
        {
            chars[i] = Alphabet[Random.Next(Alphabet.Length)];
        }
        return new string(chars);
    }
}

public sealed record LeaderboardItem(long TelegramId, string? Username, string? FirstName, string? LastName, string? PhotoUrl, long Balance);

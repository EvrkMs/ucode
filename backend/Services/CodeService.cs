using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using Ucode.Backend.Data;
using Ucode.Backend.Entities;
using Ucode.Backend.Models.Responses;

namespace Ucode.Backend.Services;

public interface ICodeService
{
    Task<Code> GenerateAsync(int points, long adminId, TimeSpan ttl, CancellationToken ct = default);
    Task<(bool success, string message, long newBalance)> RedeemAsync(string codeValue, long userId, CancellationToken ct = default);
    Task<List<CodeHistoryItemResponse>> GetHistoryAsync(int take = 100, CancellationToken ct = default);
    Task<List<LeaderboardItem>> GetLeaderboardAsync(int take = 100, CancellationToken ct = default);
    Task<long> GetBalanceAsync(long userId, CancellationToken ct = default);
}

public sealed class CodeService(UcodeDbContext dbContext) : ICodeService
{
    private readonly UcodeDbContext _dbContext = dbContext;
    private static readonly char[] Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789".ToCharArray();

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
        user.UpdatedAt = now;

        try
        {
            await _dbContext.SaveChangesAsync(ct);
            var newBalance = await GetBalanceAsync(userId, ct);
            return (true, "Баллы начислены", newBalance);
        }
        catch (DbUpdateConcurrencyException)
        {
            var currentBalance = await GetBalanceAsync(userId, ct);
            return (false, "Код уже использован или изменён", currentBalance);
        }
        catch (DbUpdateException)
        {
            var currentBalance = await GetBalanceAsync(userId, ct);
            return (false, "Не удалось применить код, попробуйте позже", currentBalance);
        }
    }

    public Task<List<CodeHistoryItemResponse>> GetHistoryAsync(int take = 100, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        return (from code in _dbContext.Codes.AsNoTracking()
                where code.Used || code.ExpiresAt > now
                join user in _dbContext.Users.AsNoTracking()
                    on code.UsedBy equals user.TelegramId into users
                from user in users.DefaultIfEmpty()
                orderby code.CreatedAt descending
                select new CodeHistoryItemResponse
                {
                    Id = code.Id,
                    Value = code.Value,
                    Points = code.Points,
                    CreatedAt = code.CreatedAt,
                    ExpiresAt = code.ExpiresAt,
                    Used = code.Used,
                    UsedAt = code.UsedAt,
                    UsedByTag = code.Used && user != null && !string.IsNullOrWhiteSpace(user.Username)
                        ? (user.Username.StartsWith("@", StringComparison.Ordinal) ? user.Username : $"@{user.Username}")
                        : null
                })
            .Take(take)
            .ToListAsync(ct);
    }

    public Task<List<LeaderboardItem>> GetLeaderboardAsync(int take = 100, CancellationToken ct = default)
    {
        var totals = _dbContext.Codes
            .AsNoTracking()
            .Where(c => c.Used && c.UsedBy != null)
            .GroupBy(c => c.UsedBy!.Value)
            .Select(g => new { TelegramId = g.Key, Balance = g.Sum(c => (long)c.Points) });

        return (from total in totals
                join user in _dbContext.Users.AsNoTracking() on total.TelegramId equals user.TelegramId
                orderby total.Balance descending, user.TelegramId
                select new LeaderboardItem(
                    user.TelegramId,
                    user.Username,
                    user.FirstName,
                    user.LastName,
                    user.PhotoUrl,
                    total.Balance))
            .Take(take)
            .ToListAsync(ct);
    }

    public async Task<long> GetBalanceAsync(long userId, CancellationToken ct = default)
    {
        var total = await _dbContext.Codes
            .AsNoTracking()
            .Where(c => c.Used && c.UsedBy == userId)
            .Select(c => (long?)c.Points)
            .SumAsync(ct);
        return total ?? 0;
    }

    private static string GenerateCode(int length = 5)
    {
        var chars = new char[length];
        for (var i = 0; i < length; i++)
        {
            var index = RandomNumberGenerator.GetInt32(Alphabet.Length);
            chars[i] = Alphabet[index];
        }
        return new string(chars);
    }
}

public sealed record LeaderboardItem(long TelegramId, string? Username, string? FirstName, string? LastName, string? PhotoUrl, long Balance);

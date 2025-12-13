using Microsoft.EntityFrameworkCore;
using Ucode.Backend.Data;
using Ucode.Backend.Entities;
using Ucode.Backend.Models;

namespace Ucode.Backend.Services;

public interface IUserService
{
    Task<User> UpsertTelegramUserAsync(TelegramUser tgUser, DateTimeOffset authDate, CancellationToken ct = default);
    Task<User?> GetByTelegramIdAsync(long telegramId, CancellationToken ct = default);
}

public sealed class UserService(UcodeDbContext dbContext, ILogger<UserService> logger) : IUserService
{
    private readonly UcodeDbContext _dbContext = dbContext;
    private readonly ILogger<UserService> _logger = logger;

    public async Task<User> UpsertTelegramUserAsync(TelegramUser tgUser, DateTimeOffset authDate, CancellationToken ct = default)
    {
        var existing = await _dbContext.Users.AsTracking().FirstOrDefaultAsync(u => u.TelegramId == tgUser.Id, ct);
        var now = DateTimeOffset.UtcNow;

        if (existing is null)
        {
            var user = new User
            {
                TelegramId = tgUser.Id,
                Username = tgUser.Username,
                FirstName = tgUser.FirstName,
                LastName = tgUser.LastName,
                LanguageCode = tgUser.LanguageCode,
                PhotoUrl = tgUser.PhotoUrl,
                IsBot = tgUser.IsBot,
                IsPremium = tgUser.IsPremium,
                LastAuthAt = authDate,
                CreatedAt = now,
                UpdatedAt = now
            };

            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync(ct);
            return user;
        }

        existing.Username = tgUser.Username;
        existing.FirstName = tgUser.FirstName;
        existing.LastName = tgUser.LastName;
        existing.LanguageCode = tgUser.LanguageCode;
        existing.PhotoUrl = tgUser.PhotoUrl;
        existing.IsBot = tgUser.IsBot;
        existing.IsPremium = tgUser.IsPremium;
        existing.LastAuthAt = authDate;
        existing.UpdatedAt = now;

        await _dbContext.SaveChangesAsync(ct);
        return existing;
    }

    public Task<User?> GetByTelegramIdAsync(long telegramId, CancellationToken ct = default)
    {
        return _dbContext.Users.AsNoTracking().FirstOrDefaultAsync(u => u.TelegramId == telegramId, ct);
    }
}

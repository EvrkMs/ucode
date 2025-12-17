using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Ucode.Backend.Models;
using Ucode.Backend.Options;

namespace Ucode.Backend.Services;

public interface ITokenService
{
    AuthToken IssueToken(Entities.User user);
    TelegramUser? ReadUser(ClaimsPrincipal principal);
}

public sealed class TokenService(IOptions<JwtOptions> jwtOptions, IMemoryCache cache) : ITokenService
{
    private readonly JwtOptions _options = jwtOptions.Value;
    private readonly IMemoryCache _cache = cache;

    public AuthToken IssueToken(Entities.User user)
    {
        if (user.TelegramId == 0)
        {
            throw new InvalidOperationException("Telegram user id is required for token issuing.");
        }

        if (string.IsNullOrWhiteSpace(_options.SigningKey))
        {
            throw new InvalidOperationException("Jwt:SigningKey is not configured.");
        }

        var signingKeyBytes = Encoding.UTF8.GetBytes(_options.SigningKey);
        var signingKey = new SymmetricSecurityKey(signingKeyBytes);
        var signingKeyHash = Convert.ToHexString(SHA256.HashData(signingKeyBytes));

        if (_cache.TryGetValue<TokenCacheEntry>(user.TelegramId, out var existing) &&
            existing is not null &&
            existing.ExpiresAt > DateTimeOffset.UtcNow &&
            string.Equals(existing.SigningKeyHash, signingKeyHash, StringComparison.Ordinal))
        {
            return new AuthToken(existing.Token, existing.ExpiresAt, existing.User, existing.CsrfToken);
        }

        var now = DateTimeOffset.UtcNow;
        var expiresAt = now.AddMinutes(_options.LifetimeMinutes <= 0 ? 60 : _options.LifetimeMinutes);
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);
        var handler = new JwtSecurityTokenHandler();
        var csrfToken = Guid.NewGuid().ToString("N");

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.TelegramId.ToString()),
            new(JwtRegisteredClaimNames.UniqueName, user.Username ?? user.TelegramId.ToString()),
            new("tg:first_name", user.FirstName ?? string.Empty),
            new("tg:last_name", user.LastName ?? string.Empty),
            new("tg:username", user.Username ?? string.Empty),
            new("csrf", csrfToken)
        };

        if (!string.IsNullOrWhiteSpace(user.PhotoUrl))
        {
            claims.Add(new Claim("tg:photo_url", user.PhotoUrl));
        }

        if (user.IsAdmin || user.IsRoot)
        {
            claims.Add(new Claim(ClaimTypes.Role, "admin"));
            claims.Add(new Claim("role", "admin"));
        }

        if (user.IsRoot)
        {
            claims.Add(new Claim(ClaimTypes.Role, "root"));
            claims.Add(new Claim("role", "root"));
        }

        var descriptor = new SecurityTokenDescriptor
        {
            Audience = _options.Audience,
            Issuer = _options.Issuer,
            Expires = expiresAt.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Subject = new ClaimsIdentity(claims),
            SigningCredentials = credentials
        };

        var token = handler.CreateToken(descriptor);
        var tokenString = handler.WriteToken(token);

        var cacheEntry = new TokenCacheEntry(tokenString, expiresAt, new TelegramUser
        {
            Id = user.TelegramId,
            Username = user.Username,
            FirstName = user.FirstName,
            LastName = user.LastName,
            PhotoUrl = user.PhotoUrl,
            IsPremium = user.IsPremium,
            IsBot = user.IsBot
        }, signingKeyHash, csrfToken);
        _cache.Set(user.TelegramId, cacheEntry, cacheEntry.ExpiresAt);

        return new AuthToken(tokenString, cacheEntry.ExpiresAt, cacheEntry.User, csrfToken);
    }

    public TelegramUser? ReadUser(ClaimsPrincipal principal)
    {
        var idValue = principal.FindFirstValue(JwtRegisteredClaimNames.Sub) 
                      ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!long.TryParse(idValue, out var userId))
        {
            return null;
        }

        return new TelegramUser
        {
            Id = userId,
            FirstName = principal.FindFirstValue("tg:first_name"),
            LastName = principal.FindFirstValue("tg:last_name"),
            Username = principal.FindFirstValue("tg:username"),
            PhotoUrl = principal.FindFirstValue("tg:photo_url")
        };
    }

    private sealed record TokenCacheEntry(string Token, DateTimeOffset ExpiresAt, TelegramUser User, string SigningKeyHash, string CsrfToken);
}

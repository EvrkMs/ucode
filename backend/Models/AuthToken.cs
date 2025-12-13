namespace Ucode.Backend.Models;

public sealed class AuthToken
{
    public AuthToken(string token, DateTimeOffset expiresAt, TelegramUser user, string csrfToken)
    {
        Token = token;
        ExpiresAt = expiresAt;
        User = user;
        CsrfToken = csrfToken;
    }

    public string Token { get; }
    public DateTimeOffset ExpiresAt { get; }
    public TelegramUser User { get; }
    public string CsrfToken { get; }
}

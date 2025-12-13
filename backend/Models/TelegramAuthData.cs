namespace Ucode.Backend.Models;

public sealed class TelegramAuthData
{
    public TelegramAuthData(TelegramUser user, DateTimeOffset authDate)
    {
        User = user;
        AuthDate = authDate;
    }

    public TelegramUser User { get; }
    public DateTimeOffset AuthDate { get; }
}

namespace Ucode.Backend.Entities;

public class User
{
    public long TelegramId { get; set; }
    public string? Username { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? LanguageCode { get; set; }
    public string? PhotoUrl { get; set; }
    public bool? IsBot { get; set; }
    public bool? IsPremium { get; set; }
    public bool IsAdmin { get; set; }
    public bool IsRoot { get; set; }
    public DateTimeOffset LastAuthAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

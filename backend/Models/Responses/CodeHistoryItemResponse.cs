namespace Ucode.Backend.Models.Responses;

public sealed class CodeHistoryItemResponse
{
    public Guid Id { get; init; }
    public string Value { get; init; } = string.Empty;
    public int Points { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset ExpiresAt { get; init; }
    public bool Used { get; init; }
    public DateTimeOffset? UsedAt { get; init; }
    public string? UsedByTag { get; init; }
}

namespace Ucode.Backend.Models.Responses;

public sealed class GenerateCodeResponse
{
    public required string Code { get; init; }
    public int Points { get; init; }
    public DateTimeOffset ExpiresAt { get; init; }
}

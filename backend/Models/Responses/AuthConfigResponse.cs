namespace Ucode.Backend.Models.Responses;

public sealed class AuthConfigResponse
{
    public required string Issuer { get; init; }
    public required string Audience { get; init; }
    public int TokenTtlSeconds { get; init; }
}

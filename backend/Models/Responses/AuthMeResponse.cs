namespace Ucode.Backend.Models.Responses;

public sealed class AuthMeResponse
{
    public required AuthUserDto User { get; init; }
}

public sealed class AuthUserDto
{
    public long Id { get; init; }
    public string? Username { get; init; }
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    public string? PhotoUrl { get; init; }
    public string Role { get; init; } = "client";
    public long Balance { get; init; }
}

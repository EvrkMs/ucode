namespace Ucode.Backend.Options;

public sealed class JwtOptions
{
    public string Issuer { get; set; } = "ucode";
    public string Audience { get; set; } = "ucode-web";
    public string SigningKey { get; set; } = string.Empty;
    public int LifetimeMinutes { get; set; } = 60;
}

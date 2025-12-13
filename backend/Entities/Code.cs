namespace Ucode.Backend.Entities;

public class Code
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Value { get; set; } = string.Empty;
    public int Points { get; set; }
    public long CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public bool Used { get; set; }
    public long? UsedBy { get; set; }
    public DateTimeOffset? UsedAt { get; set; }
}

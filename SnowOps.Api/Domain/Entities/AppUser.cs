namespace SnowOps.Api.Domain.Entities;

public sealed class AppUser
{
    public Guid Id { get; set; }
    public string Login { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

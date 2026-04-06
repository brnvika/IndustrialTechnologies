namespace SnowOps.Api.Contracts;

public sealed class UserDto
{
    public Guid Id { get; set; }
    public string Login { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}
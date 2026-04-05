namespace SnowOps.Api.Domain;

public sealed class WorkReport
{
    public long Id { get; set; }
    public long TaskId { get; set; }
    public string Employee { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Comment { get; set; } = string.Empty;
    public string[] Photos { get; set; } = [];
    public DateTimeOffset CreatedAt { get; set; }
}

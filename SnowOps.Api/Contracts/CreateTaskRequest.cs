namespace SnowOps.Api.Contracts;

public sealed class CreateTaskRequest
{
    public long ZoneId { get; set; }
    public string Type { get; set; } = string.Empty;
    public int CoveragePercent { get; set; }
    public string[] Photos { get; set; } = [];
}

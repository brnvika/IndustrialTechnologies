namespace SnowOps.Api.Contracts;

public sealed class FixTaskRequest
{
    public string Employee { get; set; } = string.Empty;
    public string Comment { get; set; } = string.Empty;
    public string[] PhotoUrls { get; set; } = [];
}

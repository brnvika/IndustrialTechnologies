namespace SnowOps.Api.Domain;

public sealed class SnowTask
{
    public long Id { get; set; }
    public long ZoneId { get; set; }
    public string ZoneCode { get; set; } = string.Empty;
    public string ZoneName { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string Type { get; set; } = string.Empty;
    public string LocationType { get; set; } = "Обычный переход";
    public int CoveragePercent { get; set; }
    public string Status { get; set; } = "found";
    public string? Owner { get; set; }
    public DateTimeOffset FoundAt { get; set; }
    public DateTimeOffset? FixedAt { get; set; }
    public string[] Photos { get; set; } = [];
    public string? FixComment { get; set; }
    public int Risk { get; set; }
}

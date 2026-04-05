namespace SnowOps.Api.Contracts;

public sealed class ReportSummaryDto
{
    public int TotalDefects { get; set; }
    public int Found { get; set; }
    public int InProgress { get; set; }
    public int Fixed { get; set; }
    public double? AvgFixTimeMinutes { get; set; }
    public double? MedianFixTimeMinutes { get; set; }
    public Dictionary<string, int> ByType { get; set; } = new();
    public List<MapItemDto> MapItems { get; set; } = new();
}

public sealed class MapItemDto
{
    public Guid Id { get; set; }
    public long PublicId { get; set; }
    public double Lat { get; set; }
    public double Lon { get; set; }
    public int Status { get; set; }
    public int RiskR { get; set; }
    public int DefectType { get; set; }
}

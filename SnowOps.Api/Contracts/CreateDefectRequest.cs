namespace SnowOps.Api.Contracts;

public sealed class CreateDefectRequest
{
    public Guid DistrictId { get; set; }
    public double Lat { get; set; }
    public double Lon { get; set; }
    public string? AreaCode { get; set; }
    public string? AreaName { get; set; }
    public int DefectType { get; set; }
    public int CoveragePercent { get; set; }
    public int LocationType { get; set; }
}

namespace SnowOps.Api.Contracts;

public sealed class CreateDistrictRequest
{
    public string Name { get; set; } = string.Empty;
    public double CenterLat { get; set; }
    public double CenterLon { get; set; }
}

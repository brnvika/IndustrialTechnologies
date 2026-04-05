namespace SnowOps.Api.Contracts;

public sealed class CreateZoneRequest
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string LocationType { get; set; } = "Обычный переход";
}

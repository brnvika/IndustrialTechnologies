namespace SnowOps.Api.Domain;

public sealed class SnowZone
{
    public long Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string LocationType { get; set; } = "Обычный переход";
    public DateTimeOffset CreatedAt { get; set; }
}

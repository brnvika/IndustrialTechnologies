namespace SnowOps.Api.Contracts;

public sealed class DistrictDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public double CenterLat { get; set; }
    public double CenterLon { get; set; }
    public string? Timezone { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

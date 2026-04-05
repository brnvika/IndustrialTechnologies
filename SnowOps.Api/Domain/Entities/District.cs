namespace SnowOps.Api.Domain.Entities;

public sealed class District
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public double CenterLat { get; set; }
    public double CenterLon { get; set; }
    public string? Timezone { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<WeatherObservation> WeatherObservations { get; set; } = new List<WeatherObservation>();
    public ICollection<Defect> Defects { get; set; } = new List<Defect>();
}

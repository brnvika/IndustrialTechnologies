namespace SnowOps.Api.Domain.Entities;

public sealed class WeatherObservation
{
    public Guid Id { get; set; }
    public Guid DistrictId { get; set; }
    public DateTimeOffset ObservedAt { get; set; }
    public decimal TempC { get; set; }
    public decimal Precipitation3hMm { get; set; }
    public decimal Snowfall3hMm { get; set; }
    public bool SnowOrIceLast3h { get; set; }
    public string? Raw { get; set; } // jsonb stored as string
    public string Source { get; set; } = "open-meteo";

    public District District { get; set; } = null!;
}

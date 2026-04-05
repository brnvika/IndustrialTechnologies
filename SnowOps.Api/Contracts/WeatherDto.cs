namespace SnowOps.Api.Contracts;

public sealed class WeatherDto
{
    public Guid Id { get; set; }
    public Guid DistrictId { get; set; }
    public DateTimeOffset ObservedAt { get; set; }
    public decimal TempC { get; set; }
    public decimal Precipitation3hMm { get; set; }
    public decimal Snowfall3hMm { get; set; }
    public bool SnowOrIceLast3h { get; set; }
    public string Source { get; set; } = string.Empty;
}

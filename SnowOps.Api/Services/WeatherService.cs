using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SnowOps.Api.Data;
using SnowOps.Api.Domain.Entities;

namespace SnowOps.Api.Services;

public sealed class WeatherService(AppDbContext db, HttpClient http, ILogger<WeatherService> logger)
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(30);

    public async Task<WeatherObservation?> GetLatestAsync(Guid districtId, CancellationToken ct = default)
    {
        var cached = await db.WeatherObservations
            .Where(w => w.DistrictId == districtId)
            .OrderByDescending(w => w.ObservedAt)
            .FirstOrDefaultAsync(ct);

        if (cached is not null && DateTimeOffset.UtcNow - cached.ObservedAt < CacheTtl)
            return cached;

        var district = await db.Districts.FindAsync([districtId], ct);
        if (district is null) return cached;

        try
        {
            var fresh = await FetchFromApiAsync(district, ct);
            if (fresh is not null)
            {
                db.WeatherObservations.Add(fresh);
                await db.SaveChangesAsync(ct);
                return fresh;
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to fetch weather for district {DistrictId}", districtId);
        }

        return cached;
    }

    private async Task<WeatherObservation?> FetchFromApiAsync(District district, CancellationToken ct)
    {
        var url = $"https://api.open-meteo.com/v1/forecast" +
                  $"?latitude={district.CenterLat.ToString(System.Globalization.CultureInfo.InvariantCulture)}" +
                  $"&longitude={district.CenterLon.ToString(System.Globalization.CultureInfo.InvariantCulture)}" +
                  $"&current=temperature_2m,precipitation,snowfall,weathercode" +
                  $"&hourly=precipitation,snowfall,weathercode" +
                  $"&past_hours=3&forecast_hours=0&timeformat=unixtime";

        var response = await http.GetAsync(url, ct);
        if (!response.IsSuccessStatusCode) return null;

        var json = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        decimal tempC = 0;
        decimal precip3h = 0;
        decimal snow3h = 0;
        bool snowOrIce = false;

        if (root.TryGetProperty("current", out var current))
        {
            if (current.TryGetProperty("temperature_2m", out var t))
                tempC = (decimal)t.GetDouble();
            if (current.TryGetProperty("precipitation", out var p))
                precip3h = (decimal)p.GetDouble();
            if (current.TryGetProperty("snowfall", out var s))
                snow3h = (decimal)s.GetDouble();
            if (current.TryGetProperty("weathercode", out var wc))
            {
                var code = wc.GetInt32();
                snowOrIce = (code >= 51 && code <= 77) || code is 85 or 86 || (code >= 95 && code <= 99);
            }
        }

        if (root.TryGetProperty("hourly", out var hourly))
        {
            if (hourly.TryGetProperty("snowfall", out var hSnow))
            {
                foreach (var sv in hSnow.EnumerateArray())
                    snow3h += (decimal)sv.GetDouble();
            }
            if (hourly.TryGetProperty("weathercode", out var hWc))
            {
                foreach (var wcv in hWc.EnumerateArray())
                {
                    var code = wcv.GetInt32();
                    if ((code >= 51 && code <= 77) || code is 85 or 86 || (code >= 95 && code <= 99))
                    {
                        snowOrIce = true;
                        break;
                    }
                }
            }
        }

        if (snow3h > 0 || (precip3h > 0 && tempC <= 2))
            snowOrIce = true;

        return new WeatherObservation
        {
            Id = Guid.NewGuid(),
            DistrictId = district.Id,
            ObservedAt = DateTimeOffset.UtcNow,
            TempC = tempC,
            Precipitation3hMm = precip3h,
            Snowfall3hMm = snow3h,
            SnowOrIceLast3h = snowOrIce,
            Raw = json,
            Source = "open-meteo"
        };
    }
}

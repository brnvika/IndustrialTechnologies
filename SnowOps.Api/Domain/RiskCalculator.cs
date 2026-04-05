namespace SnowOps.Api.Domain;

public static class RiskCalculator
{
    public static int Calculate(DefectType defectType, int coveragePercent, LocationType locationType, bool snowOrIceLast3h)
    {
        int t = defectType switch
        {
            DefectType.Ice => 3,
            DefectType.LooseSnow => 2,
            DefectType.SnowBankAtCrosswalk => 1,
            _ => 0
        };

        int s = coveragePercent switch
        {
            <= 30 => 1,
            <= 60 => 2,
            _ => 3
        };

        int l = locationType switch
        {
            LocationType.SchoolClinic => 2,
            LocationType.BusStop => 1,
            _ => 0
        };

        int w = snowOrIceLast3h ? 1 : 0;

        return (t * s) + l + w;
    }
}

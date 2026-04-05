namespace SnowOps.Api.Domain;

public static class RiskCalculator
{
    private static readonly Dictionary<string, int> TypeWeight = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Гололёд"] = 3,
        ["Рыхлый снег или сугробы"] = 2,
        ["Снежный вал у перехода"] = 1,
        ["Норма"] = 0
    };

    private static readonly Dictionary<string, int> LocationWeight = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Обычный переход"] = 0,
        ["Остановка"] = 1,
        ["Школа/поликлиника"] = 2
    };

    public static int Compute(string type, int coveragePercent, string locationType, bool snowOrIceLast3h)
    {
        var t = TypeWeight.GetValueOrDefault(type, 0);
        var s = ComputeScale(coveragePercent);
        var l = LocationWeight.GetValueOrDefault(locationType, 0);
        var w = snowOrIceLast3h ? 1 : 0;
        return (t * s) + l + w;
    }

    private static int ComputeScale(int coveragePercent)
    {
        if (coveragePercent <= 30)
        {
            return 1;
        }

        if (coveragePercent <= 60)
        {
            return 2;
        }

        return 3;
    }
}

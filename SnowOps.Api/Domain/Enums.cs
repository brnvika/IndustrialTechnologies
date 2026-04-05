namespace SnowOps.Api.Domain;

public enum DefectType : short
{
    Normal = 0,
    SnowBankAtCrosswalk = 1,
    LooseSnow = 2,
    Ice = 3
}

public enum LocationType : short
{
    RegularCrossing = 0,
    BusStop = 1,
    SchoolClinic = 2
}

public enum DefectStatus : short
{
    Found = 0,
    InProgress = 1,
    Fixed = 2
}

public enum PhotoKind : short
{
    Before = 0,
    After = 1
}

public enum EventType : short
{
    Created = 0,
    TakenInWork = 1,
    Fixed = 2,
    PhotoAdded = 3
}

public static class EnumDisplayNames
{
    public static string ToDisplayName(this DefectType t) => t switch
    {
        DefectType.Ice => "Гололёд",
        DefectType.LooseSnow => "Рыхлый снег/сугробы",
        DefectType.SnowBankAtCrosswalk => "Снежный вал у перехода",
        _ => "Норма"
    };

    public static string ToDisplayName(this LocationType l) => l switch
    {
        LocationType.SchoolClinic => "Школа/поликлиника",
        LocationType.BusStop => "Остановка",
        _ => "Обычный переход"
    };

    public static string ToDisplayName(this DefectStatus s) => s switch
    {
        DefectStatus.InProgress => "InProgress",
        DefectStatus.Fixed => "Fixed",
        _ => "Found"
    };
}

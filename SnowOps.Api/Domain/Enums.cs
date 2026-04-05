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

namespace SnowOps.Api.Domain.Entities;

public sealed class Defect
{
    public Guid Id { get; set; }
    public long PublicId { get; set; }
    public Guid DistrictId { get; set; }
    public double Lat { get; set; }
    public double Lon { get; set; }
    public string? AreaCode { get; set; }
    public string? AreaName { get; set; }
    public DefectType DefectType { get; set; }
    public short CoveragePercent { get; set; }
    public LocationType LocationType { get; set; }
    public DefectStatus Status { get; set; }
    public int RiskR { get; set; }
    public Guid? OwnerUserId { get; set; }
    public DateTimeOffset FoundAt { get; set; }
    public DateTimeOffset? FixedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public District District { get; set; } = null!;
    public AppUser? Owner { get; set; }
    public ICollection<DefectEvent> Events { get; set; } = new List<DefectEvent>();
    public ICollection<DefectPhoto> Photos { get; set; } = new List<DefectPhoto>();
    public DefectFixProof? FixProof { get; set; }
}

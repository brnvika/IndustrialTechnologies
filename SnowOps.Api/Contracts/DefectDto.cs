namespace SnowOps.Api.Contracts;

public sealed class DefectDto
{
    public Guid Id { get; set; }
    public long PublicId { get; set; }
    public Guid DistrictId { get; set; }
    public string? DistrictName { get; set; }
    public double Lat { get; set; }
    public double Lon { get; set; }
    public string? AreaCode { get; set; }
    public string? AreaName { get; set; }
    public int DefectType { get; set; }
    public string DefectTypeName { get; set; } = string.Empty;
    public int CoveragePercent { get; set; }
    public int LocationType { get; set; }
    public string LocationTypeName { get; set; } = string.Empty;
    public int Status { get; set; }
    public string StatusName { get; set; } = string.Empty;
    public int RiskR { get; set; }
    public Guid? OwnerUserId { get; set; }
    public string? OwnerLogin { get; set; }
    public DateTimeOffset FoundAt { get; set; }
    public DateTimeOffset? FixedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public List<DefectPhotoDto>? Photos { get; set; }
    public List<DefectEventDto>? Events { get; set; }
    public DefectFixProofDto? FixProof { get; set; }
}

public sealed class DefectPhotoDto
{
    public Guid Id { get; set; }
    public int Kind { get; set; }
    public string? Url { get; set; }
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public DateTimeOffset UploadedAt { get; set; }
    public string? UploadedByLogin { get; set; }
}

public sealed class DefectEventDto
{
    public Guid Id { get; set; }
    public int EventType { get; set; }
    public int? FromStatus { get; set; }
    public int? ToStatus { get; set; }
    public string? ActorLogin { get; set; }
    public string? Comment { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class DefectFixProofDto
{
    public string Comment { get; set; } = string.Empty;
    public string? ConfirmedByLogin { get; set; }
    public DateTimeOffset ConfirmedAt { get; set; }
    public int AfterPhotosCount { get; set; }
}

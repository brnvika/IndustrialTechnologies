namespace SnowOps.Api.Domain.Entities;

public sealed class DefectPhoto
{
    public Guid Id { get; set; }
    public Guid DefectId { get; set; }
    public PhotoKind Kind { get; set; }
    public string StorageProvider { get; set; } = "local";
    public string ObjectKey { get; set; } = string.Empty;
    public string? Url { get; set; }
    public string ContentType { get; set; } = "image/jpeg";
    public long SizeBytes { get; set; }
    public byte[]? Sha256 { get; set; }
    public Guid UploadedByUserId { get; set; }
    public DateTimeOffset UploadedAt { get; set; }

    public Defect Defect { get; set; } = null!;
    public AppUser UploadedBy { get; set; } = null!;
}

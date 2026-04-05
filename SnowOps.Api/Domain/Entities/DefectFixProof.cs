namespace SnowOps.Api.Domain.Entities;

public sealed class DefectFixProof
{
    public Guid DefectId { get; set; }
    public string Comment { get; set; } = string.Empty;
    public Guid ConfirmedByUserId { get; set; }
    public DateTimeOffset ConfirmedAt { get; set; }
    public int AfterPhotosCount { get; set; }

    public Defect Defect { get; set; } = null!;
    public AppUser ConfirmedBy { get; set; } = null!;
}

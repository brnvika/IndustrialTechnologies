namespace SnowOps.Api.Domain.Entities;

public sealed class DefectEvent
{
    public Guid Id { get; set; }
    public Guid DefectId { get; set; }
    public EventType EventType { get; set; }
    public DefectStatus? FromStatus { get; set; }
    public DefectStatus? ToStatus { get; set; }
    public Guid ActorUserId { get; set; }
    public string? Comment { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public Defect Defect { get; set; } = null!;
    public AppUser Actor { get; set; } = null!;
}

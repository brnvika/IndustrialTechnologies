namespace SnowOps.Api.Contracts;

public sealed class UserProfileDto
{
    public UserDto User { get; set; } = new();
    public int OwnedDefectsCount { get; set; }
    public int InProgressDefectsCount { get; set; }
    public int FixedDefectsCount { get; set; }
    public int CreatedEventsCount { get; set; }
    public DateTimeOffset? LastTakenAt { get; set; }
    public DateTimeOffset? LastFixedAt { get; set; }
}
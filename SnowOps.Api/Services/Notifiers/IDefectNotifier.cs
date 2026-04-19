namespace SnowOps.Api.Services.Notifiers;

public interface IDefectNotifier
{
    Task NotifyAsync(string defectType, string address, byte[] imageBytes, CancellationToken cancellationToken = default);
}
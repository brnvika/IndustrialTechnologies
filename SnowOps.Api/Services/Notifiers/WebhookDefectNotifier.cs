using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace SnowOps.Api.Services.Notifiers;

public class WebhookDefectNotifier : IDefectNotifier
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<WebhookDefectNotifier> _logger;
    private readonly string _webhookUrl;

    public WebhookDefectNotifier(HttpClient httpClient, IConfiguration configuration, ILogger<WebhookDefectNotifier> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _webhookUrl = configuration["Notifications:Webhook:Url"] ?? string.Empty;
    }

    public async Task NotifyAsync(string defectType, string address, byte[] imageBytes, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_webhookUrl))
        {
            _logger.LogWarning("Webhook URL is not configured. Skipping webhook notification.");
            return;
        }

        try
        {
            var payload = new
            {
                Type = "defect_alert",
                Timestamp = DateTimeOffset.UtcNow,
                Defect = defectType,
                Address = address,
                ImageBase64 = Convert.ToBase64String(imageBytes)
            };

            var content = new StringContent(JsonSerializer.Serialize(payload));
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

            var response = await _httpClient.PostAsync(_webhookUrl, content, cancellationToken);
            response.EnsureSuccessStatusCode();

            _logger.LogInformation("Webhook alert sent for defect {DefectType} at {Address}", defectType, address);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send webhook notification for {DefectType}", defectType);
        }
    }
}
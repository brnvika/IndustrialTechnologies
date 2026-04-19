using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SnowOps.Api.Domain;

namespace SnowOps.Api.Services.Notifiers;

public class TelegramInteractionWorker : BackgroundService, IDefectNotifier
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<TelegramInteractionWorker> _logger;
    private readonly RoadCoverVisionService _visionService;
    private readonly string _botToken;
    private readonly string _chatId;

    private int _lastUpdateId = 0;

    // В памяти храним связку (ChatId -> ID Заявки в работе)
    private readonly ConcurrentDictionary<string, string> _activeWork = new();

    public TelegramInteractionWorker(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<TelegramInteractionWorker> logger,
        RoadCoverVisionService visionService) // Singleton сервис ИИ
    {
        _httpClient = httpClientFactory.CreateClient();
        _logger = logger;
        _visionService = visionService;
        _botToken = configuration["Notifications:Telegram:BotToken"] ?? string.Empty;
        _chatId = configuration["Notifications:Telegram:ChatId"] ?? string.Empty;
    }

    /// <summary>
    /// Этот метод вызывает Program.cs при обнаружении дефекта (Если дорога не чистая)
    /// </summary>
    public async Task NotifyAsync(string defectType, string address, byte[] imageBytes, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_botToken) || string.IsNullOrWhiteSpace(_chatId)) return;

        var incidentId = Guid.NewGuid().ToString("N").Substring(0, 8); // Уникальный ID заявки

        var url = $"https://api.telegram.org/bot{_botToken}/sendPhoto";
        var text = $@"🚨 <b>Обнаружен дефект!</b>
<b>Тип:</b> {defectType}
📍 <b>Адрес:</b> {address}";

        var inlineKeyboard = new
        {
            inline_keyboard = new[]
            {
                new[]
                {
                    new { text = "✅ Принять в работу", callback_data = $"acc_{incidentId}" },
                    new { text = "❌ Отклонить", callback_data = $"rej_{incidentId}" }
                }
            }
        };

        using var content = new MultipartFormDataContent();
        using var fileStream = new MemoryStream(imageBytes);
        content.Add(new StringContent(_chatId), "chat_id");
        content.Add(new StringContent(text), "caption");
        content.Add(new StringContent("HTML"), "parse_mode");
        content.Add(new StringContent(JsonSerializer.Serialize(inlineKeyboard)), "reply_markup");

        var imageContent = new StreamContent(fileStream);
        content.Add(imageContent, "photo", "defect.jpg");

        await _httpClient.PostAsync(url, content, cancellationToken);
    }

    /// <summary>
    /// Фоновый процесс, который "слушает" нажатия кнопок и новые фото в Телеграмме
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(_botToken)) return;

        _logger.LogInformation("Telegram Bot Worker started. Listening for updates...");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var updates = await GetUpdatesAsync(stoppingToken);
                foreach (var update in updates.EnumerateArray())
                {
                    _lastUpdateId = update.GetProperty("update_id").GetInt32();

                    if (update.TryGetProperty("callback_query", out var callbackQuery))
                    {
                        await HandleCallbackQuery(callbackQuery, stoppingToken);
                    }
                    else if (update.TryGetProperty("message", out var message))
                    {
                        await HandleMessage(message, stoppingToken);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error polling Telegram updates");
                await Task.Delay(3000, stoppingToken);
            }
            await Task.Delay(1000, stoppingToken); // Пауза между опросами
        }
    }

    private async Task<JsonElement> GetUpdatesAsync(CancellationToken token)
    {
        var url = $"https://api.telegram.org/bot{_botToken}/getUpdates?offset={_lastUpdateId + 1}&timeout=5";
        var json = await _httpClient.GetStringAsync(url, token);
        var doc = JsonDocument.Parse(json);
        
        if (doc.RootElement.TryGetProperty("result", out var result) && result.ValueKind == JsonValueKind.Array)
        {
            return result;
        }
        
        return JsonDocument.Parse("[]").RootElement;
    }

    private async Task HandleCallbackQuery(JsonElement callbackQuery, CancellationToken token)
    {
        var data = callbackQuery.GetProperty("data").GetString();
        var message = callbackQuery.GetProperty("message");
        var chatId = message.GetProperty("chat").GetProperty("id").GetInt64().ToString();
        var callbackId = callbackQuery.GetProperty("id").GetString();
        var messageId = message.GetProperty("message_id").GetInt64();

        if (data != null && data.StartsWith("acc_"))
        {
            var id = data.Substring(4);
            _activeWork[chatId] = id;
            await SendTextMessageWithFinishOption(chatId, $"✅ Заявка <b>#{id}</b> принята в работу.\n\nСотрудник направлен на устранение дефекта. Как только работа будет завершена, нажмите кнопку ниже для автоматического контроля через уличные камеры.", id, token);
        }
        else if (data != null && data.StartsWith("finish_"))
        {
            var id = data.Substring(7);
            await SendTextMessage(chatId, $"📡 <b>Запрос к камерам ЕЦХД...</b>\n<i>(Режим симуляции аппаратно-программного комплекса: пожалуйста, отправьте фотографию завершенной уборки в чат вручную, чтобы ИИ смог ее проанализировать)</i>", token);
        }
        else if (data != null && data.StartsWith("rej_"))
        {
            var id = data.Substring(4);
            _activeWork.TryRemove(chatId, out _);
            await SendTextMessage(chatId, $"❌ Заявка <b>#{id}</b> отклонена.", token);
        }

        // Подтверждаем клик (чтобы часики в кнопке ТГ исчезли)
        await _httpClient.GetAsync($"https://api.telegram.org/bot{_botToken}/answerCallbackQuery?callback_query_id={callbackId}", token);
        
        // Убираем кнопки из прошлого сообщения
        await RemoveInlineKeyboard(chatId, messageId, token);
    }

    private async Task HandleMessage(JsonElement message, CancellationToken token)
    {
        var chatId = message.GetProperty("chat").GetProperty("id").GetInt64().ToString();

        // Проверяем, есть ли фотографии
        if (message.TryGetProperty("photo", out var photoArray) && photoArray.GetArrayLength() > 0)
        {
            if (!_activeWork.TryGetValue(chatId, out var incidentId))
            {
                await SendTextMessage(chatId, "У вас нет активных заявок, ожидающих фотоотчета. Сначала нужно принять заявку в работу по кнопке выше.", token);
                return;
            }

            await SendTextMessage(chatId, "⏳ Получено фотоотчет. Идет анализ покрытия нейросетью...", token);

            // Берем самую большую фотографию
            var photos = photoArray.EnumerateArray().ToList();
            var fileId = photos.Last().GetProperty("file_id").GetString();

            // Скачиваем файл из ТГ
            var fileInfoUrl = $"https://api.telegram.org/bot{_botToken}/getFile?file_id={fileId}";
            var fileInfoJson = await _httpClient.GetStringAsync(fileInfoUrl, token);
            var filePath = JsonDocument.Parse(fileInfoJson).RootElement.GetProperty("result").GetProperty("file_path").GetString();
            
            var downloadUrl = $"https://api.telegram.org/file/bot{_botToken}/{filePath}";
            var imageBytes = await _httpClient.GetByteArrayAsync(downloadUrl, token);

            // Прогоняем через модель
            using var ms = new MemoryStream(imageBytes);
            var result = await _visionService.AnalyzeAsync(ms, token);

            if (result.Label == RoadCoverLabel.CleanRoad)
            {
                await SendTextMessage(chatId, $"✅ <b>Вердикт контроля качества:</b> Дорога чистая! Заявка #{incidentId} успешно закрыта.", token);
                _activeWork.TryRemove(chatId, out _); // Снимаем задачу из активных
            }
            else
            {
                var labelName = result.Label switch
                {
                    RoadCoverLabel.Ice => "Гололед",
                    RoadCoverLabel.LooseSnow => "Рыхлый снег",
                    RoadCoverLabel.Snowdrift => "Сугробы",
                    RoadCoverLabel.SnowBankAtCrosswalk => "Снежный вал у перехода",
                    _ => "Неопределено"
                };
                await SendTextMessageWithRejectOption(chatId, $"❌ <b>Вердикт контроля качества:</b> Дефект остался ({labelName}, Уверенность: {result.Confidence:P0}).\n\nНеобходимо доработать участок и прикрепить новое фото, либо отклонить заявку.", incidentId, token);
            }
        }
    }

    private async Task SendTextMessage(string chatId, string text, CancellationToken token)
    {
        var payload = new { chat_id = chatId, text = text, parse_mode = "HTML" };
        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        await _httpClient.PostAsync($"https://api.telegram.org/bot{_botToken}/sendMessage", content, token);
    }

    private async Task SendTextMessageWithRejectOption(string chatId, string text, string incidentId, CancellationToken token)
    {
        var inlineKeyboard = new
        {
            inline_keyboard = new[]
            {
                new[] { new { text = "🏁 Завершить работу (Повтор)", callback_data = $"finish_{incidentId}" } },
                new[] { new { text = "❌ Отклонить", callback_data = $"rej_{incidentId}" } }
            }
        };

        var payload = new 
        { 
            chat_id = chatId, 
            text = text, 
            parse_mode = "HTML",
            reply_markup = inlineKeyboard
        };
        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        await _httpClient.PostAsync($"https://api.telegram.org/bot{_botToken}/sendMessage", content, token);
    }
    
    private async Task SendTextMessageWithFinishOption(string chatId, string text, string incidentId, CancellationToken token)
    {
        var inlineKeyboard = new
        {
            inline_keyboard = new[]
            {
                new[] { new { text = "🏁 Завершить работу и проверить камеры", callback_data = $"finish_{incidentId}" } }
            }
        };

        var payload = new 
        { 
            chat_id = chatId, 
            text = text, 
            parse_mode = "HTML",
            reply_markup = inlineKeyboard
        };
        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        await _httpClient.PostAsync($"https://api.telegram.org/bot{_botToken}/sendMessage", content, token);
    }
    
    private async Task RemoveInlineKeyboard(string chatId, long messageId, CancellationToken token)
    {
        var payload = new { chat_id = chatId, message_id = messageId, reply_markup = new { inline_keyboard = Array.Empty<object>() } };
        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        await _httpClient.PostAsync($"https://api.telegram.org/bot{_botToken}/editMessageReplyMarkup", content, token);
    }
}
using SnowOps.Api.Contracts;
using SnowOps.Api.Data;
using SnowOps.Api.Domain;
using SnowOps.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddSingleton<DbInitializer>();
builder.Services.AddSingleton<SnowRepository>();
builder.Services.AddSingleton<RoadCoverVisionService>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("frontend", policy =>
    {
        policy.AllowAnyHeader()
            .AllowAnyMethod()
            .AllowAnyOrigin();
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors("frontend");

await app.Services.GetRequiredService<DbInitializer>().InitializeAsync();

app.MapGet("/api/health", () => Results.Ok(new { status = "ok", service = "SnowOps.Api" }));

app.MapGet("/api/zones", async (SnowRepository repository) =>
{
    var zones = await repository.GetZonesAsync();
    return Results.Ok(zones);
});

app.MapPost("/api/zones", async (CreateZoneRequest request, SnowRepository repository) =>
{
    if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Name))
    {
        return Results.BadRequest(new { error = "Code и Name обязательны" });
    }

    var zone = await repository.CreateZoneAsync(request);
    return Results.Created($"/api/zones/{zone.Id}", zone);
});

app.MapGet("/api/tasks", async (
    bool? snowOrIce,
    string? status,
    string? owner,
    string? type,
    int? minRisk,
    int? maxRisk,
    SnowRepository repository) =>
{
    var tasks = await repository.GetTasksAsync(snowOrIce ?? false, status, owner, type, minRisk, maxRisk);
    return Results.Ok(tasks);
});

app.MapGet("/api/tasks/{id:long}", async (long id, bool? snowOrIce, SnowRepository repository) =>
{
    var task = await repository.GetTaskByIdAsync(id, snowOrIce ?? false);
    return task is null ? Results.NotFound() : Results.Ok(task);
});

app.MapPost("/api/tasks", async (CreateTaskRequest request, string? createdBy, bool? snowOrIce, SnowRepository repository) =>
{
    if (request.ZoneId <= 0 || string.IsNullOrWhiteSpace(request.Type))
    {
        return Results.BadRequest(new { error = "ZoneId и Type обязательны" });
    }

    if (request.CoveragePercent < 0 || request.CoveragePercent > 100)
    {
        return Results.BadRequest(new { error = "CoveragePercent должен быть в диапазоне 0..100" });
    }

    var task = await repository.CreateTaskAsync(request, createdBy ?? "dispatcher", snowOrIce ?? false);
    return Results.Created($"/api/tasks/{task.Id}", task);
});

app.MapPost("/api/tasks/{id:long}/take", async (long id, TakeTaskRequest request, bool? snowOrIce, SnowRepository repository) =>
{
    if (string.IsNullOrWhiteSpace(request.Employee))
    {
        return Results.BadRequest(new { error = "Employee обязателен" });
    }

    var task = await repository.TakeTaskInWorkAsync(id, request.Employee, snowOrIce ?? false);
    return task is null
        ? Results.Conflict(new { error = "Задача не найдена или уже не в статусе found" })
        : Results.Ok(task);
});

app.MapPost("/api/tasks/{id:long}/fix", async (long id, FixTaskRequest request, bool? snowOrIce, SnowRepository repository) =>
{
    if (string.IsNullOrWhiteSpace(request.Employee) || string.IsNullOrWhiteSpace(request.Comment))
    {
        return Results.BadRequest(new { error = "Employee и Comment обязательны" });
    }

    var task = await repository.FixTaskAsync(id, request, snowOrIce ?? false);
    return task is null
        ? Results.Conflict(new { error = "Задача не найдена или уже не в статусе work" })
        : Results.Ok(task);
});

app.MapGet("/api/work-reports", async (int? hours, SnowRepository repository) =>
{
    var reports = await repository.GetWorkReportsAsync(hours ?? 24);
    return Results.Ok(reports);
});

app.MapGet("/api/reports/summary", async (int? hours, bool? snowOrIce, SnowRepository repository) =>
{
    var summary = await repository.GetSummaryAsync(hours ?? 8, snowOrIce ?? false);
    return Results.Ok(summary);
});

app.MapPost("/api/vision/analyze", async (IFormFile photo, RoadCoverVisionService visionService, CancellationToken cancellationToken) =>
{
    if (photo is null || photo.Length == 0)
    {
        return Results.BadRequest(new { error = "Photo is required" });
    }

    await using var stream = photo.OpenReadStream();
    var result = await visionService.AnalyzeAsync(stream, cancellationToken);

    return Results.Ok(new VisionAnalyzeResponse
    {
        Label = result.Label switch
        {
            RoadCoverLabel.Ice => "Гололёд",
            RoadCoverLabel.LooseSnow => "Рыхлый снег",
            RoadCoverLabel.Snowdrift => "Сугробы",
            RoadCoverLabel.SnowBankAtCrosswalk => "Снежный вал у перехода",
            _ => "Неопределено"
        },
        Confidence = result.Confidence,
        IceScore = result.IceScore,
        LooseSnowScore = result.LooseSnowScore,
        SnowdriftScore = result.SnowdriftScore,
        SnowBankAtCrosswalkScore = result.SnowBankAtCrosswalkScore,
        Width = result.Width,
        Height = result.Height,
        Features = result.Features
    });
})
.Accepts<IFormFile>("multipart/form-data");

app.Run();

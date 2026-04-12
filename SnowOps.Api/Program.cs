using SnowOps.Api.Contracts;
using SnowOps.Api.Domain;
using SnowOps.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
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

app.MapGet("/", async context =>
{
    context.Response.ContentType = "text/html; charset=utf-8";
    var htmlPath = Path.Combine(app.Environment.ContentRootPath, "..", "index_Version4.html");
    await context.Response.SendFileAsync(Path.GetFullPath(htmlPath));
});

app.MapGet("/api/health", () => Results.Ok(new { status = "ok", service = "SnowOps.Api" }));

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
        Engine = result.Engine,
        Label = result.Label switch
        {
            RoadCoverLabel.Ice => "Гололед",
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
.Accepts<IFormFile>("multipart/form-data")
.DisableAntiforgery();

app.Run();

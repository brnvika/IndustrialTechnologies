namespace SnowOps.Api.Services;

public sealed class VisionModelOptions
{
    public string ModelPath { get; set; } = "models/roadcover.onnx";
    public string? InputName { get; set; }
    public string? OutputName { get; set; }
    public int ImageSize { get; set; } = 224;
    public List<string> Labels { get; set; } = new()
    {
        "clean_road",
        "ice",
        "loose_snow",
        "snowbank_crosswalk",
        "snowdrift"
    };

    public List<float> Mean { get; set; } = new() { 0.485f, 0.456f, 0.406f };
    public List<float> Std { get; set; } = new() { 0.229f, 0.224f, 0.225f };
}

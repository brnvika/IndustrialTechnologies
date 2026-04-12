namespace SnowOps.Api.Contracts;

public sealed class VisionAnalyzeResponse
{
    public string Engine { get; set; } = "heuristic";
    public string Label { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public double IceScore { get; set; }
    public double LooseSnowScore { get; set; }
    public double SnowdriftScore { get; set; }
    public double SnowBankAtCrosswalkScore { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public Dictionary<string, double> Features { get; set; } = new();
}

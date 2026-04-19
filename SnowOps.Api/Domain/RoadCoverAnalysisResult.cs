namespace SnowOps.Api.Domain;

public sealed class RoadCoverAnalysisResult
{
    public string Engine { get; set; } = "heuristic";
    public RoadCoverLabel Label { get; set; }
    public double Confidence { get; set; }
    public double IceScore { get; set; }
    public double LooseSnowScore { get; set; }
    public double SnowdriftScore { get; set; }
    public double SnowBankAtCrosswalkScore { get; set; }
    public double CleanRoadScore { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public Dictionary<string, double> Features { get; set; } = new();
}

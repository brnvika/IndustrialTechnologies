using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SnowOps.Api.Domain;

namespace SnowOps.Api.Services;

public sealed class RoadCoverVisionService : IDisposable
{
    private readonly VisionModelOptions _options;
    private readonly InferenceSession? _session;

    public RoadCoverVisionService(IConfiguration configuration, IWebHostEnvironment environment)
    {
        _options = configuration.GetSection("VisionModel").Get<VisionModelOptions>() ?? new VisionModelOptions();

        var modelPath = ResolveModelPath(environment.ContentRootPath, _options.ModelPath);
        if (!File.Exists(modelPath))
        {
            return;
        }

        try
        {
            _session = new InferenceSession(modelPath);
        }
        catch
        {
            _session = null;
        }
    }

    public async Task<RoadCoverAnalysisResult> AnalyzeAsync(Stream imageStream, CancellationToken cancellationToken = default)
    {
        using var image = await Image.LoadAsync<Rgba32>(imageStream, cancellationToken);

        if (_session is not null && TryAnalyzeWithOnnx(image, out var onnxResult))
        {
            return onnxResult;
        }

        return AnalyzeWithHeuristics(image);
    }

    public void Dispose()
    {
        _session?.Dispose();
    }

    private bool TryAnalyzeWithOnnx(Image<Rgba32> source, out RoadCoverAnalysisResult result)
    {
        result = default!;

        try
        {
            if (_session is null)
            {
                return false;
            }

            var inputName = string.IsNullOrWhiteSpace(_options.InputName)
                ? _session.InputMetadata.Keys.First()
                : _options.InputName;

            var outputName = string.IsNullOrWhiteSpace(_options.OutputName)
                ? _session.OutputMetadata.Keys.First()
                : _options.OutputName;

            using var resized = source.Clone(x => x.Resize(_options.ImageSize, _options.ImageSize));

            var inputTensor = new DenseTensor<float>(new[] { 1, 3, _options.ImageSize, _options.ImageSize });
            for (var y = 0; y < _options.ImageSize; y++)
            {
                for (var x = 0; x < _options.ImageSize; x++)
                {
                    var pixel = resized[x, y];
                    inputTensor[0, 0, y, x] = pixel.R / 255f;
                    inputTensor[0, 1, y, x] = pixel.G / 255f;
                    inputTensor[0, 2, y, x] = pixel.B / 255f;
                }
            }

            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor(inputName, inputTensor)
            };

            using var outputs = _session.Run(inputs);
            var outputValue = outputs.FirstOrDefault(x => x.Name == outputName) ?? outputs.First();
            var rawOutput = ExtractLogits(outputValue.AsTensor<float>());
            var probabilities = ToProbabilities(rawOutput);

            var labels = _options.Labels.Count == probabilities.Length
                ? _options.Labels
                : new List<string> { "clean_road", "ice", "loose_snow", "snowbank_crosswalk", "snowdrift" };

            var scoreMap = new Dictionary<RoadCoverLabel, double>
            {
                [RoadCoverLabel.Ice] = 0,
                [RoadCoverLabel.LooseSnow] = 0,
                [RoadCoverLabel.Snowdrift] = 0,
                [RoadCoverLabel.SnowBankAtCrosswalk] = 0,
                [RoadCoverLabel.CleanRoad] = 0
            };

            for (var i = 0; i < Math.Min(labels.Count, probabilities.Length); i++)
            {
                var parsed = ParseLabel(labels[i]);
                if (parsed is not null)
                {
                    scoreMap[parsed.Value] = probabilities[i];
                }
            }

            // Resolve common ambiguity between snowdrift and snowbank near crosswalk.
            var best = SelectBestLabel(scoreMap);
            result = new RoadCoverAnalysisResult
            {
                Engine = "onnx",
                Label = best.Key,
                Confidence = Math.Round(best.Value, 3),
                IceScore = Math.Round(scoreMap[RoadCoverLabel.Ice], 3),
                LooseSnowScore = Math.Round(scoreMap[RoadCoverLabel.LooseSnow], 3),
                SnowdriftScore = Math.Round(scoreMap[RoadCoverLabel.Snowdrift], 3),
                SnowBankAtCrosswalkScore = Math.Round(scoreMap[RoadCoverLabel.SnowBankAtCrosswalk], 3),
                CleanRoadScore = Math.Round(scoreMap[RoadCoverLabel.CleanRoad], 3),
                Width = source.Width,
                Height = source.Height,
                Features = new Dictionary<string, double>
                {
                    ["modelInputSize"] = _options.ImageSize,
                    ["classCount"] = probabilities.Length
                }
            };

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static float[] ExtractLogits(Tensor<float> tensor)
    {
        var raw = tensor.ToArray();
        if (tensor.Rank == 1)
        {
            return raw;
        }

        var classes = tensor.Dimensions[tensor.Rank - 1];
        if (classes <= 0 || raw.Length <= classes)
        {
            return raw;
        }

        return raw.Take(classes).ToArray();
    }

    private static double[] Softmax(float[] logits)
    {
        if (logits.Length == 0)
        {
            return Array.Empty<double>();
        }

        var max = logits.Max();
        var exps = logits.Select(x => Math.Exp(x - max)).ToArray();
        var sum = exps.Sum();

        if (sum <= 0)
        {
            return Enumerable.Repeat(0.0, logits.Length).ToArray();
        }

        return exps.Select(x => x / sum).ToArray();
    }

    private static double[] ToProbabilities(float[] values)
    {
        if (values.Length == 0)
        {
            return Array.Empty<double>();
        }

        var allInUnitRange = values.All(v => !float.IsNaN(v) && !float.IsInfinity(v) && v >= 0f && v <= 1f);
        var sum = values.Sum(v => (double)v);

        // YOLOv8 ONNX classifiers often output already-normalized probabilities.
        if (allInUnitRange && Math.Abs(sum - 1.0) <= 0.02)
        {
            return values.Select(v => (double)v).ToArray();
        }

        return Softmax(values);
    }

    private static KeyValuePair<RoadCoverLabel, double> SelectBestLabel(Dictionary<RoadCoverLabel, double> scoreMap)
    {
        var best = scoreMap.OrderByDescending(x => x.Value).First();

        var crosswalk = scoreMap[RoadCoverLabel.SnowBankAtCrosswalk];
        var snowdrift = scoreMap[RoadCoverLabel.Snowdrift];

        // Promote crosswalk when model is uncertain between these two visually similar classes.
        if (best.Key == RoadCoverLabel.Snowdrift && crosswalk >= 0.33 && crosswalk / Math.Max(snowdrift, 1e-6) >= 0.55)
        {
            return new KeyValuePair<RoadCoverLabel, double>(RoadCoverLabel.SnowBankAtCrosswalk, crosswalk);
        }

        return best;
    }

    private static RoadCoverLabel? ParseLabel(string value)
    {
        var normalized = value.Trim().ToLowerInvariant().Replace('-', '_').Replace(' ', '_');
        return normalized switch
        {
            "ice" => RoadCoverLabel.Ice,
            "loose_snow" => RoadCoverLabel.LooseSnow,
            "snowdrift" => RoadCoverLabel.Snowdrift,
            "snowbank_crosswalk" => RoadCoverLabel.SnowBankAtCrosswalk,
            "clean_road" => RoadCoverLabel.CleanRoad,
            "cleanroad" => RoadCoverLabel.CleanRoad,
            _ => null
        };
    }

    private static string ResolveModelPath(string contentRootPath, string modelPath)
    {
        if (Path.IsPathRooted(modelPath))
        {
            return modelPath;
        }

        return Path.GetFullPath(Path.Combine(contentRootPath, modelPath));
    }

    private static RoadCoverAnalysisResult AnalyzeWithHeuristics(Image<Rgba32> image)
    {
        NormalizeSize(image);

        var width = image.Width;
        var height = image.Height;
        var pixels = new Rgba32[width * height];
        image.CopyPixelDataTo(pixels);

        double brightnessSum = 0;
        double saturationSum = 0;
        double whitePixels = 0;
        double neutralBrightPixels = 0;
        double darkPixels = 0;
        double lowerThirdWhite = 0;
        double bottomQuarterWhite = 0;
        double bottomHorizontalBand = 0;

        var luminance = new double[pixels.Length];
        for (var i = 0; i < pixels.Length; i++)
        {
            var pixel = pixels[i];
            var (_, s, l) = ToHsl(pixel);
            luminance[i] = l;
            brightnessSum += l;
            saturationSum += s;

            if (l >= 0.82 && s <= 0.25)
            {
                whitePixels += 1;
            }

            if (l >= 0.68 && s <= 0.18)
            {
                neutralBrightPixels += 1;
            }

            if (l <= 0.26)
            {
                darkPixels += 1;
            }
        }

        var edgeDensity = ComputeEdgeDensity(luminance, width, height);
        var brightMask = BuildBrightMask(luminance, width, height, out lowerThirdWhite, out bottomQuarterWhite, out bottomHorizontalBand);
        var (brightComponentArea, brightComponentAspect, brightComponentCount) = ComputeLargestBrightComponent(brightMask, width, height);

        var total = pixels.Length;
        var brightness = brightnessSum / total;
        var saturation = saturationSum / total;
        var whiteRatio = whitePixels / total;
        var neutralBrightRatio = neutralBrightPixels / total;
        var darkRatio = darkPixels / total;
        var lowerThirdWhiteRatio = lowerThirdWhite / Math.Max(1, width * (height / 3.0));
        var bottomQuarterWhiteRatio = bottomQuarterWhite / Math.Max(1, width * (height / 4.0));
        var bottomHorizontalBandRatio = bottomHorizontalBand / Math.Max(1, width * Math.Max(1, height / 8.0));
        var largestComponentAreaRatio = brightComponentArea / total;
        var largestComponentAspect = brightComponentAspect;
        var componentCountNormalized = brightComponentCount / Math.Max(1, total / 1000.0);

        var iceScore = Clamp01(
            (0.40 * neutralBrightRatio) +
            (0.25 * (1.0 - saturation)) +
            (0.20 * (1.0 - edgeDensity)) +
            (0.15 * (1.0 - darkRatio))
        );

        var looseSnowScore = Clamp01(
            (0.45 * whiteRatio) +
            (0.20 * lowerThirdWhiteRatio) +
            (0.20 * edgeDensity) +
            (0.15 * (1.0 - Math.Abs(brightness - 0.78)))
        );

        var snowdriftScore = Clamp01(
            (0.40 * whiteRatio) +
            (0.30 * largestComponentAreaRatio) +
            (0.15 * (1.0 - Math.Abs(largestComponentAspect - 1.0))) +
            (0.15 * bottomQuarterWhiteRatio)
        );

        var snowBankScore = Clamp01(
            (0.35 * bottomQuarterWhiteRatio) +
            (0.30 * bottomHorizontalBandRatio) +
            (0.20 * largestComponentAreaRatio) +
            (0.15 * Clamp01((largestComponentAspect - 1.2) / 3.0))
        );

        var cleanRoadScore = Clamp01(
            (0.45 * darkRatio) +
            (0.25 * (1.0 - whiteRatio)) +
            (0.20 * (1.0 - bottomQuarterWhiteRatio)) +
            (0.10 * (1.0 - lowerThirdWhiteRatio))
        );

        var scores = new Dictionary<RoadCoverLabel, double>
        {
            [RoadCoverLabel.Ice] = iceScore,
            [RoadCoverLabel.LooseSnow] = looseSnowScore,
            [RoadCoverLabel.Snowdrift] = snowdriftScore,
            [RoadCoverLabel.SnowBankAtCrosswalk] = snowBankScore,
            [RoadCoverLabel.CleanRoad] = cleanRoadScore
        };

        var best = scores.OrderByDescending(x => x.Value).First();
        var second = scores.OrderByDescending(x => x.Value).Skip(1).First();
        var confidence = Clamp01(best.Value - second.Value + 0.50);

        return new RoadCoverAnalysisResult
        {
            Engine = "heuristic",
            Label = best.Key,
            Confidence = Math.Round(confidence, 3),
            IceScore = Math.Round(iceScore, 3),
            LooseSnowScore = Math.Round(looseSnowScore, 3),
            SnowdriftScore = Math.Round(snowdriftScore, 3),
            SnowBankAtCrosswalkScore = Math.Round(snowBankScore, 3),
            CleanRoadScore = Math.Round(cleanRoadScore, 3),
            Width = width,
            Height = height,
            Features = new Dictionary<string, double>
            {
                ["brightness"] = Math.Round(brightness, 3),
                ["saturation"] = Math.Round(saturation, 3),
                ["whiteRatio"] = Math.Round(whiteRatio, 3),
                ["neutralBrightRatio"] = Math.Round(neutralBrightRatio, 3),
                ["darkRatio"] = Math.Round(darkRatio, 3),
                ["edgeDensity"] = Math.Round(edgeDensity, 3),
                ["lowerThirdWhiteRatio"] = Math.Round(lowerThirdWhiteRatio, 3),
                ["bottomQuarterWhiteRatio"] = Math.Round(bottomQuarterWhiteRatio, 3),
                ["bottomHorizontalBandRatio"] = Math.Round(bottomHorizontalBandRatio, 3),
                ["largestBrightComponentAreaRatio"] = Math.Round(largestComponentAreaRatio, 3),
                ["largestBrightComponentAspect"] = Math.Round(largestComponentAspect, 3),
                ["brightComponentCountNormalized"] = Math.Round(componentCountNormalized, 3),
                ["cleanRoadScore"] = Math.Round(cleanRoadScore, 3)
            }
        };
    }

    private static void NormalizeSize(Image<Rgba32> image)
    {
        const int maxSide = 512;
        if (image.Width <= maxSide && image.Height <= maxSide)
        {
            return;
        }

        image.Mutate(x => x.Resize(new ResizeOptions
        {
            Mode = ResizeMode.Max,
            Size = new Size(maxSide, maxSide)
        }));
    }

    private static (double Hue, double Saturation, double Lightness) ToHsl(Rgba32 pixel)
    {
        var r = pixel.R / 255.0;
        var g = pixel.G / 255.0;
        var b = pixel.B / 255.0;

        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var l = (max + min) / 2.0;

        if (Math.Abs(max - min) < 0.0001)
        {
            return (0, 0, l);
        }

        var d = max - min;
        var s = l > 0.5 ? d / (2.0 - max - min) : d / (max + min);

        double h;
        if (Math.Abs(max - r) < 0.0001)
        {
            h = (g - b) / d + (g < b ? 6 : 0);
        }
        else if (Math.Abs(max - g) < 0.0001)
        {
            h = (b - r) / d + 2;
        }
        else
        {
            h = (r - g) / d + 4;
        }

        h /= 6;
        return (h, s, l);
    }

    private static double ComputeEdgeDensity(double[] luminance, int width, int height)
    {
        if (width < 3 || height < 3)
        {
            return 0;
        }

        var edgePixels = 0;
        var samples = 0;

        for (var y = 1; y < height - 1; y++)
        {
            for (var x = 1; x < width - 1; x++)
            {
                var idx = y * width + x;
                var gx = (-1 * luminance[idx - width - 1]) + (1 * luminance[idx - width + 1])
                    + (-2 * luminance[idx - 1]) + (2 * luminance[idx + 1])
                    + (-1 * luminance[idx + width - 1]) + (1 * luminance[idx + width + 1]);
                var gy = (-1 * luminance[idx - width - 1]) + (-2 * luminance[idx - width]) + (-1 * luminance[idx - width + 1])
                    + (1 * luminance[idx + width - 1]) + (2 * luminance[idx + width]) + (1 * luminance[idx + width + 1]);
                var magnitude = Math.Sqrt((gx * gx) + (gy * gy));
                if (magnitude > 0.30)
                {
                    edgePixels++;
                }

                samples++;
            }
        }

        return samples == 0 ? 0 : edgePixels / (double)samples;
    }

    private static bool[] BuildBrightMask(double[] luminance, int width, int height, out double lowerThirdWhite, out double bottomQuarterWhite, out double bottomHorizontalBand)
    {
        var mask = new bool[luminance.Length];
        lowerThirdWhite = 0;
        bottomQuarterWhite = 0;
        bottomHorizontalBand = 0;

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var idx = y * width + x;
                var bright = luminance[idx] >= 0.74;
                mask[idx] = bright;

                if (bright && y >= (height * 2 / 3))
                {
                    lowerThirdWhite++;
                }

                if (bright && y >= (height * 3 / 4))
                {
                    bottomQuarterWhite++;
                }

                if (bright && y >= (height * 3 / 4) && y < height - Math.Max(6, height / 12))
                {
                    bottomHorizontalBand++;
                }
            }
        }

        return mask;
    }

    private static (double Area, double Aspect, double Count) ComputeLargestBrightComponent(bool[] mask, int width, int height)
    {
        var visited = new bool[mask.Length];
        var directions = new (int Dx, int Dy)[]
        {
            (1, 0), (-1, 0), (0, 1), (0, -1)
        };

        double maxArea = 0;
        double maxAspect = 1;
        double count = 0;

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var startIdx = y * width + x;
                if (!mask[startIdx] || visited[startIdx])
                {
                    continue;
                }

                count++;
                var queue = new Queue<(int X, int Y)>();
                queue.Enqueue((x, y));
                visited[startIdx] = true;

                var minX = x;
                var maxX = x;
                var minY = y;
                var maxY = y;
                var area = 0;

                while (queue.Count > 0)
                {
                    var (cx, cy) = queue.Dequeue();
                    area++;
                    minX = Math.Min(minX, cx);
                    maxX = Math.Max(maxX, cx);
                    minY = Math.Min(minY, cy);
                    maxY = Math.Max(maxY, cy);

                    foreach (var (dx, dy) in directions)
                    {
                        var nx = cx + dx;
                        var ny = cy + dy;
                        if (nx < 0 || ny < 0 || nx >= width || ny >= height)
                        {
                            continue;
                        }

                        var nIdx = ny * width + nx;
                        if (!mask[nIdx] || visited[nIdx])
                        {
                            continue;
                        }

                        visited[nIdx] = true;
                        queue.Enqueue((nx, ny));
                    }
                }

                if (area > maxArea)
                {
                    maxArea = area;
                    var compWidth = Math.Max(1, maxX - minX + 1);
                    var compHeight = Math.Max(1, maxY - minY + 1);
                    maxAspect = compWidth / (double)compHeight;
                }
            }
        }

        return (maxArea, maxAspect, count);
    }

    private static double Clamp01(double value) => Math.Min(1.0, Math.Max(0.0, value));
}

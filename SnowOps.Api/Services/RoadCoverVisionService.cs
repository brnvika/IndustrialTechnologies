using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SnowOps.Api.Domain;

namespace SnowOps.Api.Services;

public sealed class RoadCoverVisionService
{
    public async Task<RoadCoverAnalysisResult> AnalyzeAsync(Stream imageStream, CancellationToken cancellationToken = default)
    {
        using var image = await Image.LoadAsync<Rgba32>(imageStream, cancellationToken);
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
        double edgeSum = 0;
        double lowerThirdWhite = 0;
        double bottomQuarterWhite = 0;
        double bottomHorizontalBand = 0;
        double brightComponentArea = 0;
        double brightComponentAspect = 0;
        double brightComponentCount = 0;

        var luminance = new double[pixels.Length];

        for (var i = 0; i < pixels.Length; i++)
        {
            var pixel = pixels[i];
            var (h, s, l) = ToHsl(pixel);
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

        edgeSum = ComputeEdgeDensity(luminance, width, height);
        var brightMask = BuildBrightMask(luminance, width, height, out lowerThirdWhite, out bottomQuarterWhite, out bottomHorizontalBand);
        (brightComponentArea, brightComponentAspect, brightComponentCount) = ComputeLargestBrightComponent(brightMask, width, height);

        var total = pixels.Length;
        var brightness = brightnessSum / total;
        var saturation = saturationSum / total;
        var whiteRatio = whitePixels / total;
        var neutralBrightRatio = neutralBrightPixels / total;
        var darkRatio = darkPixels / total;
        var edgeDensity = edgeSum;
        var lowerThirdWhiteRatio = lowerThirdWhite / Math.Max(1, width * (height / 3.0));
        var bottomQuarterWhiteRatio = bottomQuarterWhite / Math.Max(1, width * (height / 4.0));
        var bottomHorizontalBandRatio = bottomHorizontalBand / Math.Max(1, width * Math.Max(1, height / 8.0));
        var largestComponentAreaRatio = brightComponentArea / total;
        var largestComponentAspect = brightComponentAspect;
        var componentCountNormalized = brightComponentCount / Math.Max(1, total / 1000.0);

        // Heuristics are intentionally transparent and tunable.
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

        var scores = new Dictionary<RoadCoverLabel, double>
        {
            [RoadCoverLabel.Ice] = iceScore,
            [RoadCoverLabel.LooseSnow] = looseSnowScore,
            [RoadCoverLabel.Snowdrift] = snowdriftScore,
            [RoadCoverLabel.SnowBankAtCrosswalk] = snowBankScore
        };

        var best = scores.OrderByDescending(x => x.Value).First();
        var second = scores.OrderByDescending(x => x.Value).Skip(1).First();
        var confidence = Clamp01(best.Value - second.Value + 0.50);

        return new RoadCoverAnalysisResult
        {
            Label = best.Key,
            Confidence = Math.Round(confidence, 3),
            IceScore = Math.Round(iceScore, 3),
            LooseSnowScore = Math.Round(looseSnowScore, 3),
            SnowdriftScore = Math.Round(snowdriftScore, 3),
            SnowBankAtCrosswalkScore = Math.Round(snowBankScore, 3),
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
                ["brightComponentCountNormalized"] = Math.Round(componentCountNormalized, 3)
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

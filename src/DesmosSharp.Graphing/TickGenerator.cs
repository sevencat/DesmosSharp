using System.Globalization;

namespace DesmosSharp.Graphing;

public readonly record struct AxisTick(double Value, double Position, string Label, bool IsMajor);

public sealed class TickSet
{
    public IReadOnlyList<AxisTick> X { get; init; } = [];

    public IReadOnlyList<AxisTick> Y { get; init; } = [];
}

/// <summary>按像素密度生成 1/2/5×10^k 或 π 倍数的刻度。</summary>
public static class TickGenerator
{
    private const double TargetMajorPixels = 80;
    private const int MinorDivisions = 5;

    private static readonly double[] PiMultiples = [0.25, 0.5, 1, 2, 5, 10, 20, 50];

    public static TickSet Generate(
        GraphViewport viewport,
        double pixelWidth,
        double pixelHeight,
        bool usePiLabels = false)
    {
        if (!viewport.IsValid || pixelWidth <= 0 || pixelHeight <= 0)
        {
            return new TickSet();
        }

        var xStep = ChooseStep(viewport.Width / (pixelWidth / TargetMajorPixels), usePiLabels);
        var yStep = ChooseStep(viewport.Height / (pixelHeight / TargetMajorPixels), usePiLabels);

        return new TickSet
        {
            X = BuildAxis(
                viewport.XMin,
                viewport.XMax,
                xStep,
                value => (value - viewport.XMin) / viewport.Width * pixelWidth,
                usePiLabels),
            Y = BuildAxis(
                viewport.YMin,
                viewport.YMax,
                yStep,
                value => (viewport.YMax - value) / viewport.Height * pixelHeight,
                usePiLabels),
        };
    }

    private static List<AxisTick> BuildAxis(
        double min,
        double max,
        double majorStep,
        Func<double, double> toPixel,
        bool usePiLabels)
    {
        var ticks = new List<AxisTick>();
        var minorStep = majorStep / MinorDivisions;

        if (minorStep <= 0 || !double.IsFinite(minorStep))
        {
            return ticks;
        }

        var first = Math.Ceiling(min / minorStep) * minorStep;
        var count = (int)Math.Min((max - first) / minorStep + 1, 10_000);

        for (var i = 0; i < count; i++)
        {
            var value = first + i * minorStep;
            if (value < min - minorStep * 1e-6 || value > max + minorStep * 1e-6)
            {
                continue;
            }

            var isMajor = IsMultiple(value, majorStep);
            ticks.Add(new AxisTick(
                value,
                toPixel(value),
                isMajor ? FormatLabel(value, usePiLabels) : string.Empty,
                isMajor));
        }

        return ticks;
    }

    private static bool IsMultiple(double value, double step)
    {
        var ratio = value / step;
        return Math.Abs(ratio - Math.Round(ratio)) < 1e-6;
    }

    private static double ChooseStep(double rawStep, bool piMode)
    {
        if (!double.IsFinite(rawStep) || rawStep <= 0)
        {
            return 1;
        }

        if (piMode)
        {
            var units = rawStep / Math.PI;
            foreach (var multiple in PiMultiples)
            {
                if (units <= multiple)
                {
                    return multiple * Math.PI;
                }
            }
        }

        var exponent = Math.Floor(Math.Log10(rawStep));
        var fraction = rawStep / Math.Pow(10, exponent);

        var nice = fraction switch
        {
            <= 1 => 1,
            <= 2 => 2,
            <= 5 => 5,
            _ => 10,
        };

        return nice * Math.Pow(10, exponent);
    }

    private static string FormatLabel(double value, bool usePiLabels)
    {
        if (Math.Abs(value) < 1e-9)
        {
            return "0";
        }

        return usePiLabels ? FormatPiLabel(value) : FormatDecimalLabel(value);
    }

    private static string FormatDecimalLabel(double value)
    {
        var magnitude = Math.Abs(value);

        return magnitude is >= 1e6 or < 1e-4
            ? value.ToString("0.###e+0", CultureInfo.InvariantCulture)
            : value.ToString("0.#####", CultureInfo.InvariantCulture);
    }

    private static string FormatPiLabel(double value)
    {
        var quarters = (int)Math.Round(value / Math.PI * 4);
        if (quarters == 0)
        {
            return "0";
        }

        var denominator = 4;
        var divisor = GreatestCommonDivisor(Math.Abs(quarters), denominator);
        var numerator = quarters / divisor;
        denominator /= divisor;

        if (denominator == 1)
        {
            return numerator == 1 ? "π" : numerator == -1 ? "-π" : $"{numerator}π";
        }

        var prefix = numerator == 1 ? "π" : numerator == -1 ? "-π" : $"{numerator}π";
        return $"{prefix}/{denominator}";
    }

    private static int GreatestCommonDivisor(int left, int right)
    {
        while (right != 0)
        {
            var remainder = left % right;
            left = right;
            right = remainder;
        }

        return left == 0 ? 1 : left;
    }
}

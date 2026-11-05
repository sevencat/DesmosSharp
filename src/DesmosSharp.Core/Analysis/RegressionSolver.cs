namespace DesmosSharp.Core.Analysis;

public sealed class RegressionResult
{
    public required IReadOnlyList<double> Parameters { get; init; }

    /// <summary>残差平方和。</summary>
    public required double ResidualSumOfSquares { get; init; }

    public required bool Converged { get; init; }

    /// <summary>参与拟合的数据点数量。</summary>
    public required int SampleCount { get; init; }
}

/// <summary>
/// 最小二乘拟合：数值雅可比 + 高斯-牛顿迭代，可处理线性与常见非线性模型。
/// </summary>
public static class RegressionSolver
{
    private const int MaxIterations = 60;
    private const double StepTolerance = 1e-10;

    /// <summary>
    /// 求解使 <c>Σ (target - predict(parameters))²</c> 最小的参数。
    /// 返回 null 表示数据不足或方程奇异。
    /// </summary>
    public static RegressionResult? Solve(
        IReadOnlyList<double> target,
        IReadOnlyList<double> initial,
        Func<IReadOnlyList<double>, IReadOnlyList<double>> predict)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(initial);
        ArgumentNullException.ThrowIfNull(predict);

        var count = initial.Count;
        if (count == 0 || target.Count == 0)
        {
            return null;
        }

        var parameters = initial.ToArray();
        var valid = new bool[target.Count];
        var residual = new double[target.Count];
        var jacobian = new double[target.Count, count];
        var converged = false;
        var sampleCount = 0;

        for (var iteration = 0; iteration < MaxIterations; iteration++)
        {
            var prediction = predict(parameters);
            sampleCount = 0;

            for (var i = 0; i < target.Count; i++)
            {
                var predicted = i < prediction.Count ? prediction[i] : double.NaN;
                valid[i] = double.IsFinite(target[i]) && double.IsFinite(predicted);

                if (valid[i])
                {
                    residual[i] = target[i] - predicted;
                    sampleCount++;
                }
            }

            if (sampleCount < count)
            {
                return null;
            }

            FillJacobian();

            var normal = new double[count, count];
            var right = new double[count];

            for (var a = 0; a < count; a++)
            {
                for (var b = 0; b < count; b++)
                {
                    var sum = 0.0;

                    for (var i = 0; i < target.Count; i++)
                    {
                        if (valid[i])
                        {
                            sum += jacobian[i, a] * jacobian[i, b];
                        }
                    }

                    normal[a, b] = sum;
                }

                var rhs = 0.0;

                for (var i = 0; i < target.Count; i++)
                {
                    if (valid[i])
                    {
                        rhs += jacobian[i, a] * residual[i];
                    }
                }

                right[a] = rhs;
                normal[a, a] += 1e-12;
            }

            var delta = SolveLinear(normal, right);
            if (delta is null)
            {
                return null;
            }

            var stepSize = 0.0;
            var scale = 1.0;

            for (var j = 0; j < count; j++)
            {
                parameters[j] += delta[j];
                stepSize = Math.Max(stepSize, Math.Abs(delta[j]));
                scale = Math.Max(scale, Math.Abs(parameters[j]));
            }

            if (stepSize < StepTolerance * scale)
            {
                converged = true;
                break;
            }
        }

        var finalCost = Cost(parameters);

        return new RegressionResult
        {
            Parameters = parameters,
            ResidualSumOfSquares = finalCost,
            Converged = converged,
            SampleCount = sampleCount,
        };

        void FillJacobian()
        {
            var prediction = predict(parameters);

            for (var j = 0; j < count; j++)
            {
                var step = 1e-6 * (1 + Math.Abs(parameters[j]));
                var shifted = (double[])parameters.Clone();
                shifted[j] += step;
                var shiftedPrediction = predict(shifted);

                for (var i = 0; i < target.Count; i++)
                {
                    if (!valid[i])
                    {
                        jacobian[i, j] = 0;
                        continue;
                    }

                    var predicted = i < shiftedPrediction.Count ? shiftedPrediction[i] : double.NaN;
                    jacobian[i, j] = double.IsFinite(predicted) && i < prediction.Count
                        ? (predicted - prediction[i]) / step
                        : 0;
                }
            }
        }

        double Cost(double[] candidate)
        {
            var prediction = predict(candidate);
            var sum = 0.0;

            for (var i = 0; i < target.Count; i++)
            {
                var predicted = i < prediction.Count ? prediction[i] : double.NaN;

                if (double.IsFinite(target[i]) && double.IsFinite(predicted))
                {
                    var difference = target[i] - predicted;
                    sum += difference * difference;
                }
            }

            return sum;
        }
    }

    /// <summary>高斯消元求解线性方程组，奇异时返回 null。</summary>
    private static double[]? SolveLinear(double[,] matrix, double[] right)
    {
        var count = right.Length;
        var augmented = new double[count, count + 1];

        for (var row = 0; row < count; row++)
        {
            for (var column = 0; column < count; column++)
            {
                augmented[row, column] = matrix[row, column];
            }

            augmented[row, count] = right[row];
        }

        for (var pivot = 0; pivot < count; pivot++)
        {
            var best = pivot;

            for (var row = pivot + 1; row < count; row++)
            {
                if (Math.Abs(augmented[row, pivot]) > Math.Abs(augmented[best, pivot]))
                {
                    best = row;
                }
            }

            if (Math.Abs(augmented[best, pivot]) < 1e-14)
            {
                return null;
            }

            if (best != pivot)
            {
                for (var column = pivot; column <= count; column++)
                {
                    (augmented[pivot, column], augmented[best, column]) =
                        (augmented[best, column], augmented[pivot, column]);
                }
            }

            for (var row = pivot + 1; row < count; row++)
            {
                var factor = augmented[row, pivot] / augmented[pivot, pivot];
                if (factor == 0)
                {
                    continue;
                }

                for (var column = pivot; column <= count; column++)
                {
                    augmented[row, column] -= factor * augmented[pivot, column];
                }
            }
        }

        var solution = new double[count];

        for (var row = count - 1; row >= 0; row--)
        {
            var sum = augmented[row, count];

            for (var column = row + 1; column < count; column++)
            {
                sum -= augmented[row, column] * solution[column];
            }

            solution[row] = sum / augmented[row, row];
        }

        return solution;
    }
}

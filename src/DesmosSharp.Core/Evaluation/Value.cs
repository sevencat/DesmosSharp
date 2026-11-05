using System.Globalization;

namespace DesmosSharp.Core.Evaluation;

public enum ValueKind
{
    Undefined = 0,
    Number,
    Boolean,
    Point,
    List,
}

public readonly struct PointValue(double x, double y)
{
    public double X { get; } = x;

    public double Y { get; } = y;

    public override string ToString() =>
        $"({X.ToString("G12", CultureInfo.InvariantCulture)}, {Y.ToString("G12", CultureInfo.InvariantCulture)})";
}

/// <summary>求值结果。用结构体避免绘图热路径上的堆分配。</summary>
public readonly struct Value
{
    private readonly double _number;
    private readonly bool _boolean;
    private readonly PointValue _point;
    private readonly IReadOnlyList<Value>? _items;

    private Value(ValueKind kind, double number, bool boolean, PointValue point, IReadOnlyList<Value>? items)
    {
        Kind = kind;
        _number = number;
        _boolean = boolean;
        _point = point;
        _items = items;
    }

    public ValueKind Kind { get; }

    public static Value Undefined => default;

    public bool IsUndefined => Kind == ValueKind.Undefined;

    public static Value FromNumber(double value) => new(ValueKind.Number, value, false, default, null);

    public static Value FromBoolean(bool value) => new(ValueKind.Boolean, 0, value, default, null);

    public static Value FromPoint(double x, double y) => new(ValueKind.Point, 0, false, new PointValue(x, y), null);

    public static Value FromList(IReadOnlyList<Value> items) => new(ValueKind.List, 0, false, default, items);

    public bool TryGetNumber(out double value)
    {
        value = _number;
        return Kind == ValueKind.Number;
    }

    public bool TryGetBoolean(out bool value)
    {
        value = _boolean;
        return Kind == ValueKind.Boolean;
    }

    public bool TryGetPoint(out PointValue value)
    {
        value = _point;
        return Kind == ValueKind.Point;
    }

    public IReadOnlyList<Value>? Items => _items;

    public override string ToString() => Kind switch
    {
        ValueKind.Number => _number.ToString("G12", CultureInfo.InvariantCulture),
        ValueKind.Boolean => _boolean ? "true" : "false",
        ValueKind.Point => _point.ToString(),
        ValueKind.List => "[" + string.Join(", ", _items ?? []) + "]",
        _ => "undefined",
    };
}

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace DesmosSharp.App.ViewModels;

/// <summary>自由参数的滑块模型。</summary>
public partial class ParameterViewModel : ObservableObject
{
    private int _direction = 1;

    public ParameterViewModel(
        string name,
        double value = 1,
        double minimum = -10,
        double maximum = 10,
        double step = 0.1)
    {
        _name = name;
        _minimum = minimum;
        _maximum = maximum;
        _step = step;
        _value = Math.Clamp(value, minimum, maximum);
    }

    [ObservableProperty]
    private string _name;

    [ObservableProperty]
    private double _value;

    [ObservableProperty]
    private double _minimum;

    [ObservableProperty]
    private double _maximum;

    [ObservableProperty]
    private double _step;

    [ObservableProperty]
    private bool _isAnimating;

    /// <summary>是否展开最小值 / 最大值 / 步长设置。</summary>
    [ObservableProperty]
    private bool _isEditingSettings;

    /// <summary>动画倍速。</summary>
    public double Speed { get; set; } = 1;

    partial void OnValueChanged(double value)
    {
        if (value < Minimum)
        {
            Value = Minimum;
        }
        else if (value > Maximum)
        {
            Value = Maximum;
        }
    }

    partial void OnMinimumChanged(double value)
    {
        if (Maximum < value)
        {
            Maximum = value;
        }

        if (Value < value)
        {
            Value = value;
        }
    }

    partial void OnMaximumChanged(double value)
    {
        if (Minimum > value)
        {
            Minimum = value;
        }

        if (Value > value)
        {
            Value = value;
        }
    }

    /// <summary>推进一步动画（由外部定时器调用），到达两端后反向。</summary>
    public void Advance()
    {
        var range = Maximum - Minimum;
        if (range <= 0)
        {
            return;
        }

        var delta = Math.Max(Step, range / 200) * Speed;
        var next = Value + delta * _direction;

        if (next >= Maximum)
        {
            next = Maximum;
            _direction = -1;
        }
        else if (next <= Minimum)
        {
            next = Minimum;
            _direction = 1;
        }

        Value = next;
    }

    [RelayCommand]
    private void ToggleAnimation() => IsAnimating = !IsAnimating;

    [RelayCommand]
    private void ToggleSettings() => IsEditingSettings = !IsEditingSettings;
}

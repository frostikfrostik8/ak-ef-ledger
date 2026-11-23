using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using AefAccount.Models;
using AefAccount.Services;

namespace AefAccount.Converters;

public sealed class HexBrushConverter : IValueConverter
{
    private static readonly Dictionary<string, SolidColorBrush> Cache = new(StringComparer.OrdinalIgnoreCase);

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var hex = value?.ToString();
        if (string.IsNullOrWhiteSpace(hex)) hex = "#8FA3B8";
        if (Cache.TryGetValue(hex, out var cached)) return cached;
        try
        {
            var color = (Color)ColorConverter.ConvertFromString(hex);
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            Cache[hex] = brush;
            return brush;
        }
        catch
        {
            return Brushes.Gray;
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class DiffKindBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var kind = value is DiffKind diffKind ? diffKind : DiffKind.Neutral;
        return kind switch
        {
            DiffKind.Up => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#B0D040")),
            DiffKind.Down => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF6255")),
            _ => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#B1B1B1")),
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class InverseBooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class EmptyStringToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        string.IsNullOrWhiteSpace(value?.ToString()) ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class OperatorArtConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var kind = ParseKind(parameter);
        return OperatorArt.Load(value?.ToString(), kind);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    internal static OperatorArtKind ParseKind(object? parameter) => parameter?.ToString()?.ToLowerInvariant() switch
    {
        "icon" => OperatorArtKind.Icon,
        "detail" => OperatorArtKind.Detail,
        "full" => OperatorArtKind.Full,
        _ => OperatorArtKind.Portrait,
    };
}

public sealed class OperatorArtVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var kind = OperatorArtConverter.ParseKind(parameter);
        return OperatorArt.Load(value?.ToString(), kind) == null ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class ProgressBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var percent = value is IConvertible convertible ? convertible.ToDouble(culture) : 0;
        var key = percent >= 99.5 ? "Brush.Good" : percent <= 0.5 ? "Brush.Text.Faint" : "Brush.Accent";
        return Application.Current?.TryFindResource(key) as Brush ?? Brushes.Gray;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

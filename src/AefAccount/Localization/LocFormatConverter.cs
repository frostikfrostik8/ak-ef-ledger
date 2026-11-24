using System.Globalization;
using System.Windows.Data;

namespace AefAccount.Localization;

/// <summary>
/// Форматирует значение по шаблону из словаря:
/// <c>Text="{Binding Count, Converter={StaticResource LocFormat}, ConverterParameter=key}"</c>.
/// </summary>
public sealed class LocFormatConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = parameter?.ToString();
        if (string.IsNullOrWhiteSpace(key)) return value?.ToString() ?? "";
        return L.T(key, value ?? "");
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

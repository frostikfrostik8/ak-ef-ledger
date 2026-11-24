using System.Windows.Data;
using System.Windows.Markup;

namespace AefAccount.Localization;

/// <summary>
/// XAML-разметка <c>{loc:Loc key}</c>: создаёт OneWay-биндинг к текущему
/// переводу и обновляется при смене языка без перезапуска.
/// </summary>
[MarkupExtensionReturnType(typeof(string))]
public sealed class LocExtension : MarkupExtension
{
    public LocExtension()
    {
    }

    public LocExtension(string key)
    {
        Key = key;
    }

    [ConstructorArgument("key")]
    public string Key { get; set; } = "";

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        if (string.IsNullOrWhiteSpace(Key)) return "";

        return new Binding($"[{Key}]")
        {
            Source = LocalizationService.Instance,
            Mode = BindingMode.OneWay,
        }.ProvideValue(serviceProvider);
    }
}

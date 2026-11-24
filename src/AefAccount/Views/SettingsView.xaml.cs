using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using AefAccount.Localization;

namespace AefAccount.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
    }

    private void OpenSite_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo("https://endfield.gryphline.com/") { UseShellExecute = true });
        }
        catch
        {
            // Ссылка не критична для работы приложения.
        }
    }

    private void OpenLangFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var localization = LocalizationService.Instance;
            Directory.CreateDirectory(localization.LanguageDirectory);
            localization.EnsureLanguageFiles();
            Process.Start(new ProcessStartInfo(localization.LanguageDirectory) { UseShellExecute = true });
        }
        catch
        {
            // Папка языков не критична для работы приложения.
        }
    }
}

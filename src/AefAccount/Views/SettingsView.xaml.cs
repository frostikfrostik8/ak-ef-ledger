using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

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
}

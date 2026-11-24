using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using AefAccount;
using AefAccount.Localization;
using AefAccount.Models;
using AefAccount.ViewModels;

namespace AefAccount.Views;

public partial class SnapshotsView : UserControl
{
    public SnapshotsView()
    {
        InitializeComponent();
    }

    private async void OpenSnapshot_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm || (sender as FrameworkElement)?.DataContext is not SnapshotSummary snapshot) return;
        await vm.OpenSnapshotAsync(snapshot);
        (Window.GetWindow(this) as MainWindow)?.NavigateTo("overview");
    }

    private void OpenReport_Click(object sender, RoutedEventArgs e) => OpenPath(sender, data => data.ReportPath);

    private void OpenRaw_Click(object sender, RoutedEventArgs e) => OpenPath(sender, data => data.RawPath);

    private void OpenPath(object sender, Func<SnapshotSummary, string?> selector)
    {
        if (DataContext is not MainViewModel vm || (sender as FrameworkElement)?.DataContext is not SnapshotSummary snapshot) return;
        var relative = selector(snapshot);
        if (string.IsNullOrWhiteSpace(relative)) return;
        var path = Path.IsPathRooted(relative) ? relative : Path.Combine(vm.RepoRoot, relative);
        if (!File.Exists(path))
        {
            MessageBox.Show(L.T("message_file_not_found", path), "AEF", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception exception)
        {
            MessageBox.Show(exception.Message, L.T("message_open_file_error"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}

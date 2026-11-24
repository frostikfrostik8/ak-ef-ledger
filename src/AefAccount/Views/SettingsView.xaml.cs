using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using AefAccount.Localization;
using AefAccount.Services;
using AefAccount.ViewModels;
using Microsoft.Win32;

namespace AefAccount.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
    }

    private MainViewModel? ViewModel => DataContext as MainViewModel;

    private Window? Owner => Window.GetWindow(this);

    private void OpenSite_Click(object sender, RoutedEventArgs e) =>
        OpenUrl("https://endfield.gryphline.com/");

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

    private async void PickArtFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = L.T("settings_art_pick_folder"),
            Multiselect = false,
        };
        if (dialog.ShowDialog(Owner) != true) return;

        await RunArtOperationAsync(
            () => ArtPack.UseFolder(dialog.FolderName),
            "settings_art_applied");
    }

    private async void InstallArtZip_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = L.T("settings_art_pick_zip"),
            Filter = "ZIP (*.zip)|*.zip|All files (*.*)|*.*",
            CheckFileExists = true,
        };
        if (dialog.ShowDialog(Owner) != true) return;

        await RunArtOperationAsync(
            () => ArtPack.InstallFromZip(dialog.FileName),
            "settings_art_installed");
    }

    private async void DownloadArt_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not { } viewModel) return;

        viewModel.IsArtBusy = true;
        viewModel.ArtProgress = 0;
        string? archive = null;
        try
        {
            var progress = new Progress<double>(value => viewModel.ArtProgress = value * 100);
            archive = await ArtPack.DownloadAsync(progress, CancellationToken.None);
            var status = await Task.Run(() => ArtPack.InstallFromZip(archive));
            ApplyArtStatus(status, "settings_art_installed");
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            // 404 — приватный репозиторий или пак ещё не опубликован.
            var open = Ask(
                L.T("settings_art_download_failed", ArtPackCatalog.ReleaseUrl),
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (open == MessageBoxResult.Yes) OpenUrl(ArtPackCatalog.ReleaseUrl);
        }
        catch (Exception exception)
        {
            ShowArtError(exception);
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(archive))
            {
                try
                {
                    File.Delete(archive);
                }
                catch (IOException)
                {
                    // Временный архив не критичен.
                }
            }
            viewModel.IsArtBusy = false;
            viewModel.ArtProgress = 0;
        }
    }

    private async void RemoveArt_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not { } viewModel) return;

        var confirm = Ask(
            L.T("settings_art_confirm_remove"),
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes) return;

        await RunArtOperationAsync(() => ArtPack.Remove(), "settings_art_removed");
    }

    private void OpenArtFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var root = ArtPack.Status.Root ?? ArtPack.LocalRoot;
            Directory.CreateDirectory(root);
            Process.Start(new ProcessStartInfo(root) { UseShellExecute = true });
        }
        catch
        {
            // Папка арта не критична для работы приложения.
        }
    }

    private async Task RunArtOperationAsync(Func<ArtPackStatus> operation, string successKey)
    {
        if (ViewModel is not { } viewModel) return;

        viewModel.IsArtBusy = true;
        try
        {
            var status = await Task.Run(operation);
            ApplyArtStatus(status, successKey);
        }
        catch (Exception exception)
        {
            ShowArtError(exception);
        }
        finally
        {
            viewModel.IsArtBusy = false;
        }
    }

    private void ApplyArtStatus(ArtPackStatus status, string successKey)
    {
        if (ViewModel is not { } viewModel) return;

        viewModel.RefreshArtStatus();
        if (!string.IsNullOrWhiteSpace(successKey))
            viewModel.SetSettingsStatus(L.T(successKey, status.OperatorCount));
    }

    private void ShowArtError(Exception exception)
    {
        var message = exception is InvalidDataException or FileNotFoundException
            ? L.T("settings_art_invalid")
            : exception.Message;
        Ask(message, MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private MessageBoxResult Ask(string message, MessageBoxButton buttons, MessageBoxImage image)
    {
        var title = L.T("settings_art_title");
        return Owner is { } owner
            ? MessageBox.Show(owner, message, title, buttons, image)
            : MessageBox.Show(message, title, buttons, image);
    }

    private static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch
        {
            // Ссылка не критична для работы приложения.
        }
    }
}

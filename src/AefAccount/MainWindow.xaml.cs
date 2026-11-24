using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using AefAccount.Localization;
using AefAccount.Models;
using AefAccount.Services;
using AefAccount.ViewModels;
using AefAccount.Views;

namespace AefAccount;

public partial class MainWindow : Window
{
    private bool _ready;
    private bool _tokenPromptVisible;
    private FileSystemWatcher? _tokenWatcher;

    public MainViewModel ViewModel { get; } = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = ViewModel;
        ViewModel.NavigateRequested += (_, key) => NavigateTo(key);
        ViewModel.TokenRequired += (_, _) => ShowTokenPrompt();
        Loaded += MainWindow_Loaded;
        StateChanged += (_, _) => UpdateMaximizeIcon();
        Activated += (_, _) => EnsureTokenPrompt();
        LocalizationService.Instance.LanguageChanged += OnLanguageChanged;
        Closed += (_, _) =>
        {
            LocalizationService.Instance.LanguageChanged -= OnLanguageChanged;
            _tokenWatcher?.Dispose();
        };
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        // Пересоздаём текущую страницу: строки моделей (диффы, даты, гир)
        // вычисляются в момент биндинга и не реализуют INPC.
        Dispatcher.BeginInvoke(new Action(() => ShowPage(ViewModel.CurrentPageKey)), DispatcherPriority.Background);
    }

    private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            ToggleMaximize();
            return;
        }
        if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
        {
            try { DragMove(); } catch (InvalidOperationException) { }
        }
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void Maximize_Click(object sender, RoutedEventArgs e) => ToggleMaximize();

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void ToggleMaximize()
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        UpdateMaximizeIcon();
    }

    private void UpdateMaximizeIcon()
    {
        if (MaximizeIcon == null) return;
        MaximizeIcon.Text = WindowState == WindowState.Maximized ? "\uE923" : "\uE922";
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        _ready = true;
        await ViewModel.InitializeAsync();
        var pageArg = Environment.GetCommandLineArgs()
            .FirstOrDefault(arg => arg.StartsWith("--page=", StringComparison.OrdinalIgnoreCase))
            ?.Split('=', 2)[1];
        var index = pageArg == null ? 0 : ViewModel.NavItems.ToList().FindIndex(item => item.Key == pageArg);
        NavList.SelectedIndex = index >= 0 ? index : 0;
        ShowPage(index >= 0 ? ViewModel.NavItems[index].Key : "overview");

        var operatorArg = Environment.GetCommandLineArgs()
            .FirstOrDefault(arg => arg.StartsWith("--operator=", StringComparison.OrdinalIgnoreCase))
            ?.Split('=', 2)[1];
        if (!string.IsNullOrWhiteSpace(operatorArg))
        {
            var op = ViewModel.Data?.Operators.FirstOrDefault(item =>
                item.Name.Equals(operatorArg, StringComparison.OrdinalIgnoreCase) ||
                item.DisplayName.Equals(operatorArg, StringComparison.OrdinalIgnoreCase));
            if (op != null)
            {
                new OperatorWindow(op) { Owner = this }.Show();
            }
        }

        var testArg = Environment.GetCommandLineArgs()
            .FirstOrDefault(arg => arg.StartsWith("--test=", StringComparison.OrdinalIgnoreCase))
            ?.Split('=', 2)[1];
        switch (testArg?.ToLowerInvariant())
        {
            case "operator":
                ViewModel.AddTestOperatorCommand.Execute(null);
                break;
            case "vesper":
                ViewModel.AddVesperCommand.Execute(null);
                break;
            case "dev":
                ViewModel.LoadDevProfileCommand.Execute(null);
                break;
        }

        StartTokenWatcher();
        EnsureTokenPrompt();
    }

    private void StartTokenWatcher()
    {
        try
        {
            var directory = TokenService.SecretsDirectory(ViewModel.RepoRoot);
            Directory.CreateDirectory(directory);
            _tokenWatcher = new FileSystemWatcher(directory)
            {
                IncludeSubdirectories = false,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
                EnableRaisingEvents = true,
            };
            _tokenWatcher.Created += (_, _) => ScheduleTokenCheck();
            _tokenWatcher.Changed += (_, _) => ScheduleTokenCheck();
            _tokenWatcher.Deleted += (_, _) => ScheduleTokenCheck();
            _tokenWatcher.Renamed += (_, _) => ScheduleTokenCheck();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            // Проверка на Activated и перед снимком всё равно сработает.
        }
    }

    private void ScheduleTokenCheck() =>
        Dispatcher.BeginInvoke(new Action(EnsureTokenPrompt), DispatcherPriority.Background);

    private void EnsureTokenPrompt()
    {
        if (TokenService.HasToken(ViewModel.RepoRoot))
        {
            if (_tokenPromptVisible) HideTokenPrompt();
            return;
        }

        ShowTokenPrompt();
    }

    private void ShowTokenPrompt()
    {
        if (_tokenPromptVisible)
        {
            TokenInput.Focus();
            return;
        }

        _tokenPromptVisible = true;
        TokenError.Visibility = Visibility.Collapsed;
        TokenInput.Clear();
        TokenOverlay.Visibility = Visibility.Visible;
        Dispatcher.BeginInvoke(new Action(() =>
        {
            TokenInput.Focus();
            Keyboard.Focus(TokenInput);
        }), DispatcherPriority.Input);
    }

    private void HideTokenPrompt()
    {
        _tokenPromptVisible = false;
        TokenOverlay.Visibility = Visibility.Collapsed;
        TokenInput.Clear();
        TokenError.Visibility = Visibility.Collapsed;
    }

    private void TokenInput_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (TokenError.Visibility == Visibility.Visible) TokenError.Visibility = Visibility.Collapsed;
    }

    private void TokenInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        e.Handled = true;
        SaveToken();
    }

    private void TokenSave_Click(object sender, RoutedEventArgs e) => SaveToken();

    private void SaveToken()
    {
        var token = TokenInput.Password.Trim();
        if (token.Length == 0)
        {
            ShowTokenError(L.T("token_error_empty"));
            return;
        }
        if (token.Length < 16)
        {
            ShowTokenError(L.T("token_error_short"));
            return;
        }

        try
        {
            TokenService.Save(ViewModel.RepoRoot, token);
            HideTokenPrompt();
            ViewModel.SetSettingsStatus(L.T("token_status_saved"));
            ViewModel.SetStatus(L.T("token_status_saved"));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            ShowTokenError(L.T("token_error_save"));
        }
    }

    private void ShowTokenError(string message)
    {
        TokenError.Text = message;
        TokenError.Visibility = Visibility.Visible;
        TokenInput.Focus();
    }

    private void TokenExit_Click(object sender, RoutedEventArgs e) => Close();

    private void OpenSkport_Click(object sender, RoutedEventArgs e) => OpenUrl("https://www.skport.com/");

    private void OpenTokenPage_Click(object sender, RoutedEventArgs e) =>
        OpenUrl("https://web-api.skport.com/cookie_store/account_token");

    private void OpenRepository_Click(object sender, RoutedEventArgs e) =>
        OpenUrl("https://github.com/frostikfrostik8/ak-ef-ledger");

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

    private void NavList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready || NavList.SelectedItem is not NavItem item) return;
        ShowPage(item.Key);
    }

    public void NavigateTo(string key)
    {
        var index = ViewModel.NavItems.ToList().FindIndex(item => item.Key == key);
        if (index >= 0) NavList.SelectedIndex = index;
        ShowPage(key);
    }

    private void ShowPage(string key)
    {
        ViewModel.SetPage(key);
        ContentHost.Children.Clear();
        FrameworkElement view = key switch
        {
            "roster" => new RosterView(),
            "dynamics" => new DynamicsView(),
            "progress" => new ProgressView(),
            "achievements" => new AchievementsView(),
            "snapshots" => new SnapshotsView(),
            "settings" => new SettingsView(),
            _ => new OverviewView(),
        };
        view.DataContext = ViewModel;
        ContentHost.Children.Add(view);
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

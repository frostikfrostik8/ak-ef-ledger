using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using AefAccount.Localization;
using AefAccount.Models;
using AefAccount.ViewModels;
using AefAccount.Views;

namespace AefAccount;

public partial class MainWindow : Window
{
    private bool _ready;

    public MainViewModel ViewModel { get; } = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = ViewModel;
        ViewModel.NavigateRequested += (_, key) => NavigateTo(key);
        Loaded += MainWindow_Loaded;
        StateChanged += (_, _) => UpdateMaximizeIcon();
        LocalizationService.Instance.LanguageChanged += OnLanguageChanged;
        Closed += (_, _) => LocalizationService.Instance.LanguageChanged -= OnLanguageChanged;
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

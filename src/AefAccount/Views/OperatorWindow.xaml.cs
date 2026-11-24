using System.Windows;
using AefAccount.Localization;
using AefAccount.Models;

namespace AefAccount.Views;

public partial class OperatorWindow : Window
{
    private readonly OperatorModel _operator;

    public OperatorWindow(OperatorModel op)
    {
        _operator = op;
        InitializeComponent();
        DataContext = op;
        UpdateTitle();
        LocalizationService.Instance.LanguageChanged += OnLanguageChanged;
        Closed += (_, _) => LocalizationService.Instance.LanguageChanged -= OnLanguageChanged;
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        // Пересоздаём DataContext, чтобы пересчитались строки модели.
        DataContext = null;
        DataContext = _operator;
        UpdateTitle();
    }

    private void UpdateTitle() => Title = $"{_operator.DisplayName} · AK:EF";

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}


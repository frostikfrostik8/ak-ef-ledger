using System.Windows;
using AefAccount.Models;

namespace AefAccount.Views;

public partial class OperatorWindow : Window
{
    public OperatorWindow(OperatorModel op)
    {
        InitializeComponent();
        DataContext = op;
        Title = $"{op.DisplayName} · AK:EF";
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}


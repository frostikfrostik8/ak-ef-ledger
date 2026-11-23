using System.Windows;
using System.Windows.Controls;
using AefAccount.Models;
using AefAccount.Views;

namespace AefAccount.Controls;

public partial class OperatorCard : UserControl
{
    public OperatorCard()
    {
        InitializeComponent();
    }

    private void Root_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not OperatorModel op) return;
        var window = new OperatorWindow(op) { Owner = Window.GetWindow(this) };
        window.ShowDialog();
    }
}

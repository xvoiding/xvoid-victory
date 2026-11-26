using System.Windows;
using Xvoid.Launcher.ViewModels;

namespace Xvoid.Launcher.Views;

public partial class SetupWindow : Window
{
    public SetupWindow(SetupViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        vm.Step = 0;
        vm.Finished += Close;
        // Leaving early is allowed; the launcher then opens Settings so nothing is stuck.
    }

    void Close_Click(object sender, RoutedEventArgs e) => Close();
}

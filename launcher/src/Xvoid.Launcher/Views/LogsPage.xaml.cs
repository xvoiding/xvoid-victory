using System.Collections.Specialized;
using System.Windows.Controls;

namespace Xvoid.Launcher.Views;

public partial class LogsPage : UserControl
{
    public LogsPage()
    {
        InitializeComponent();
        // Follow the newest line, like a terminal.
        DataContextChanged += (_, _) =>
        {
            if (DataContext is ViewModels.LogsViewModel vm)
                ((INotifyCollectionChanged)vm.Lines).CollectionChanged += (_, e) =>
                {
                    if (e.Action == NotifyCollectionChangedAction.Add && List.Items.Count > 0)
                        List.ScrollIntoView(List.Items[^1]);
                };
        };
    }
}

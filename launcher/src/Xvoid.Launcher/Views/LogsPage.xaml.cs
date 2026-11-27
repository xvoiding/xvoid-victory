using System.Collections.Specialized;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Xvoid.Launcher.Views;

public partial class LogsPage : UserControl
{
    bool _scrollQueued;

    public LogsPage()
    {
        InitializeComponent();
        // Follow the newest line, like a terminal. The scroll waits until the list has taken the new lines in:
        // scrolling from inside the collection's own change event (before the ListBox has seen it) makes WPF throw
        // "An ItemsControl is inconsistent with its items source", once for every line.
        DataContextChanged += (_, _) =>
        {
            if (DataContext is ViewModels.LogsViewModel vm)
                ((INotifyCollectionChanged)vm.Lines).CollectionChanged += (_, e) =>
                {
                    if (e.Action != NotifyCollectionChangedAction.Add || _scrollQueued) return;
                    _scrollQueued = true;
                    Dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
                    {
                        _scrollQueued = false;
                        if (List.Items.Count > 0) List.ScrollIntoView(List.Items[^1]);
                    });
                };
        };
    }
}

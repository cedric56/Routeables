using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
#if WINUI3
using System.Collections.Specialized;
using System.ComponentModel;
#else
using Microsoft.UI.Xaml.Markup;
#endif
using Routeables.Abstraction;

#if WINUI3
namespace Routeables.WinUI3;
#else
namespace Routeables.Uno;
#endif

/// <summary>
/// A <see cref="Microsoft.UI.Xaml.Controls.ListView"/> that shows the entries of an <see cref="IMainViewModel"/> and selects the current one.
/// Give it the main view model as <c>DataContext</c> directly or by inheritance:
/// <code>
/// &lt;routeables:RouteableListView /&gt;
/// </code>
/// Choosing an entry goes to it; a locked entry is disabled.
/// </summary>
public partial class RouteableMenu : Microsoft.UI.Xaml.Controls.ListView
{
    /// <summary>Creates the list.</summary>
    public RouteableMenu()
    {
        SelectionMode = ListViewSelectionMode.Single;
#if WINUI3
        // WinUI 3: XamlReader cannot find a control of a library (it has no XAML metadata for it), and the containers are not
        // reliably given a content in code. So the list holds the controls themselves (one RouteableMenuItem per entry), which a
        // ListView shows as they are, and the binding to the main view model is done here.
        DataContextChanged += (_, _) => Attach(DataContext as IMainViewModel);
        SelectionChanged += OnSelectionChanged;
        Unloaded += (_, _) => Attach(null);
        Loaded += (_, _) => Attach(DataContext as IMainViewModel);
#else
        SetBinding(ItemsSourceProperty, new Binding { Path = new PropertyPath(nameof(IMainViewModel.Entries)) });
        SetBinding(SelectedItemProperty, new Binding { Path = new PropertyPath(nameof(IMainViewModel.SelectedEntry)), Mode = BindingMode.TwoWay });
        ItemTemplate = (DataTemplate)XamlReader.Load(MenuItemTemplate);
#endif
    }

#if WINUI3
    private IMainViewModel? _main;
    private bool _syncing;

    private void Attach(IMainViewModel? main)
    {
        if (ReferenceEquals(main, _main))
        {
            if (main is not null)
            {
                Rebuild();
            }

            return;
        }

        if (_main is not null)
        {
            _main.Entries.CollectionChanged -= OnEntriesChanged;
            if (_main is INotifyPropertyChanged oldNotifier)
            {
                oldNotifier.PropertyChanged -= OnMainPropertyChanged;
            }
        }

        _main = main;
        if (main is not null)
        {
            main.Entries.CollectionChanged += OnEntriesChanged;
            if (main is INotifyPropertyChanged notifier)
            {
                notifier.PropertyChanged += OnMainPropertyChanged;
            }
        }

        Rebuild();
    }

    private void OnEntriesChanged(object? sender, NotifyCollectionChangedEventArgs e) => OnUi(Rebuild);

    private void OnMainPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is null or nameof(IMainViewModel.SelectedEntry))
        {
            OnUi(SyncSelection);
        }
    }

    private void OnUi(Action action)
    {
        if (DispatcherQueue is { } queue && !queue.HasThreadAccess)
        {
            queue.TryEnqueue(() => action());
        }
        else
        {
            action();
        }
    }

    private void Rebuild()
    {
        _syncing = true;
        try
        {
            Items.Clear();
            if (_main is not null)
            {
                foreach (var entry in _main.Entries)
                {
                    Items.Add(new RouteableMenuItem { Item = entry });
                }
            }
        }
        finally
        {
            _syncing = false;
        }

        SyncSelection();
    }

    private void SyncSelection()
    {
        _syncing = true;
        try
        {
            var selected = _main?.SelectedEntry;
            SelectedItem = Items.OfType<RouteableMenuItem>().FirstOrDefault(item => ReferenceEquals(item.Item, selected));
        }
        finally
        {
            _syncing = false;
        }
    }

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_syncing && _main is not null && SelectedItem is RouteableMenuItem { Item: { } entry })
        {
            _main.SelectedEntry = entry;
        }
    }
#else
    // Built from text: a template cannot be assembled from code. {Binding} gives the entry itself to the item.
    // The namespace is the one of the host this file is compiled into, never written by hand.
    private static readonly string MenuItemTemplate =
        $$"""
        <DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                      xmlns:routeables="using:{{typeof(RouteableMenuItem).Namespace}}">
            <routeables:RouteableMenuItem Item="{Binding}" />
        </DataTemplate>
        """;
#endif
}
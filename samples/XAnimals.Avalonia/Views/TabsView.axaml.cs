using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Routeables.Avalonia;
using System;
using XAnimals.ViewModels;

namespace XAnimals.Avalonia;

public partial class TabsView : RouteableFor<TabsViewModel>
{
    private IDisposable? _tabs;

    public TabsView()
    {
        InitializeComponent();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _tabs ??= Tabs.AttachRouteables(Application.Current!.GetRouteableServices(), TabsViewModel.Region);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _tabs?.Dispose();
        _tabs = null;
        base.OnDetachedFromVisualTree(e);
    }
}

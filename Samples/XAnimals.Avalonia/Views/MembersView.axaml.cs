using Routeables.Avalonia;
using XAnimals.ViewModels;

namespace XAnimals.Avalonia;

public partial class MembersView : RouteableFor<MembersViewModel>
{
    public MembersView()
    {
        InitializeComponent();
    }
}

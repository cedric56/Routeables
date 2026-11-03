using Routeables.Avalonia;
using XAnimals.ViewModels;

namespace XAnimals.Avalonia;

public partial class BoardView : RouteableFor<BoardViewModel>
{
    public BoardView()
    {
        InitializeComponent();
    }
}

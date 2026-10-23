using CommunityToolkit.Mvvm.Input;
using Routeables.Core;
using System.Windows.Input;

namespace Routeables.Tests.Core;

/// <summary>
/// Concrete <see cref="CloseableViewModelBase{TResult}"/> used to test the base class's
/// close/command/title behavior in isolation.
/// </summary>
public class TestCloseableViewModel : CloseableViewModelBase<string>
{
    public bool CanCloseResult { get; set; } = true;

    public override ICommand CloseCommand { get; } = new RelayCommand(() => { });

    public override string Title => "Test Title";

    public override Task<bool> CanCloseAsync() => Task.FromResult(CanCloseResult);
}

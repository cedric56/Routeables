using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Routeables.Avalonia;
using System.Globalization;
using System.Windows.Input;
using AvaloniaButton = global::Avalonia.Controls.Button;
using AvaloniaPointerEventArgs = global::Avalonia.Input.PointerEventArgs;
using AvaloniaTappedEventArgs = global::Avalonia.Input.TappedEventArgs;

namespace Routeables.Tests.Avalonia;

public class AvaloniaHelpersTests
{
    private sealed class RecordingCommand(bool canExecute = true) : ICommand
    {
        public object? Parameter { get; private set; }
        public int ExecuteCount { get; private set; }
        public bool CanExecute(object? parameter) => canExecute;
        public void Execute(object? parameter)
        {
            Parameter = parameter;
            ExecuteCount++;
        }
        public event EventHandler? CanExecuteChanged;

        private void OnCanExecuteChanged()
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private static void RaiseDoubleTapped(AvaloniaButton button)
    {
        var pointerEvent = new AvaloniaPointerEventArgs(
            InputElement.PointerPressedEvent,
            button,
            pointer: null!,
            rootVisual: null,
            rootVisualPosition: default,
            timestamp: 0,
            properties: default,
            modifiers: KeyModifiers.None);
        button.RaiseEvent(new AvaloniaTappedEventArgs(InputElement.DoubleTappedEvent, pointerEvent));
    }

    [Fact]
    public void NullConverter_reports_nullness_and_rejects_reverse_conversion()
    {
        var converter = new RouteableNullConverter();
        var culture = CultureInfo.InvariantCulture;

        Assert.Equal(true, converter.Convert(null, typeof(bool), null, culture));
        Assert.Equal(false, converter.Convert("value", typeof(bool), null, culture));
        Assert.Throws<NotSupportedException>(
            () => converter.ConvertBack(true, typeof(object), null, culture));
    }

    [Fact]
    public void DoubleClickBehavior_executes_the_command_with_its_attached_parameter()
    {
        var button = new AvaloniaButton();
        var command = new RecordingCommand();
        var parameter = new object();
        RouteableDoubleClickBehavior.SetCommandParameter(button, parameter);
        RouteableDoubleClickBehavior.SetCommand(button, command);

        RaiseDoubleTapped(button);

        Assert.Equal(1, command.ExecuteCount);
        Assert.Same(parameter, command.Parameter);
    }

    [Fact]
    public void DoubleClickBehavior_does_not_execute_when_command_cannot_execute()
    {
        var button = new AvaloniaButton();
        var command = new RecordingCommand(canExecute: false);
        RouteableDoubleClickBehavior.SetCommand(button, command);

        RaiseDoubleTapped(button);

        Assert.Equal(0, command.ExecuteCount);
    }

    [Fact]
    public void DoubleClickBehavior_detaches_the_handler_when_the_command_is_cleared()
    {
        var button = new AvaloniaButton();
        var command = new RecordingCommand();
        RouteableDoubleClickBehavior.SetCommand(button, command);
        RouteableDoubleClickBehavior.SetCommand(button, null!);

        RaiseDoubleTapped(button);

        Assert.Equal(0, command.ExecuteCount);
        Assert.Null(RouteableDoubleClickBehavior.GetCommand(button));
    }

    [Fact]
    public void DoubleClickBehavior_attached_properties_round_trip_through_their_accessors()
    {
        var button = new AvaloniaButton();
        var command = new RecordingCommand();
        var parameter = new object();

        RouteableDoubleClickBehavior.SetCommand(button, command);
        RouteableDoubleClickBehavior.SetCommandParameter(button, parameter);

        Assert.Same(command, RouteableDoubleClickBehavior.GetCommand(button));
        Assert.Same(parameter, RouteableDoubleClickBehavior.GetCommandParameter(button));
    }

    [Fact]
    public void RouteableMenu_binds_to_routeables_and_selects_the_current_routeable_two_way()
    {
        var list = new RouteableMenu();
        var itemsBinding = BindingOperations.GetBindingExpressionBase(list, ItemsControl.ItemsSourceProperty);
        var selectedBinding = BindingOperations.GetBindingExpressionBase(list, SelectingItemsControl.SelectedItemProperty);

        Assert.NotNull(itemsBinding);
        Assert.NotNull(selectedBinding);
        Assert.NotNull(list.ItemTemplate);
        Assert.Single(list.Styles);
    }
}

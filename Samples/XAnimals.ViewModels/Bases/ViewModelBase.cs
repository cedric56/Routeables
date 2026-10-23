using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace XAnimals.ViewModels;

/// <summary>The part every view model of the samples has in common: it tells the view when a property changes.</summary>
public abstract class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

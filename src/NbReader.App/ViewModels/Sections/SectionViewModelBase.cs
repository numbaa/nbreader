using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace NbReader.App.ViewModels;

public abstract class SectionViewModelBase : INotifyPropertyChanged
{
    protected SectionViewModelBase(MainWindowViewModel owner)
    {
        Owner = owner;
        Owner.PropertyChanged += OnOwnerPropertyChanged;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected MainWindowViewModel Owner { get; }

    private void OnOwnerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.PropertyName))
        {
            return;
        }

        OnPropertyChanged(e.PropertyName);
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

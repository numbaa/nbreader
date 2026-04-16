using Avalonia.Controls;
using NbReader.App.ViewModels;
using NbReader.Infrastructure;

namespace NbReader.App;

public partial class MainWindow : Window
{
    public MainWindow(AppRuntime runtime)
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel(runtime);
    }
}
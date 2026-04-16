using Avalonia.Controls;
using NbReader.App.ViewModels;

namespace NbReader.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel();
    }
}
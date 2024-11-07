using System;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace NBReader.ViewModels
{

    public partial class HomeViewModel : ViewModelBase
    {
        [ObservableProperty]
        private bool _IsButtonVisible;

        public HomeViewModel()
        {
            IsButtonVisible = true;
        }

        [RelayCommand]
        private void OpenFileDialog()
        {
            System.Console.Out.WriteLine("abcd");
        }
    }

}

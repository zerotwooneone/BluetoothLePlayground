using System;

using BtServer.ViewModels;

using Windows.UI.Xaml.Controls;

namespace BtServer.Views
{
    public sealed partial class MainPage : Page
    {
        private MainViewModel ViewModel => DataContext as MainViewModel;

        public MainPage()
        {
            InitializeComponent();
        }
    }
}

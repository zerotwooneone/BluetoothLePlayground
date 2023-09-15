using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using ListenerGui.Main;

namespace ListenerGui
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private void App_OnStartup(object sender, StartupEventArgs e)
        {
            var vm = new MainWindowViewmodel();
            var mainWindow = new MainWindow();
            mainWindow.DataContext = vm;
            mainWindow.Show();
        }
    }
}
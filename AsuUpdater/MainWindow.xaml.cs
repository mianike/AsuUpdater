using System.Reflection;
using System.Windows;
using AsuUpdater.Classes;

namespace AsuUpdater
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            Release.Init(Assembly.GetExecutingAssembly().GetName().Version);
            Title += " " + Release.GetInstance().GetReleaseTitle();

            var updaterViewModel = new UpdaterViewModel();
            DataContext = updaterViewModel;

            updaterViewModel.Close += Close;
            Closing += updaterViewModel.OnWindowClosing;
        }
    }

}

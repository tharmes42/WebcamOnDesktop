using System.ComponentModel;
using Windows.UI.Xaml.Controls;
using System.Runtime.CompilerServices;

using System.Threading.Tasks;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Navigation;
using WebcamOnDesktop.Controls;
using Windows.UI.ViewManagement;
using Windows.Foundation;
using WebcamOnDesktop.Services;
using Windows.UI.Xaml.Media.Animation;

// Die Elementvorlage "Leere Seite" wird unter https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x407 dokumentiert.

namespace WebcamOnDesktop.Views
{
    /// <summary>
    /// Eine leere Seite, die eigenständig verwendet oder zu der innerhalb eines Rahmens navigiert werden kann.
    /// </summary>
    public sealed partial class MainPage : Page, INotifyPropertyChanged
    {
        ObservableCollection<Camera> cameras = new ObservableCollection<Camera>();
        public MainPage()
        {
            InitializeComponent();
            CameraListView.Loaded += CameraListView_Loaded;
            //if you want any size smaller than the default 500x320, you will need to manually reset it
            //ApplicationView.GetForCurrentView().SetPreferredMinSize(new Size(200, 100));
            ApplicationView.PreferredLaunchViewSize = new Size(500, 380);
            ApplicationView.PreferredLaunchWindowingMode = ApplicationViewWindowingMode.PreferredLaunchViewSize;


        }

        private void CameraListView_Loaded(object sender, RoutedEventArgs e)
        {
            // Set focus so the first item of the listview has focus
            // instead of some item which is not visible on page load
            CameraListView.Focus(FocusState.Programmatic);
  
        }
        


        private void CompactOverlayButton_Click(object sender, RoutedEventArgs e)
        {
            Windows.Storage.ApplicationDataContainer localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
            //preselect camera if it was previously selected
            localSettings.Values["cameraSelected"] = CameraListView.SelectedItem?.ToString();
            NavigationService.Navigate(typeof(CameraPage), null, new SuppressNavigationTransitionInfo());
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            CameraListView.ItemsSource = await CameraControl.GetCamerasAsync();
            Windows.Storage.ApplicationDataContainer localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;

            //save name of camera (this is used by target page CameraPage, note: is it dirty to use localSettings as alternative to a mvvm pattern?
            string cameraSelected = (string)localSettings?.Values["cameraSelected"];
            int cameraSelectedIndex = 0;
            for (int i=0; i<CameraListView.Items.Count; i++)
            {
                if (CameraListView.Items[i].ToString() == cameraSelected)
                {
                    cameraSelectedIndex = i;
                    break;
                }
            }
            
            CameraListView.SelectedIndex = cameraSelectedIndex;
            
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            Windows.Storage.ApplicationDataContainer localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
            //preselect camera if it was previously selected
            localSettings.Values["cameraSelected"] =  CameraListView.SelectedItem?.ToString();
            
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void Set<T>(ref T storage, T value, [CallerMemberName] string propertyName = null)
        {
            if (Equals(storage, value))
            {
                return;
            }

            storage = value;
            OnPropertyChanged(propertyName);
        }

        private void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));


    }


}

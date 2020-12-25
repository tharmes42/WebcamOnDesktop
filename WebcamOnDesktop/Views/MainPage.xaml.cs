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
        }

        private void CameraListView_Loaded(object sender, RoutedEventArgs e)
        {
            // Set focus so the first item of the listview has focus
            // instead of some item which is not visible on page load
            CameraListView.Focus(FocusState.Programmatic);
            
        }


        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            CameraListView.ItemsSource = await CameraControl.GetCamerasAsync();
            Windows.Storage.ApplicationDataContainer localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
            /*if (CameraListView.Items.Count > 0)
                CameraListView.SelectedIndex = 0;
            */
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
            //cameras = await CameraControl.GetCamerasAsync();
            /*            CameraListView.ItemsSource = await Camera.GetCamerasAsync();
                        cameras = await Camera.GetCamerasAsync();
            */
            /*contacts2.Add(new Contact("John", "Doe", "ABC Printers"));
            contacts2.Add(new Contact("Jane", "Doe", "XYZ Refrigerators"));
            contacts2.Add(new Contact("Santa", "Claus", "North Pole Toy Factory Inc."));
            */
            
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            Windows.Storage.ApplicationDataContainer localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
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

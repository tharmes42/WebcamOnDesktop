using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using WebcamOnDesktop.Helpers;
using WebcamOnDesktop.Services;
using Windows.ApplicationModel.Core;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;


// Die Elementvorlage "Leere Seite" wird unter https://go.microsoft.com/fwlink/?LinkId=234238 dokumentiert.

namespace WebcamOnDesktop.Views
{
    /// <summary>
    /// CameraPage
    /// </summary>
    public sealed partial class CameraPage : Page, INotifyPropertyChanged
    {
        public CameraPage()
        {
            this.InitializeComponent();
        }
        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            Windows.Storage.ApplicationDataContainer localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
            cameraControl.CameraSelected = (string)localSettings?.Values["cameraSelected"];
            /*
            //if settings page has not been used yet, use these values as defaults
            if (localSettings?.Values["FlipHorizontal"] == null) {
                await Windows.Storage.ApplicationData.Current.LocalSettings.SaveAsync("FlipHorizontal", "True"); 
            }
            */

            cameraControl.FlipHorizontal = await Windows.Storage.ApplicationData.Current.LocalSettings.ReadAsync<bool>("FlipHorizontal");
            cameraControl.FlipVertical = await Windows.Storage.ApplicationData.Current.LocalSettings.ReadAsync<bool>("FlipVertical");

            await cameraControl.InitializeCameraAsync();

            if (ApplicationView.GetForCurrentView().IsViewModeSupported(ApplicationViewMode.CompactOverlay))
            {
                //bool modeSwitched = await ApplicationView.GetForCurrentView().TryEnterViewModeAsync(ApplicationViewMode.CompactOverlay);

                ViewModePreferences compactOptions = ViewModePreferences.CreateDefault(ApplicationViewMode.CompactOverlay);

                compactOptions.CustomSize = new Windows.Foundation.Size(320, 240);
                // TODO: size must get correct aspectratio
                //compactOptions.CustomSize = cameraControl.PreviewSize;
                compactOptions.ViewSizePreference = ViewSizePreference.UseNone;
                bool modeSwitched = await ApplicationView.GetForCurrentView().TryEnterViewModeAsync(ApplicationViewMode.CompactOverlay, compactOptions);
                //compactOverlayButton.Visibility = Visibility.Collapsed;
                //use also titlebar space
                CoreApplication.GetCurrentView().TitleBar.ExtendViewIntoTitleBar = true;
                //make whole window dragable
                Window.Current.SetTitleBar(ContentArea);

            }
            Window.Current.Activated += Current_Activated;


        }



        private async void Current_Activated(object sender, Windows.UI.Core.WindowActivatedEventArgs e)
        {
            if (e.WindowActivationState == CoreWindowActivationState.Deactivated)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(1600));
                PrimaryCommandBar.Visibility = Visibility.Collapsed;
            }
            else
            {
                PrimaryCommandBar.Visibility = Visibility.Visible;
            }
        }

        private void OnElementClicked(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            var selectedFlyoutItem = sender as AppBarButton;
            //SelectedOptionText.Text = "You clicked: " + (sender as AppBarButton).Label;
            //PrimaryCommandBar.Visibility = Visibility.Collapsed;
            NavigationService.Navigate(typeof(MainPage), null, new SuppressNavigationTransitionInfo());
            //NavigationService.GoBack();
        }

        protected override async void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            await cameraControl.CleanupCameraAsync();
            CoreApplication.GetCurrentView().TitleBar.ExtendViewIntoTitleBar = false;
            //Window.Current.SetTitleBar(null);
            bool modeSwitched = await ApplicationView.GetForCurrentView().TryEnterViewModeAsync(ApplicationViewMode.Default);
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

        private void CameraControl_Loaded(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {

        }


    }
}

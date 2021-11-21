using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

using WebcamOnDesktop.Helpers;
using WebcamOnDesktop.Services;

using Windows.ApplicationModel;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace WebcamOnDesktop.Views
{
    // TODO WTS: Add other settings as necessary. For help see https://github.com/Microsoft/WindowsTemplateStudio/blob/release/docs/UWP/pages/settings-codebehind.md
    // TODO WTS: Change the URL for your privacy policy in the Resource File, currently set to https://YourPrivacyUrlGoesHere
    public sealed partial class SettingsPage : Page, INotifyPropertyChanged
    {
        private ElementTheme _elementTheme = ThemeSelectorService.Theme;

        public ElementTheme ElementTheme
        {
            get { return _elementTheme; }

            set { Set(ref _elementTheme, value); }
        }

        private string _versionDescription;

        public string VersionDescription
        {
            get { return _versionDescription; }

            set { Set(ref _versionDescription, value); }
        }

        //setting hide background (experimental)
        private bool _hideBackground = false;

        public bool HideBackground
        {
            get { return _hideBackground; }
            set { Set(ref _hideBackground, value); }
        }

        //setting horizontal flip
        private bool _flipHorizontal = true;

        public bool FlipHorizontal
        {
            get { return _flipHorizontal; }
            set { Set(ref _flipHorizontal, value); }
        }

        //setting vertical flip
        private bool _flipVertical = false;

        public bool FlipVertical
        {
            get { return _flipVertical; }
            set { Set(ref _flipVertical, value); }
        }


        public SettingsPage()
        {
            InitializeComponent();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            await InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            VersionDescription = GetVersionDescription();
            HideBackground = await ApplicationData.Current.LocalSettings.ReadAsync<bool>("HideBackground");
            FlipHorizontal = await ApplicationData.Current.LocalSettings.ReadAsync<bool>("FlipHorizontal");
            FlipVertical = await ApplicationData.Current.LocalSettings.ReadAsync<bool>("FlipVertical");

            await Task.CompletedTask;
        }

        private string GetVersionDescription()
        {
            var appName = "AppDisplayName".GetLocalized();
            //var appName = "AppDisplayName";
            var package = Package.Current;
            var packageId = package.Id;
            var version = packageId.Version;

            return $"{appName} - {version.Major}.{version.Minor}.{version.Build}.{version.Revision}";
        }

        private async void ThemeChanged_CheckedAsync(object sender, RoutedEventArgs e)
        {
            var param = (sender as RadioButton)?.CommandParameter;

            if (param != null)
            {
                await ThemeSelectorService.SetThemeAsync((ElementTheme)param);
            }
        }

        //direct update of settings via XAML checkbox
        //settingskey must be passed as Checkbox CommandParameter
        private async void SettingChanged_CheckedAsync(object sender, RoutedEventArgs e)
        {
            var settingsKey = (sender as CheckBox)?.CommandParameter;
            var settingsVal = (sender as CheckBox)?.IsChecked;

            if (settingsKey != null && settingsVal!= null)
            {
                await ApplicationData.Current.LocalSettings.SaveAsync(settingsKey.ToString(), settingsVal.ToString());
            }
        }


        public event PropertyChangedEventHandler PropertyChanged;

        //used to update the visual elements on value change
        private void Set<T>(ref T storage, T value, [CallerMemberName]string propertyName = null)
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

using System;
using WebcamOnDesktop.Helpers;
using WebcamOnDesktop.Services;
using Windows.ApplicationModel.Activation;
using Windows.UI.Xaml;
using Windows.Storage;

namespace WebcamOnDesktop
{
    public sealed partial class App : Application
    {
        private Lazy<ActivationService> _activationService;

        private ActivationService ActivationService
        {
            get { return _activationService.Value; }
        }

        public App()
        {
            InitializeComponent();
            UnhandledException += OnAppUnhandledException;

            // Deferred execution until used. Check https://docs.microsoft.com/dotnet/api/system.lazy-1 for further info on Lazy<T> class.
            _activationService = new Lazy<ActivationService>(CreateActivationService);
        }

        protected override async void OnLaunched(LaunchActivatedEventArgs args)
        {
            //set defaults if app is started for the first time or the app was previously killed in an unexpected way
            /*if (args.PreviousExecutionState == ApplicationExecutionState.NotRunning ) 
            {
                //do some initial stuff
            }
            */
            
            //set initial values
            //remark: it is the only way I know to detect if a setting has not been set before -> LocalSettings.ReadAsync does not return NULL :(
            ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
            if (localSettings.Values["FlipHorizontal"] == null) { await ApplicationData.Current.LocalSettings.SaveAsync("FlipHorizontal", true.ToString()); }
            if (localSettings.Values["FlipVertical"] == null) { await ApplicationData.Current.LocalSettings.SaveAsync("FlipVertical", false.ToString()); }
                       

            if (!args.PrelaunchActivated)
            {
                await ActivationService.ActivateAsync(args);
            }
        }

        protected override async void OnActivated(IActivatedEventArgs args)
        {
            await ActivationService.ActivateAsync(args);
        }

        private void OnAppUnhandledException(object sender, Windows.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            // TODO WTS: Please log and handle the exception as appropriate to your scenario
            // For more info see https://docs.microsoft.com/uwp/api/windows.ui.xaml.application.unhandledexception
        }

        private ActivationService CreateActivationService()
        {
            return new ActivationService(this, typeof(Views.MainPage), new Lazy<UIElement>(CreateShell));
        }

        private UIElement CreateShell()
        {
            return new Views.ShellPage();
        }
    }
}

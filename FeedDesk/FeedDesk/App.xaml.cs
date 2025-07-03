using System.Collections;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json.Nodes;
using XmlClients.Core.Contracts.Services;
using XmlClients.Core.Helpers;
using XmlClients.Core.Services;
using FeedDesk.Activation;
using FeedDesk.Contracts.Services;
using FeedDesk.Services;
using FeedDesk.ViewModels;
using FeedDesk.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Xaml;
using Windows.Storage;

namespace FeedDesk;

public partial class App : Application
{
    // AppDataFolder
    //private static readonly ResourceLoader _resourceLoader = new();
    private static readonly string _appName = "FeedDesk";//_resourceLoader.GetString("AppName");
    private static readonly string _appDeveloper = "torum";
    private static readonly string _envDataFolder = System.Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
    public static readonly string AppName = _appName;
    public static string AppDataFolder { get; } = _envDataFolder + System.IO.Path.DirectorySeparatorChar + _appDeveloper + System.IO.Path.DirectorySeparatorChar + _appName;
    public static string AppConfigFilePath { get; } = Path.Combine(AppDataFolder, _appName + ".config");


    // DispatcherQueue
    private static readonly Microsoft.UI.Dispatching.DispatcherQueue _currentDispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
    public static Microsoft.UI.Dispatching.DispatcherQueue CurrentDispatcherQueue => _currentDispatcherQueue;

    // ErrorLog
    public bool IsSaveErrorLog = true;
    public string LogFilePath = System.Environment.GetFolderPath(Environment.SpecialFolder.Desktop) + System.IO.Path.DirectorySeparatorChar + "FeedDesk_errors.txt";
    private readonly StringBuilder Errortxt = new();

    public const string BackdropSettingsKey = "AppSystemBackdropOption";

    // MainWindow
    public static WindowEx MainWindow { get; } = new MainWindow();
    public static UIElement? AppTitlebar
    {
        get; set;
    }

    public IHost Host
    {
        get;
    }

    public static T GetService<T>()
        where T : class
    {
        if ((App.Current as App)!.Host.Services.GetService(typeof(T)) is not T service)
        {
            throw new ArgumentException($"{typeof(T)} needs to be registered in ConfigureServices within App.xaml.cs.");
        }

        return service;
    }

    public App()
    {
        // Only works in packaged environment.
        //Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride = "en-US";
        //System.Globalization.CultureInfo.CurrentUICulture = new System.Globalization.CultureInfo("en-US", false);
        //Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride = "ja-JP";
        //System.Globalization.CultureInfo.CurrentUICulture = new System.Globalization.CultureInfo( "ja-JP", false );

        // Force theme
        //this.RequestedTheme = ApplicationTheme.Dark;
        //this.RequestedTheme = ApplicationTheme.Light;

        InitializeComponent();

        Host = Microsoft.Extensions.Hosting.Host.
        CreateDefaultBuilder().
        UseContentRoot(AppContext.BaseDirectory).
        ConfigureServices((context, services) =>
        {
            // Default Activation Handler
            services.AddTransient<ActivationHandler<LaunchActivatedEventArgs>, DefaultActivationHandler>();

            // Other Activation Handlers
            //services.AddTransient<IActivationHandler, AppNotificationActivationHandler>();

            // Services
            //services.AddSingleton<IAppNotificationService, AppNotificationService>();
            //services.AddSingleton<ILocalSettingsService, LocalSettingsService>();
            services.AddSingleton<IThemeSelectorService, ThemeSelectorService>();
            //services.AddTransient<IWebViewService, WebViewService>();
            services.AddSingleton<IActivationService, ActivationService>();
            services.AddSingleton<IPageService, PageService>();
            services.AddSingleton<INavigationService, NavigationService>();
            //services.AddTransient<INavigationViewService, NavigationViewService>();

            // Core Services
            //services.AddSingleton<IFileService, FileService>();
            services.AddTransient<IFileDialogService, FileDialogService>();
            services.AddSingleton<IDataAccessService, DataAccessService>();
            services.AddSingleton<IFeedClientService, FeedClientService>();
            services.AddSingleton<IAutoDiscoveryService, AutoDiscoveryService>();
            services.AddSingleton<IOpmlService, OpmlService>();

            // Views and ViewModels
            services.AddSingleton<SettingsViewModel>();
            services.AddSingleton<SettingsPage>();
            services.AddTransient<FeedAddViewModel>();
            services.AddTransient<FeedAddPage>();
            services.AddTransient<FeedEditViewModel>();
            services.AddTransient<FeedEditPage>();
            services.AddTransient<FolderEditViewModel>();
            services.AddTransient<FolderEditPage>();
            services.AddTransient<FolderAddViewModel>();
            services.AddTransient<FolderAddPage>();
            services.AddSingleton<MainViewModel>();
            services.AddSingleton<MainPage>();
            services.AddSingleton<ShellPage>();
            services.AddSingleton<ShellViewModel>();

            // Configuration
            //services.Configure<LocalSettingsOptions>(context.Configuration.GetSection(nameof(LocalSettingsOptions)));
        }).
        Build();

        //App.GetService<IAppNotificationService>().Initialize();

        // 
        Microsoft.UI.Xaml.Application.Current.UnhandledException += App_UnhandledException;
        TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
    }

    protected async override void OnLaunched(LaunchActivatedEventArgs args)
    {
        base.OnLaunched(args);

        // Single instance.
        // https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/migrate-to-windows-app-sdk/guides/applifecycle
        var mainInstance = Microsoft.Windows.AppLifecycle.AppInstance.FindOrRegisterForKey("FeedDeskMain");
        // If the instance that's executing the OnLaunched handler right now
        // isn't the "main" instance.
        if (!mainInstance.IsCurrent)
        {
            // Redirect the activation (and args) to the "main" instance, and exit.
            var activatedEventArgs = Microsoft.Windows.AppLifecycle.AppInstance.GetCurrent().GetActivatedEventArgs();
            await mainInstance.RedirectActivationToAsync(activatedEventArgs);

            System.Diagnostics.Process.GetCurrentProcess().Kill();
            return;
        }
        else
        {
            // Otherwise, register for activation redirection
            Microsoft.Windows.AppLifecycle.AppInstance.GetCurrent().Activated += App_Activated;
        }




        // WinUIEx Storage option.
        if (!RuntimeHelper.IsMSIX)
        {
            // Create if not exists.
            if (!Directory.Exists(AppDataFolder))
            {
                Directory.CreateDirectory(AppDataFolder);
            }

            WinUIEx.WindowManager.PersistenceStorage = new FilePersistence(Path.Combine(AppDataFolder, "WinUIExPersistence.json"));
        }
        
        // Nortification example.
        //App.GetService<IAppNotificationService>().Show(string.Format("AppNotificationSamplePayload".GetLocalized(), AppContext.BaseDirectory));

        await App.GetService<IActivationService>().ActivateAsync(args);
    }

    private void App_Activated(object? sender, Microsoft.Windows.AppLifecycle.AppActivationArguments e)
    {
        CurrentDispatcherQueue?.TryEnqueue(() =>
        {
            MainWindow.Activate();
            MainWindow.BringToFront();
        });
    }

    #region == UnhandledException ==

    private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        // TODO: Log and handle exceptions as appropriate.
        // https://docs.microsoft.com/windows/windows-app-sdk/api/winrt/microsoft.ui.xaml.application.unhandledexception.

        // This does not fire...because of winui3 bugs. should be fixed in v1.2.2 WinAppSDK
        // see https://github.com/microsoft/microsoft-ui-xaml/issues/5221

        Debug.WriteLine("App_UnhandledException", e.Message + $"StackTrace: {e.Exception.StackTrace}, Source: {e.Exception.Source}");
        AppendErrorLog("App_UnhandledException", e.Message + $"StackTrace: {e.Exception.StackTrace}, Source: {e.Exception.Source}");

        try
        {
            SaveErrorLog();
        }
        catch (Exception) { }
    }

    private void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        if (e.Exception.InnerException is not Exception exception)
        {
            return;
        }

        Debug.WriteLine("TaskScheduler_UnobservedTaskException: " + exception.Message);
        AppendErrorLog("TaskScheduler_UnobservedTaskException", exception.Message);
        SaveErrorLog();

        e.SetObserved();
    }

    private void CurrentDomain_UnhandledException(object sender, System.UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is not Exception exception)
        {
            return;
        }

        if (exception is TaskCanceledException)
        {
            // can ignore.
            Debug.WriteLine("CurrentDomain_UnhandledException (TaskCanceledException): " + exception.Message);
            AppendErrorLog("CurrentDomain_UnhandledException (TaskCanceledException)", exception.Message);
        }
        else
        {
            Debug.WriteLine("CurrentDomain_UnhandledException: " + exception.Message);
            AppendErrorLog("CurrentDomain_UnhandledException", exception.Message);
            SaveErrorLog();
        }
    }

    public void AppendErrorLog(string kindTxt, string errorTxt)
    {
        Errortxt.AppendLine(kindTxt + ": " + errorTxt);
        var dt = DateTime.Now;
        Errortxt.AppendLine($"Occured at {dt.ToString("yyyy/MM/dd HH:mm:ss")}");
        Errortxt.AppendLine("");
    }

    public void SaveErrorLog()
    {
        if (!IsSaveErrorLog)
        {
            return;
        }

        if (string.IsNullOrEmpty(LogFilePath))
        {
            return;
        }

        if (Errortxt.Length > 0)
        {
            Errortxt.AppendLine("");
            var dt = DateTime.Now;
            Errortxt.AppendLine($"Saved at {dt.ToString("yyyy/MM/dd HH:mm:ss")}");

            var s = Errortxt.ToString();
            if (!string.IsNullOrEmpty(s))
            {
                File.WriteAllText(LogFilePath, s);
            }
        }
    }

    #endregion

    #region == FilePersistence for WinUIEx ==

    private class FilePersistence : IDictionary<string, object>
    {
        private readonly Dictionary<string, object> _data = [];
        private readonly string _file;

        public FilePersistence(string filename)
        {
            _file = filename;
            try
            {
                if (File.Exists(filename))
                {
                    if (JsonNode.Parse(File.ReadAllText(filename)) is JsonObject jo)
                    {
                        foreach (var node in jo)
                        {
                            if (node.Value is JsonValue jvalue && jvalue.TryGetValue<string>(out var value))
                            {
                                _data[node.Key] = value;
                            }
                        }
                    }
                }
            }
            catch { }
        }
        private void Save()
        {
            var jo = new JsonObject();
            foreach (var item in _data)
            {
                if (item.Value is string s) // In this case we only need string support. TODO: Support other types
                {
                    jo.Add(item.Key, s);
                }
            }
            File.WriteAllText(_file, jo.ToJsonString());
        }
        public object this[string key] { get => _data[key]; set { _data[key] = value; Save(); } }

        public ICollection<string> Keys => _data.Keys;

        public ICollection<object> Values => _data.Values;

        public int Count => _data.Count;

        public bool IsReadOnly => false;

        public void Add(string key, object value)
        {
            _data.Add(key, value); Save();
        }

        public void Add(KeyValuePair<string, object> item)
        {
            _data.Add(item.Key, item.Value); Save();
        }

        public void Clear()
        {
            _data.Clear(); Save();
        }

        public bool Contains(KeyValuePair<string, object> item) => _data.Contains(item);

        public bool ContainsKey(string key) => _data.ContainsKey(key);

        public void CopyTo(KeyValuePair<string, object>[] array, int arrayIndex) => throw new NotImplementedException();

        public IEnumerator<KeyValuePair<string, object>> GetEnumerator() => throw new NotImplementedException();

        public bool Remove(string key) => throw new NotImplementedException();

        public bool Remove(KeyValuePair<string, object> item) => throw new NotImplementedException();

        public bool TryGetValue(string key, [MaybeNullWhen(false)] out object value) => throw new NotImplementedException();

        IEnumerator IEnumerable.GetEnumerator() => throw new NotImplementedException();
    }

    #endregion
}

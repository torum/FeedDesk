using FeedDesk.Services;
using FeedDesk.Services.Contracts;
using FeedDesk.ViewModels;
using FeedDesk.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WinRT;


// https://github.com/CommunityToolkit/Windows/issues/612
[assembly: GeneratedWinRTExposedExternalType(typeof(GridLength))]


namespace FeedDesk;

public partial class App
{
    // AppDataFolder
    private static readonly string AppDeveloper = "torum";
    private static readonly string EnvDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
    public static readonly string AppName = "FeedDesk";//_resourceLoader.GetString("AppName");
    public static string AppDataFolder { get; } = EnvDataFolder + Path.DirectorySeparatorChar + AppDeveloper + Path.DirectorySeparatorChar + AppName;
    public static string AppConfigFilePath { get; } = Path.Combine(AppDataFolder, AppName + ".config");

    // ErrorLog
#if DEBUG
    public bool IsSaveErrorLog = true;
#else
    public bool IsSaveErrorLog = false;
#endif
    public string LogFilePath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop) + Path.DirectorySeparatorChar + "FeedDesk_errors.txt";
    private readonly StringBuilder _errortxt = new();
    private readonly Lock _errorLogLock = new();
    private Task? _appHostStartTask;
    private Task? _appHostStopTask;

    public Task? AppHostStopTask
    {
        get => _appHostStopTask;
        set => _appHostStopTask = value;
    }
    private bool _isAppHostStopping = false;
    public bool IsAppHostStopping
    {
        get => (Current as App)?._isAppHostStopping ?? false;
        set
        {
            if (Current is App app)
            {
                app._isAppHostStopping = value;
            }
        }
    }
    private bool _allowWindowClose;
    public bool IsAllowWindowClose
    {
        get => (Current as App)?._allowWindowClose ?? false;
        set
        {
            if (Current is App app)
            {
                app._allowWindowClose = value;
            }
        }
    }
    private bool _shutdownCloseStarted;
    public bool IsShutdownCloseStarted
    {
        get => (Current as App)?._shutdownCloseStarted ?? false;
        set
        {
            if (Current is App app)
            {
                app._shutdownCloseStarted = value;
            }
        }
    }

    //public const string BackdropSettingsKey = "AppSystemBackdropOption";

    public static UIElement? AppTitlebar
    {
        get; set;
    }

    // MainWindow
    public static MainWindow? MainWnd
    {
        get; private set;
    }

    public Microsoft.UI.Dispatching.DispatcherQueue? CurrentDispatcherQueue { get; private set; }

    public IHost Host
    {
        get;
    }

    public static T GetService<T>()
        where T : class
    {
        if ((Current as App)!.Host.Services.GetService(typeof(T)) is not T service)
        {
            throw new ArgumentException($"{typeof(T)} needs to be registered in ConfigureServices within App.xaml.cs.");
        }

        return service;
    }

    public App()
    {
        CurrentDispatcherQueue = DispatcherQueue.GetForCurrentThread();

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
                // Core Services
                services.AddSingleton<IFileDialogService, FileDialogService>();
                services.AddSingleton<IDataAccessService, DataAccessService>();
                //services.AddSingleton<IFeedClientService, FeedClientService>();
                services.AddSingleton<IAutoDiscoveryService, AutoDiscoveryService>();
                services.AddSingleton<IOpmlService, OpmlService>();
                services.AddSingleton<IDispatcherService>(new DispatcherService(CurrentDispatcherQueue));
                services.AddSingleton<IFeedRefreshService, FeedRefreshService>();
                services.AddSingleton<IFeedTreePersistenceService, FeedTreePersistenceService>();
                services.AddHttpClient(FeedClientService.HttpClientName, client =>
                {
                    // Use the per-request cancellation token as the single timeout mechanism.
                    client.Timeout = Timeout.InfiniteTimeSpan;
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/xml"));
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/xml"));
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/atom+xml"));
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/rss+xml"));
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/rdf+xml"));
                    client.DefaultRequestHeaders.UserAgent.ParseAdd("FeedDesk/1.0");
                }).ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
                    {
                        ConnectTimeout = TimeSpan.FromSeconds(15)
                    });

                services.AddSingleton<IFeedClientService, FeedClientService>();

                // Views and ViewModels
                services.AddSingleton<SettingsPage>();
                services.AddTransient<FeedAddViewModel>();
                services.AddTransient<FeedAddPage>();
                services.AddSingleton<FeedEditPage>();
                services.AddSingleton<FolderEditPage>();
                services.AddSingleton<FolderAddPage>();
                services.AddSingleton<MainWindow>();
                services.AddSingleton<MainViewModel>();
                services.AddSingleton<MainPage>();
                services.AddSingleton<ShellPage>();

                // Configuration
                //services.Configure<LocalSettingsOptions>(context.Configuration.GetSection(nameof(LocalSettingsOptions)));
            }).
            Build();


        Current.UnhandledException += App_UnhandledException;
        TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
    }

    protected async override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
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

            Process.GetCurrentProcess().Kill();
            return;
        }
        else
        {
            // Otherwise, register for activation redirection
            Microsoft.Windows.AppLifecycle.AppInstance.GetCurrent().Activated += App_Activated;
        }

        _appHostStartTask = StartAppHostAsync();
        MainWnd = GetService<MainWindow>();
        /*
        MainWnd.AppWindow.Closing += (sender, e) =>
        {
            _appHostStopTask = StopAppHostAsync();
        };
        */
        //MainWnd.AppWindow.Show(true);
        MainWnd.Activate();
        MainWnd.AppWindow.MoveInZOrderAtTop();
    }

    private async Task StartAppHostAsync()
    {
        try
        {
            await Host.StartAsync();
        }
        catch (Exception ex)
        {
            AppendErrorLog("AppHost.StartAsync", ex.ToString());
            SaveErrorLog();
        }
    }

    public async Task StopAppHostAndCloseWindowAsync()
    {
        var progressRing = new ProgressRing
        {
            IsActive = true,
            Width = 32,
            Height = 32
        };

        var content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12
        };
        content.Children.Add(progressRing);
        content.Children.Add(new TextBlock
        {
            Text = "Stopping background operations…",
            VerticalAlignment = VerticalAlignment.Center
        });

        var dialog = new ContentDialog
        {
            Title = "Closing FeedDesk",
            Content = content,
            XamlRoot = MainWnd!.Content.XamlRoot
        };

        // Prevent the user from dismissing the dialog before cleanup finishes.
        dialog.Closing += (_, e) =>
        {
            if (!_allowWindowClose)
            {
                e.Cancel = true;
            }
        };

        var dialogOperation = dialog.ShowAsync();

        try
        {
            await StopAppHostAsync();
        }
        catch (Exception ex)
        {
            LogShutdownTimestamp($"Shutdown failed: {ex}");
            AppendErrorLog("App shutdown", ex.ToString());
            SaveErrorLog();
        }
        finally
        {
            _allowWindowClose = true;

            try
            {
                dialog.Hide();
                await dialogOperation;
            }
            finally
            {
                //MainWnd?.Close();
            }
        }
    }

    private static void LogShutdownTimestamp(string message)
    {
        Debug.WriteLine($"[{DateTimeOffset.UtcNow:O}] {message}");
    }

    private async Task StopAppHostAsync()
    {
        LogShutdownTimestamp("Shutdown: started");

        if (_appHostStartTask != null)
        {
            LogShutdownTimestamp("Shutdown: waiting for Host.StartAsync");
            await _appHostStartTask;
            LogShutdownTimestamp("Shutdown: Host.StartAsync completed");
        }

        _isAppHostStopping = true;

        LogShutdownTimestamp("Shutdown: saving window settings");
        MainWnd?.SaveSettings();

        var vm = GetService<MainViewModel>();

        LogShutdownTimestamp("Shutdown: saving feed tree");
        vm.SaveServiceXml();

        LogShutdownTimestamp("Shutdown: canceling background operations");
        vm.BeginShutdown();

        try
        {
            LogShutdownTimestamp("Shutdown: Host.StopAsync started");
            await Host.StopAsync();
            LogShutdownTimestamp("Shutdown: Host.StopAsync completed");

            LogShutdownTimestamp("Shutdown: waiting for background operations");
            await vm.WaitForBackgroundOperationsAsync();
            LogShutdownTimestamp("Shutdown: background operations completed");
        }
        catch (Exception ex)
        {
            LogShutdownTimestamp($"Shutdown: exception: {ex}");
            AppendErrorLog("AppHost.StopAsync", ex.ToString());
            SaveErrorLog();
        }
        finally
        {
            try
            {
                LogShutdownTimestamp("Shutdown: Host.Dispose started");
                Host.Dispose();
                LogShutdownTimestamp("Shutdown: Host.Dispose completed");
            }
            catch (Exception ex)
            {
                LogShutdownTimestamp($"Shutdown: Host.Dispose exception: {ex}");
                Debug.WriteLine($"Exception disposing Host: {ex}");
            }
        }
    }

    private void App_Activated(object? sender, Microsoft.Windows.AppLifecycle.AppActivationArguments e)
    {
        LogShutdownTimestamp($"Activation received; kind={e.Kind}; stopping={_isAppHostStopping}");

        if (_isAppHostStopping)
        {
            LogShutdownTimestamp("Activation ignored because shutdown is in progress");
            return;
        }

        var wasQueued = CurrentDispatcherQueue?.TryEnqueue(() =>
        {
            var window = MainWnd;
            if (window is null)
            {
                LogShutdownTimestamp("Activation dispatched, but MainWnd is null");
                return;
            }

            window.Activate();
            LogShutdownTimestamp("Activation handled; MainWnd.Activate called");
        }) ?? false;

        LogShutdownTimestamp(wasQueued
            ? "Activation callback queued"
            : "Activation callback could not be queued");
    }

    #region == UnhandledException ==

    private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        // TODO: Log and handle exceptions as appropriate.
        // https://docs.microsoft.com/windows/windows-app-sdk/api/winrt/microsoft.ui.xaml.application.unhandledexception.

        // This does not fire...because of winui3 bugs. should be fixed in v1.2.2 WinAppSDK
        // see https://github.com/microsoft/microsoft-ui-xaml/issues/5221

        Debug.WriteLine("App_UnhandledException", e.Message + $" StackTrace: {e.Exception.StackTrace}, Source: {e.Exception.Source}");
        AppendErrorLog("App_UnhandledException", e.Message + $" StackTrace: {e.Exception.StackTrace}, Source: {e.Exception.Source}");
        if (e.Exception.InnerException is TaskCanceledException or OperationCanceledException)
        {
            // can ignore.
            Debug.WriteLine("App_UnhandledException (TaskCanceledException): " + e.Exception.Message);
            //AppendErrorLog("App_UnhandledException (TaskCanceledException)", e.Exception.Message);
        }
        else
        {
            Debug.WriteLine("App_UnhandledException: " + e.Exception.Message);
            AppendErrorLog("App_UnhandledException", e.Exception.Message);
            SaveErrorLog();
        }

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
        lock (_errorLogLock)
        {
            _errortxt.AppendLine(kindTxt + ": " + errorTxt);
            var dt = DateTime.Now;
            _errortxt.AppendLine($"Occured at {dt:yyyy/MM/dd HH:mm:ss}");
            _errortxt.AppendLine();
        }
    }

    public void SaveErrorLog()
    {
        lock (_errorLogLock)
        {
            if (!IsSaveErrorLog || string.IsNullOrEmpty(LogFilePath) || _errortxt.Length == 0)
            {
                return;
            }

            var dt = DateTime.Now;
            _errortxt.AppendLine();
            _errortxt.AppendLine($"Saved at {dt:yyyy/MM/dd HH:mm:ss}");

            File.WriteAllText(LogFilePath, _errortxt.ToString());
        }
    }

    #endregion
}

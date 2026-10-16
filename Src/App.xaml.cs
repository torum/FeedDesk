using FeedDesk.Helpers;
using FeedDesk.Services;
using FeedDesk.Services.Contracts;
using FeedDesk.ViewModels;
using FeedDesk.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WinRT;
using Windows.Storage;

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
    private readonly CancellationTokenSource _cts = new();
    public CancellationTokenSource AppCancellationToken => _cts;
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

    private bool _allowWindowClose;
    public static bool IsAllowWindowClose
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
    public static bool IsShutdownCloseStarted
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

    public static T GetService<T>() where T : class
    {
        return ((App)Current).Host.Services.GetRequiredService<T>();
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
                services.AddSingleton<IAutoDiscoveryService, AutoDiscoveryService>();
                services.AddSingleton<IOpmlService, OpmlService>();
                services.AddSingleton<IDispatcherService>(new DispatcherService(CurrentDispatcherQueue));
                services.AddSingleton<IFeedRefreshService, FeedRefreshService>();
                services.AddSingleton<IFeedTreePersistenceService, FeedTreePersistenceService>();
                services.AddHttpClient(FeedClientService.HttpClientName, client =>
                {
                    // Addition to this timeout, also use the per-request cancellation token as the fallback timeout mechanism.
                    // Though, not a good idea to set timeout here to avoid disposable race. 
                    client.Timeout = TimeSpan.FromSeconds(10);//Timeout.InfiniteTimeSpan;//
                    // Preemptively close the connection to avoid IOExceptions caused by remote server silently tears down an idle TLS/socket connection.
                    // .NET's internal reading loops encounter expected IOExceptions or SocketExceptions on server side closed idle keep-alive sockets.
                    client.DefaultRequestHeaders.ConnectionClose = true;
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/xml"));
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/xml"));
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/atom+xml"));
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/rss+xml"));
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/rdf+xml"));
                    client.DefaultRequestHeaders.UserAgent.ParseAdd("FeedDesk/1.2");
                }).ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
                {
                    ConnectTimeout = TimeSpan.FromSeconds(8),
                    PooledConnectionLifetime = TimeSpan.FromMinutes(5),
                    PooledConnectionIdleTimeout = TimeSpan.FromMinutes(1),
                    SslOptions = new System.Net.Security.SslClientAuthenticationOptions
                    {
                        // Optional: nothing to change here by default; handshake time is bounded by ConnectTimeout.
                    }
                });
                services.AddSingleton<IFeedClientService, FeedClientService>();

                services.AddSingleton<IConfigurationService, ConfigurationService>();

                // Views and ViewModels
                services.AddSingleton<MainViewModel>();
                services.AddSingleton<MainWindow>();
                services.AddSingleton<ShellPage>();
                services.AddSingleton<MainPage>();
                services.AddTransient<FeedAddViewModel>();
                /*
                services.AddSingleton<SettingsPage>();
                services.AddTransient<FeedAddPage>();
                services.AddSingleton<FeedEditPage>();
                services.AddSingleton<FolderEditPage>();
                services.AddSingleton<FolderAddPage>();
                */
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
        try
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

                //Process.GetCurrentProcess().Kill();
                Environment.Exit(0);
                return;
            }
            else
            {
                // Otherwise, register for activation redirection
                Microsoft.Windows.AppLifecycle.AppInstance.GetCurrent().Activated += App_Activated;
            }

            var configurationService = GetService<IConfigurationService>();
            var filePath = App.AppConfigFilePath;
            if (RuntimeHelper.IsMSIX)
            {
                filePath = Path.Combine(ApplicationData.Current.LocalFolder.Path, App.AppName + ".config");
            }
            configurationService.Initialize(filePath);
            await configurationService.LoadConfigurationAsync();

            _appHostStartTask = StartAppHostAsync();
            MainWnd = GetService<MainWindow>();

            //MainWnd.AppWindow.Show(true);
            MainWnd.Activate();
            MainWnd.AppWindow.MoveInZOrderAtTop();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"OnLaunched: {ex}");
            AppendErrorLog("OnLaunched", ex.ToString());
            SaveErrorLog();

            // Startup failed and the app is in an unknown state, so exit instead of leaving a half-initialized process.
            Environment.Exit(1);
        }   

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

    private static void LogShutdownTimestamp(string message)
    {
        Debug.WriteLine($"[{DateTimeOffset.UtcNow:O}] {message}");
    }

    public async Task ShutdownAndCloseAsync()
    {
        LogShutdownTimestamp("Shutdown: ShutdownAndCloseAsync started");

        _isAppHostStopping = true;

        try
        {
            var vm = GetService<MainViewModel>();

            LogShutdownTimestamp("Shutdown: saving feed tree");
            vm.SaveServiceXml();

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

            // This is weird WinUI3 bug, but calling MainWnd.Close() here WHILE MINIMIZED causes the app to hang on shutdown.
            /*
            LogShutdownTimestamp($"Shutdown: starting Close()");
            // Window.Close() must run on the UI thread.
            var queued = CurrentDispatcherQueue?.TryEnqueue(() => MainWnd?.Close()) ?? false;
            if (!queued)
            {
                LogShutdownTimestamp($"Shutdown: Close() failed,starting Exit()");
                Exit();
            }
            */
            LogShutdownTimestamp("Shutdown: ShutdownAndCloseAsync completed");
        }
    }

    private async Task StopAppHostAsync()
    {
        LogShutdownTimestamp("Shutdown: StopAppHostAsync() started");

        if (_appHostStartTask != null)
        {
            LogShutdownTimestamp("Shutdown: waiting for Host.StartAsync");
            await _appHostStartTask;
            LogShutdownTimestamp("Shutdown: Host.StartAsync completed");
        }

        var vm = GetService<MainViewModel>();

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
                LogShutdownTimestamp("Shutdown: CancellationTokenSource disposed started");
                _cts?.Dispose();
                LogShutdownTimestamp("Shutdown: CancellationTokenSource disposed completed");

                LogShutdownTimestamp("Shutdown: Host.Dispose started");
                Host.Dispose();
                LogShutdownTimestamp("Shutdown: Host.Dispose completed");
            }
            catch (Exception ex)
            {
                LogShutdownTimestamp($"Shutdown: Host.Dispose exception: {ex}");
                Debug.WriteLine($"Exception disposing Host: {ex}");

                AppendErrorLog("AppHost.StopAsync @Dispose()", ex.ToString());
            }
            finally
            {
                // Save error log if any.
                SaveErrorLog();

                LogShutdownTimestamp("Shutdown: StopAppHostAsync() completed");
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

        // e.Exception can lose detail when it crosses the WinRT boundary,
        // but ToString() still gives the best available type, message, HResult and stack trace.
        var details = $"{e.Exception}{Environment.NewLine}HResult: 0x{e.Exception.HResult:X8}{Environment.NewLine}Message: {e.Message}";

        Debug.WriteLine($"App_UnhandledException: {details}");
        AppendErrorLog("App_UnhandledException", details);
        SaveErrorLog();

        // Mark as handled only for exceptions that are known to be recoverable.
        // Leaving it unhandled lets the app terminate, which is safer for unknown states.
        if (e.Exception is OperationCanceledException)
        {
            e.Handled = true;
        }
    }

    private void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        // Flatten so nested AggregateExceptions are unwrapped, and log full details (type, message, stack trace).
        var details = e.Exception.Flatten().ToString();

        Debug.WriteLine($"TaskScheduler_UnobservedTaskException: {details}");
        AppendErrorLog("TaskScheduler_UnobservedTaskException", details);
        SaveErrorLog();

        e.SetObserved();
    }

    private void CurrentDomain_UnhandledException(object sender, System.UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is not Exception exception)
        {
            var raw = e.ExceptionObject?.ToString() ?? "Unknown exception object";
            Debug.WriteLine($"CurrentDomain_UnhandledException: {raw}");
            AppendErrorLog("CurrentDomain_UnhandledException", raw);
            SaveErrorLog();
            return;
        }

        if (exception is TaskCanceledException)
        {
            // can ignore.
            Debug.WriteLine($"CurrentDomain_UnhandledException (TaskCanceledException): {exception.Message}");
            return;
        }

        // Log full details (type, message, stack trace, inner exceptions).
        var details = exception.ToString();

        Debug.WriteLine($"CurrentDomain_UnhandledException (IsTerminating={e.IsTerminating}): {details}");
        AppendErrorLog("CurrentDomain_UnhandledException", details);
        SaveErrorLog();
    }

    public void AppendErrorLog(string kindTxt, string errorTxt)
    {
        lock (_errorLogLock)
        {
            _errortxt.AppendLine(kindTxt + ": " + errorTxt);
            var dt = DateTime.Now;
            _errortxt.AppendLine($"Occurred at {dt:yyyy/MM/dd HH:mm:ss}");
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

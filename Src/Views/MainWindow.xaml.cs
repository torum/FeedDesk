using CommunityToolkit.WinUI;
using FeedDesk.Helpers;
using FeedDesk.Models;
using FeedDesk.Services.Contracts;
using FeedDesk.ViewModels;
using FeedDesk.Views;
using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using System;
using System.Diagnostics;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using Windows.Storage;
using WinRT;

namespace FeedDesk;

public sealed partial class MainWindow : Window
{
    // Window position and size
    // TODO: Change this lator.1920x1080
    private int _winRestoreWidth = 1024;//1024;
    private int _winRestoreHeight = 768;//768;
    private int _winRestoreTop = 100;
    private int _winRestoreLeft = 100;

    //private readonly UISettings settings;
    private ElementTheme theme = ElementTheme.Default;

    private MainViewModel _vm;

    private readonly IConfigurationService _configurationService;

    [DynamicWindowsRuntimeCast(typeof(OverlappedPresenter))]
    public MainWindow(IConfigurationService configurationService)
    {
        _configurationService = configurationService;
        _vm = App.GetService<MainViewModel>();

        LoadSettings();

        InitializeComponent();

        this.Title = "AppDisplayName".GetLocalized();
        this.AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Icons\\FeedDesk3.ico"));

        this.ExtendsContentIntoTitleBar = true;

        // It's important to set content as early as here in order to set theme.
        // But make sure to call LoadSettings() before in order to apply settings value for contents.
        var shell = App.GetService<ShellPage>();
        this.Content = shell;

        // It is necessary to set theme here after the content is set.
        shell.RequestedTheme = theme;

        // Don't do this. This right here interfear the color change in active state change.
        //SetCapitionButtonColorForWin11();

        // Call shelPage now that shellpage is careated.
        shell.SetTitleBar(this);

        this.Closed += Window_Closed;
        this.AppWindow.Closing += AppWindow_Closing;
        this.SizeChanged += Window_SizeChanged;
        this.AppWindow.Changed += AppWindow_Changed;
        this.Activated += MainWindow_Activated;

        if (this.AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.PreferredMinimumWidth = 600;
            presenter.PreferredMinimumHeight = 780;
        }
    }

    [DynamicWindowsRuntimeCast(typeof(OverlappedPresenter))]
    [DynamicWindowsRuntimeCast(typeof(FrameworkElement))]
    private async void AppWindow_Closing(Microsoft.UI.Windowing.AppWindow sender, Microsoft.UI.Windowing.AppWindowClosingEventArgs args)
    {
        Debug.WriteLine("Shutdown: AppWindow_Closing");
        if (App.Current is not App app)
        {
            return;
        }

        // Second pass: cleanup is finished and Close() was called by the app. Let it through.
        if (App.IsAllowWindowClose)
        {
            return;
        }

        // Must be set synchronously, before any await.
        args.Cancel = true;

        if (App.IsShutdownCloseStarted)
        {
            return;
        }

        App.IsShutdownCloseStarted = true;

        // Hide instead of showing a dialog. The window object stays alive while cleanup runs.
        sender.Hide();

        _vm.UpdateSettings();

        #region == Save Configuration ==

        if (!RuntimeHelper.IsMSIX)
        {
            Directory.CreateDirectory(App.AppDataFolder);
        }

        //SaveSettings();
        var config = _configurationService.CurrentConfiguration;

        OverlappedPresenterState? winState = null; // For AOT workaround, don't set default.  = OverlappedPresenterState.Restored;

        //Microsoft.UI.Windowing.AppWindow? appWindow = this.AppWindow;
        if (sender != null)
        {
            // AOT bad
            // https://github.com/microsoft/CsWinRT/issues/1930
            if (sender.Presenter is OverlappedPresenter presenter)
            {
                // AOT bad
                //OverlappedPresenter? presenter = appWindow.Presenter as OverlappedPresenter;

                if (presenter?.State == OverlappedPresenterState.Maximized)
                {
                    winState = OverlappedPresenterState.Maximized;
                }
                else if (presenter?.State == OverlappedPresenterState.Minimized)
                {
                    winState = OverlappedPresenterState.Minimized;
                }
                else if (presenter?.State == OverlappedPresenterState.Restored)
                {
                    winState = OverlappedPresenterState.Restored;
                }
            }
            else
            {
                //App.AppendErrorLog("AOT Error",$"{appWindow.Presenter}"); //Microsoft.UI.Windowing.AppWindowPresenter
                //App.SaveErrorLog();

                try
                {
                    // For AOT tmp workaround
                    OverlappedPresenter presenterAOT = this.AppWindow.Presenter.As<OverlappedPresenter>();

                    if (presenterAOT.State == OverlappedPresenterState.Maximized)
                    {
                        winState = OverlappedPresenterState.Maximized;
                    }
                    else if (presenterAOT.State == OverlappedPresenterState.Minimized)
                    {
                        winState = OverlappedPresenterState.Minimized;
                    }
                    else if (presenterAOT.State == OverlappedPresenterState.Restored)
                    {
                        winState = OverlappedPresenterState.Restored;
                    }
                }
                catch (Exception ex)
                {
                    (Application.Current as App)?.AppendErrorLog("AOT Error@SaveSettings", $"{ex}");
                    (Application.Current as App)?.SaveErrorLog();
                }

            }

            // For AOT, check if winState is null or not.
            if (winState != null)
            {
                // Main Window attributes
                if (winState == OverlappedPresenterState.Restored)
                {
                    config.MainWindow.Width = sender.Size.Width;
                }
                else
                {
                    config.MainWindow.Width = _winRestoreWidth;
                }
                config.MainWindow.RestoreWidth = _winRestoreWidth;

                if (winState == OverlappedPresenterState.Restored)
                {
                    config.MainWindow.Height = sender.Size.Height;
                }
                else
                {
                    config.MainWindow.Height = _winRestoreHeight;
                }
                config.MainWindow.RestoreHeight = _winRestoreHeight;

                if (winState == OverlappedPresenterState.Restored)
                {
                    config.MainWindow.Top = sender.Position.Y;
                }
                else
                {
                    config.MainWindow.Top = _winRestoreTop;
                }
                config.MainWindow.RestoreTop = _winRestoreTop;

                if (winState == OverlappedPresenterState.Restored)
                {
                    config.MainWindow.Left = sender.Position.X;
                }
                else
                {
                    config.MainWindow.Left = _winRestoreLeft;
                }
                config.MainWindow.RestoreLeft = _winRestoreLeft;

                if (winState == OverlappedPresenterState.Maximized)
                {
                    //attrs.Value = "Maximized";
                    config.MainWindow.WindowState = OverlappedPresenterState.Maximized;
                }
                else if (winState == OverlappedPresenterState.Restored)
                {
                    //attrs.Value = "Normal";
                    config.MainWindow.WindowState = OverlappedPresenterState.Restored;
                }
                else if (winState == OverlappedPresenterState.Minimized)
                {
                    //attrs.Value = "Minimized";
                    config.MainWindow.WindowState = OverlappedPresenterState.Restored; // Ignore actual value.
                }
            }
        }

        await _configurationService.SaveConfigurationAsync();

        #endregion

        app.AppHostStopTask = app.ShutdownAndCloseAsync();
    }

    private void Window_Closed(object sender, WindowEventArgs args)
    {
        Debug.WriteLine("Shutdown: Window_Closed");

        this.Closed -= Window_Closed;
        this.AppWindow.Closing -= AppWindow_Closing;
        this.SizeChanged -= Window_SizeChanged;
        this.AppWindow.Changed -= AppWindow_Changed;
        this.Activated -= MainWindow_Activated;
    }

    [DynamicWindowsRuntimeCast(typeof(OverlappedPresenter))]
    private void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        // Ensure that the restore position and sizes are updated when the window is activated, especially after being minimized or restored.
        Microsoft.UI.Windowing.AppWindow? appWindow = this.AppWindow;
        if (appWindow == null)
        {
            return;
        }
        if (appWindow.Presenter is not OverlappedPresenter presenter)
        {
            return;
        }
        if (presenter.State == OverlappedPresenterState.Restored)
        {
            _winRestoreHeight = appWindow.Size.Height;
            _winRestoreWidth = appWindow.Size.Width;
            _winRestoreTop = appWindow.Position.Y;
            _winRestoreLeft = appWindow.Position.X;
        }
    }

    [DynamicWindowsRuntimeCast(typeof(OverlappedPresenter))]
    private void AppWindow_Changed(AppWindow sender, AppWindowChangedEventArgs args)
    {
        if (!args.DidPositionChange)
        {
            return;
        }

        //Microsoft.UI.Windowing.AppWindow? appWindow = this.AppWindow;
        if (sender == null)
        {
            return;
        }
        if (sender.Presenter is not OverlappedPresenter presenter)
        {
            return;
        }
        if (presenter.State == OverlappedPresenterState.Restored)
        {
            _winRestoreHeight = sender.Size.Height;
            _winRestoreWidth = sender.Size.Width;
            _winRestoreTop = sender.Position.Y;
            _winRestoreLeft = sender.Position.X;
        }
    }

    [DynamicWindowsRuntimeCast(typeof(FrameworkElement))]
    public void SetCapitionButtonColorForWin11()
    {
        var currentTheme = ((FrameworkElement)Content).ActualTheme;
        if (currentTheme == ElementTheme.Dark)
        {
            this.AppWindow.TitleBar.ButtonForegroundColor = Colors.White;
            this.AppWindow.TitleBar.ButtonInactiveForegroundColor = Colors.White;
        }
        else if (currentTheme == ElementTheme.Light)
        {
            this.AppWindow.TitleBar.ButtonForegroundColor = Colors.Black;
            this.AppWindow.TitleBar.ButtonInactiveForegroundColor = Colors.Black;
        }
        else
        {
            if (Application.Current.RequestedTheme == ApplicationTheme.Dark)
            {
                this.AppWindow.TitleBar.ButtonForegroundColor = Colors.White;
                this.AppWindow.TitleBar.ButtonInactiveForegroundColor = Colors.White;
            }
            else
            {
                this.AppWindow.TitleBar.ButtonForegroundColor = Colors.Black;
                this.AppWindow.TitleBar.ButtonInactiveForegroundColor = Colors.Black;
            }
        }
    }

    [DynamicWindowsRuntimeCast(typeof(OverlappedPresenter))]
    [DynamicWindowsRuntimeCast(typeof(FrameworkElement))]
    private void LoadSettings()
    {
        if (DesktopAcrylicController.IsSupported())
        {
            _vm.IsAcrylicSupported = true;

            _vm.IsBackdropEnabled = true;
        }
        if (MicaController.IsSupported())
        {
            _vm.IsMicaSupported = true;

            _vm.IsBackdropEnabled = true;
        }

        if (string.IsNullOrEmpty(_configurationService.ConfigFilePath) || (!File.Exists(_configurationService.ConfigFilePath)))
        {
            // Sets default.

            if (DesktopAcrylicController.IsSupported())
            {
                SystemBackdrop = new DesktopAcrylicBackdrop();
                _vm.Material = SystemBackdropOption.Acrylic;

                _vm.IsBackdropEnabled = true;
            }
            else if (MicaController.IsSupported())
            {
                SystemBackdrop = new MicaBackdrop()
                {
                    Kind = MicaKind.Base
                };
                _vm.Material = SystemBackdropOption.Mica;

                _vm.IsBackdropEnabled = true;
            }

            theme = ElementTheme.Default;

            return;
        }

        var config = _configurationService.CurrentConfiguration;
        Debug.WriteLine($"MainWindow Configuration: Width={config.MainWindow.Width}, Height={config.MainWindow.Height}, Top={config.MainWindow.Top}, Left={config.MainWindow.Left}, RestoreWidth={config.MainWindow.RestoreWidth}, RestoreHeight={config.MainWindow.RestoreHeight}, RestoreTop={config.MainWindow.RestoreTop}, RestoreLeft={config.MainWindow.RestoreLeft}");

        if (config.MainWindow.Width < 500)
        {
            config.MainWindow.Width = 500;
        }
        if (config.MainWindow.Height < 500)
        {
            config.MainWindow.Height = 500;
        }
        if (config.MainWindow.Top < 0)
        {
            config.MainWindow.Top = 0;
        }
        if (config.MainWindow.Left < 0)
        {
            config.MainWindow.Left = 0;
        }

        if (config.MainWindow.RestoreWidth < 500)
        {
            config.MainWindow.RestoreWidth = 500;
        }
        if (config.MainWindow.RestoreHeight < 500)
        {
            config.MainWindow.RestoreHeight = 500;
        }
        if (config.MainWindow.RestoreTop < 0)
        {
            config.MainWindow.RestoreTop = 0;
        }
        if (config.MainWindow.RestoreLeft < 0)
        {
            config.MainWindow.RestoreLeft = 0;
        }

        // Restore window size and position
        Microsoft.UI.Windowing.AppWindow? appWindow = this.AppWindow;
        if (appWindow != null)
        {
            // AOT bad
            /*
            // https://github.com/microsoft/CsWinRT/issues/1930
            // Window state
            if (appWindow.Presenter is OverlappedPresenter presenter)
            {
            }
            */

            if (config.MainWindow.WindowState == OverlappedPresenterState.Maximized)
            {
                // Sets restore size and position.
                appWindow.MoveAndResize(new Windows.Graphics.RectInt32(config.MainWindow.Left, config.MainWindow.Top, config.MainWindow.Width, config.MainWindow.Height));
                // Maximize the window.
                (appWindow.Presenter as OverlappedPresenter)!.Maximize();
            }
            else if (config.MainWindow.WindowState == OverlappedPresenterState.Minimized)
            {
                // This should not happen.

                // DONT restore minimized size and pos. bad.

                (appWindow.Presenter as OverlappedPresenter)!.Restore();

                appWindow.MoveAndResize(new Windows.Graphics.RectInt32(config.MainWindow.Left, config.MainWindow.Top, config.MainWindow.Width, config.MainWindow.Height));
            }
            else if (config.MainWindow.WindowState == OverlappedPresenterState.Restored)
            {
                // TODO:
                //var dpi = Windows.Win32.PInvoke.GetDpiForWindow(new Windows.Win32.Foundation.HWND(WinRT.Interop.WindowNative.GetWindowHandle(this)));
                //var scalingFactor = (float)dpi / 96;
                //AppWindow.Resize(new Windows.Graphics.SizeInt32((int)(400.0f * scalingFactor), (int)(300.0f * scalingFactor)));

                // Sets restore size and position.
                appWindow.MoveAndResize(new Windows.Graphics.RectInt32(config.MainWindow.Left, config.MainWindow.Top, config.MainWindow.Width, config.MainWindow.Height));
            }
        }


        // Apply theme and backdrop
        if (config.SystemBackdrop != SystemBackdropOption.None)
        {
            theme = config.Theme;
            _vm.Theme = config.Theme;
        }
        _vm.Material = config.SystemBackdrop;
        SwitchBackdrop(config.SystemBackdrop);

    }

    public void SwitchBackdrop(SystemBackdropOption backdrop)
    {
        var vm = App.GetService<MainViewModel>();

        if (backdrop == SystemBackdropOption.Mica)
        {
            if (MicaController.IsSupported())
            {
                this.SystemBackdrop = new MicaBackdrop()
                {
                    Kind = MicaKind.Base
                };
                vm.Material = SystemBackdropOption.Mica;
                //TitleBarHelper.UpdateTitleBar(Theme, App.MainWnd);

                vm.IsBackdropEnabled = true;
            }
        }
        else if (backdrop == SystemBackdropOption.MicaAlt)
        {
            if (MicaController.IsSupported())
            {
                this.SystemBackdrop = new MicaBackdrop()
                {
                    Kind = MicaKind.BaseAlt
                };
                vm.Material = SystemBackdropOption.MicaAlt;
                //TitleBarHelper.UpdateTitleBar(Theme, App.MainWnd);

                vm.IsBackdropEnabled = true;
            }
        }
        else if (backdrop == SystemBackdropOption.Acrylic)
        {
            if (DesktopAcrylicController.IsSupported())
            {
                this.SystemBackdrop = new DesktopAcrylicBackdrop();

                vm.Material = SystemBackdropOption.Acrylic;
                //TitleBarHelper.UpdateTitleBar(Theme, App.MainWnd);

                vm.IsBackdropEnabled = true;
            }
        }
        else if (backdrop == SystemBackdropOption.None)
        {
            this.SystemBackdrop = null;

            vm.Material = SystemBackdropOption.None;
            vm.IsBackdropEnabled = false;
            vm.Theme = ElementTheme.Default;
            theme = ElementTheme.Default;
            if (this.Content is ShellPage root)
            {
                root.RequestedTheme = theme;

                //TitleBarHelper.UpdateTitleBar(theme, this);
                //SetCapitionButtonColorForWin11();
            }

            this.SystemBackdrop = null;
        }
    }

    [DynamicWindowsRuntimeCast(typeof(OverlappedPresenter))]
    private void Window_SizeChanged(object sender, WindowSizeChangedEventArgs args)
    {
        Microsoft.UI.Windowing.AppWindow? appWindow = this.AppWindow;
        if (appWindow != null)
        {
            if (appWindow.Presenter is OverlappedPresenter presenter)
            {
                if (presenter.State == OverlappedPresenterState.Maximized)
                {
                }
                else if (presenter.State == OverlappedPresenterState.Minimized)
                {
                }
                else
                {
                    _winRestoreHeight = (int)appWindow.Size.Height;
                    _winRestoreWidth = (int)appWindow.Size.Width;
                    _winRestoreTop = (int)appWindow.Position.Y;
                    _winRestoreLeft = (int)appWindow.Position.X;
                }
            }
        }
    }

}

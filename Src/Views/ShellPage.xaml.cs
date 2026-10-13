using FeedDesk.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WinRT;
using System;

namespace FeedDesk.Views;

public sealed partial class ShellPage : Page
{
    private MainWindow? _mainWindow;

    public ShellPage(MainViewModel vm)
    {
        ViewModel = vm;//App.GetService<MainViewModel>(); ;

        InitializeComponent();

        NavigationFrame.Content = App.GetService<MainPage>();

        App.AppTitlebar = AppTitleBar as UIElement;

        this.ActualThemeChanged += this.This_ActualThemeChanged;

        Loaded += ShellPage_Loaded;
        Unloaded += Page_Unloaded;
    }

    public MainViewModel ViewModel { get; }

    public Frame NavFrame => NavigationFrame;

    public void SetTitleBar(MainWindow wnd)
    {
        // MainWindow is null when ShellPage is created because of the order of creation.
        // Instead, call this when mainWindow is created.

        wnd.SetTitleBar(AppTitleBar);

        _mainWindow = wnd;
        _mainWindow.Activated += MainWindow_Activated;
    }

    private void This_ActualThemeChanged(FrameworkElement sender, object args)
    {
        if (App.MainWnd is null)
        {
            return;
        }

        App.MainWnd.SetCapitionButtonColorForWin11();
    }

    // TEMP: Require CsWinRT 2.3.0-prerelease.251115.2
    // https://github.com/dotnet/runtime/issues/121590
    [DynamicWindowsRuntimeCast(typeof(SolidColorBrush))]
    private void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        var resource = args.WindowActivationState == WindowActivationState.Deactivated ? "WindowCaptionForegroundDisabled" : "WindowCaptionForeground";

        AppTitleBarText.Foreground = (SolidColorBrush)Application.Current.Resources[resource];
        AppTitleBarIcon.Opacity = args.WindowActivationState == WindowActivationState.Deactivated ? 0.4 : 0.8;
        AppMenuBar.Opacity = args.WindowActivationState == WindowActivationState.Deactivated ? 0.4 : 0.8;
    }

    private async void ShellPage_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= ShellPage_Loaded;
        try
        {
            await ViewModel.InitializeAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ShellPage init failed: {ex}");
        }
    }

    private void Page_Unloaded(object sender, RoutedEventArgs e)
    {
        // Unsub
        
        _mainWindow?.Activated -= MainWindow_Activated;

        this.ActualThemeChanged -= this.This_ActualThemeChanged;
    }
}

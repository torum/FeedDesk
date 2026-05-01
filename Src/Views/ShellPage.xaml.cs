using FeedDesk.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Windows.System;

namespace FeedDesk.Views;

public sealed partial class ShellPage : Page
{
    public MainViewModel ViewModel { get; }

    public Frame NavFrame => NavigationFrame;
    
    public ShellPage()
    {
        ViewModel = App.GetService<MainViewModel>(); ;

        InitializeComponent();

        NavigationFrame.Content = App.GetService<MainPage>();

        App.AppTitlebar = AppTitleBar as UIElement;

        this.ActualThemeChanged += this.This_ActualThemeChanged;
    }

    public void InitWhenMainWindowIsReady(MainWindow wnd)
    {
        // MainWindow is null when ShellPage is created because of the order of creation.
        // Instead, InitWhenMainWindowIsReady() is called when mainWindow is created.

        wnd.SetTitleBar(AppTitleBar);

        wnd.Activated += MainWindow_Activated;
    }

    private void This_ActualThemeChanged(FrameworkElement sender, object args)
    {
        if (App.MainWnd is null)
        {
            return;
        }

        App.MainWnd.SetCapitionButtonColorForWin11();
    }

    private void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        var resource = args.WindowActivationState == WindowActivationState.Deactivated ? "WindowCaptionForegroundDisabled" : "WindowCaptionForeground";

        AppTitleBarText.Foreground = (SolidColorBrush)App.Current.Resources[resource];
        AppTitleBarIcon.Opacity = args.WindowActivationState == WindowActivationState.Deactivated ? 0.4 : 0.8;
        AppMenuBar.Opacity = args.WindowActivationState == WindowActivationState.Deactivated ? 0.4 : 0.8;
    }

    private void Page_Unloaded(object sender, RoutedEventArgs e)
    {
        // Unsub
        var wnd = App.GetService<MainWindow>();
        wnd?.Activated -= MainWindow_Activated;
        this.ActualThemeChanged -= this.This_ActualThemeChanged;
    }
}

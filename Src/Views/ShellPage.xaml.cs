using FeedDesk.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using Windows.System;
using WinRT;

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

        await Task.Yield();

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

    private void KeyboardAccelerator_Invoked(Microsoft.UI.Xaml.Input.KeyboardAccelerator sender, Microsoft.UI.Xaml.Input.KeyboardAcceleratorInvokedEventArgs args)
    {
        VirtualKeyModifiers modifiers = args.KeyboardAccelerator.Modifiers;

        bool isControlPressed = modifiers.HasFlag(VirtualKeyModifiers.Control);
        bool isShiftPressed = modifiers.HasFlag(VirtualKeyModifiers.Shift);
        bool isAltPressed = modifiers.HasFlag(VirtualKeyModifiers.Menu); // Note: Alt maps to 'Menu'

        bool isAKeyPressed = args.KeyboardAccelerator.Key == VirtualKey.A;

        if (isControlPressed && isShiftPressed && isAKeyPressed)
        {
            if (ViewModel.EntryArchiveReadCommand.CanExecute(null))
            {
                ViewModel.EntryArchiveReadCommand.Execute(null);
            }
            args.Handled = true;
            return;
        }

        bool isNKeyPressed = args.KeyboardAccelerator.Key == VirtualKey.N;

        if (isControlPressed && isShiftPressed && isNKeyPressed)
        {
            if (ViewModel.FolderAddCommand.CanExecute(null))
            {
                ViewModel.FolderAddCommand.Execute(null);
            }
            args.Handled = true;
            return;
        }
        else if (isControlPressed && isNKeyPressed)
        {
            if (ViewModel.FeedAddCommand.CanExecute(null))
            {
                ViewModel.FeedAddCommand.Execute(null);
            }
            args.Handled = true;
            return;
        }

        bool isRKeyPressed = args.KeyboardAccelerator.Key == VirtualKey.R;

        if (isControlPressed && isRKeyPressed)
        {
            if (ViewModel.FeedRefreshAllCommand.CanExecute(null))
            {
                ViewModel.FeedRefreshAllCommand.Execute(null);
            }
            args.Handled = true;
            return;
        }

        bool isEKeyPressed = args.KeyboardAccelerator.Key == VirtualKey.E;

        if (isControlPressed && isEKeyPressed)
        {
            if (ViewModel.NodeEditCommand.CanExecute(null))
            {
                ViewModel.NodeEditCommand.Execute(null);
            }
            args.Handled = true;
            return;
        }

        bool isDelKeyPressed = args.KeyboardAccelerator.Key == VirtualKey.Delete;

        if (isDelKeyPressed && (!isControlPressed && !isShiftPressed && !isAltPressed))
        {
            if (ViewModel.NodeRemoveCommand.CanExecute(null))
            {
                ViewModel.NodeRemoveCommand.Execute(null);
            }
            args.Handled = true;
            return;
        }

        bool isOKeyPressed = args.KeyboardAccelerator.Key == VirtualKey.O;

        if (isControlPressed && isOKeyPressed)
        {
            if (ViewModel.EntryViewExternalCommand.CanExecute(null))
            {
                ViewModel.EntryViewExternalCommand.Execute(null);
            }
            args.Handled = true;
            return;
        }
    }
}

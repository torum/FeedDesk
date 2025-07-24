using System.Xml;
using System.Xml.Linq;
using FeedDesk.Services.Contracts;
using FeedDesk.Services;
using FeedDesk.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using Windows.System;
using FeedDesk.Helpers;
using static System.Net.Mime.MediaTypeNames;
using System.Threading.Tasks;
using System.Diagnostics;

namespace FeedDesk.Views;

public sealed partial class ShellPage : Page
{
    public MainViewModel ViewModel
    {
        get;
    }
    /*
    // List of ValueTuple holding the Navigation Tag and the relative Navigation Page
    private readonly List<(string Tag, Type? Page)> _pages =
    [
        //("Rent", typeof(Rent.RentSearchPage)),
        ("Rent", null),
            ("RentSearch", typeof(Rent.RentSearchPage)),
            ("RentResidentials", typeof(Rent.Residentials.SearchPage)),
            ("RentCommercials", typeof(Rent.Commercials.CommercialsPage)),
            ("RentParkings", typeof(Rent.Parkings.ParkingsPage)),
            ("RentOwners", typeof(Rent.Owners.OwnersPage)),
            ("Brokers", typeof(Brokers.BrokersPage)),
            //("Settings", typeof(SettingsPage)),
        ];
    */
    // For uses of Navigation in other pages.
    public Frame NavFrame => NavigationFrame;

    private readonly IThemeSelectorService _themeSelectorService;
    
    public ShellPage(IThemeSelectorService themeSelectorService)
    {
        ViewModel = App.GetService<MainViewModel>(); ;

        _themeSelectorService = themeSelectorService;

        InitializeComponent();

        //AppTitleBarText.Text = "AppDisplayName".GetLocalized();

        NavigationFrame.Content = App.GetService<MainPage>();

        if (App.MainWnd is not null)
        {
            App.MainWnd.ExtendsContentIntoTitleBar = true;
            App.MainWnd.SetTitleBar(AppTitleBar);

            App.MainWnd.Activated += MainWindow_Activated;
            App.MainWnd.Closed += MainWindow_Closed;
        }
        else
        {
            Debug.WriteLine("MainWindow is null @ShellPage::Constructor");
        }

        InitThemeService();

        //NavigationFrame.Content = App.GetService<MainPage>();
        //NavigationFrame?.Navigate(typeof(MainPage), null, new SlideNavigationTransitionInfo() { Effect = SlideNavigationTransitionEffect.FromRight });
    }

    private async void InitThemeService()
    {
        await _themeSelectorService.InitializeAsync();//.ConfigureAwait(false)
        await _themeSelectorService.SetRequestedThemeAsync();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Needed to be here. (don't put this in constructor.. messes up when theme changed.)
        //TitleBarHelper.UpdateTitleBar(RequestedTheme, App.MainWindow);

        KeyboardAccelerators.Add(BuildKeyboardAccelerator(VirtualKey.Left, VirtualKeyModifiers.Menu));
        KeyboardAccelerators.Add(BuildKeyboardAccelerator(VirtualKey.GoBack));

        //ShellMenuBarSettingsButton.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler(ShellMenuBarSettingsButton_PointerPressed), true);
        //ShellMenuBarSettingsButton.AddHandler(UIElement.PointerReleasedEvent, new PointerEventHandler(ShellMenuBarSettingsButton_PointerReleased), true);

        // give some time to let window draw itself.
        //await Task.Delay(100);
        //await Task.Yield();
        //_navigationService.NavigateTo(typeof(MainViewModel).FullName!);


        //NavigationFrame?.Navigate(typeof(SettingsPage));
    }

    private void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        App.AppTitlebar = AppTitleBarText as UIElement;

        var resource = args.WindowActivationState == WindowActivationState.Deactivated ? "WindowCaptionForegroundDisabled" : "WindowCaptionForeground";

        AppTitleBarText.Foreground = (SolidColorBrush)App.Current.Resources[resource];
        AppTitleBarIcon.Opacity = args.WindowActivationState == WindowActivationState.Deactivated ? 0.4 : 0.8;
        AppMenuBar.Opacity = args.WindowActivationState == WindowActivationState.Deactivated ? 0.4 : 0.8;


        // 
        //SViewModel.NavigationService.NavigateTo(typeof(MainViewModel).FullName!);
        //_navigationService.NavigateTo(typeof(MainViewModel).FullName!);
        //NavigationFrame.Navigate(typeof(ViewModels.MainViewModel), null, new SlideNavigationTransitionInfo() { Effect = SlideNavigationTransitionEffect.FromRight });
        //NavigationFrame?.Navigate(typeof(Views.MainPage), null, new SlideNavigationTransitionInfo() { Effect = SlideNavigationTransitionEffect.FromRight });

    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        //ShellMenuBarSettingsButton.RemoveHandler(UIElement.PointerPressedEvent, (PointerEventHandler)ShellMenuBarSettingsButton_PointerPressed);
        //ShellMenuBarSettingsButton.RemoveHandler(UIElement.PointerReleasedEvent, (PointerEventHandler)ShellMenuBarSettingsButton_PointerReleased);
    }

    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        var vm = App.GetService<MainViewModel>();

        // Save service tree.
        vm.SaveServiceXml();

        // Dispose httpclient.
        vm.CleanUp();


        // Save err log.
        (App.Current as App)?.SaveErrorLog();
    }

    private static KeyboardAccelerator BuildKeyboardAccelerator(VirtualKey key, VirtualKeyModifiers? modifiers = null)
    {
        var keyboardAccelerator = new KeyboardAccelerator() { Key = key };

        if (modifiers.HasValue)
        {
            keyboardAccelerator.Modifiers = modifiers.Value;
        }

        keyboardAccelerator.Invoked += OnKeyboardAcceleratorInvoked;

        return keyboardAccelerator;
    }

    private static void OnKeyboardAcceleratorInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        // TODO:!
        /*
        var navigationService = App.GetService<INavigationService>();

        var result = navigationService.GoBack();

        args.Handled = result;
        */
    }
    /*

    private void ShellMenuBarSettingsButton_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        AnimatedIcon.SetState((UIElement)sender, "PointerOver");
    }

    private void ShellMenuBarSettingsButton_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        AnimatedIcon.SetState((UIElement)sender, "Pressed");
    }

    private void ShellMenuBarSettingsButton_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        AnimatedIcon.SetState((UIElement)sender, "Normal");
    }

    private void ShellMenuBarSettingsButton_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        AnimatedIcon.SetState((UIElement)sender, "Normal");
    }
    */


    /*
    private void NavigationFrame_Navigated(object sender, NavigationEventArgs e)
    {
        if (NavigationFrame.SourcePageType == typeof(SettingsPage))
        {
            // SettingsItem is not part of NavView.MenuItems, and doesn't have a Tag.
            //NavigationViewControl.SelectedItem = (NavigationViewItem)NavigationViewControl.SettingsItem;
            //NavigationViewControl.Header = "設定";
            return;
        }
        else if (NavigationFrame.SourcePageType != null)
        {
            //NavigationViewControl.Header = null;

            //var item = _pages.FirstOrDefault(p => p.Page == e.SourcePageType);

            //NavigationViewControl.SelectedItem = NavigationViewControl.MenuItems.OfType<NavigationViewItem>().First(n => n.Tag.Equals(item.Tag));

            //NavigationViewControl.Header = ((NavigationViewItem)NavigationViewControl.SelectedItem)?.Content?.ToString();

            // Do nothing.
        }
    }
    */

    private void NavigationFrame_NavigationFailed(object sender, NavigationFailedEventArgs e)
    {
        e.Handled = true;
    }

    private void NavigationFrame_Loaded(object sender, RoutedEventArgs e)
    {

    }
}
